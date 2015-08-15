// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Database.Utility;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Utility;
using Microsoft.Practices.EnterpriseLibrary.TransientFaultHandling;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Sinks
{
    /// <summary>
    /// Sink that asynchronously writes entries to SQL Server database.
    /// </summary>
    public class SqlDatabaseSink : IObserver<EventEntry>, IDisposable
    {
        private readonly RetryPolicy retryPolicy = new RetryPolicy<SqlDatabaseTransientErrorDetectionStrategy>(5, TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5));
        private readonly string instanceName;
        private readonly string connectionString;
        private readonly string tableName;
        private readonly string storedProcedureName;
        private readonly BufferedEventPublisher<EventEntry> bufferedPublisher;
        private readonly TimeSpan onCompletedTimeout;
        private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlDatabaseSink" /> class with the specified instance name, connection string and table name.
        /// </summary>
        /// <param name="instanceName">The name of the instance originating the entries.</param>
        /// <param name="connectionString">The connection string.</param>
        /// <param name="tableName">The name of the table.</param>
        /// <param name="storedProcedureName">The name of the stored procedure that writes to table></param>
        /// <param name="bufferingInterval">The buffering interval between each batch publishing.</param>
        /// <param name="bufferingCount">The number of entries that will trigger a batch publishing.</param>
        /// <param name="maxBufferSize">The maximum number of entries that can be buffered while it's sending to the store before the sink starts dropping entries.</param>      
        /// <param name="onCompletedTimeout">Defines a timeout interval for when flushing the entries after an <see cref="OnCompleted"/> call is received and before disposing the sink.
        /// This means that if the timeout period elapses, some event entries will be dropped and not sent to the store. Normally, calling <see cref="IDisposable.Dispose"/> on 
        /// the <see cref="System.Diagnostics.Tracing.EventListener"/> will block until all the entries are flushed or the interval elapses.
        /// If <see langword="null"/> is specified, then the call will block indefinitely until the flush operation finishes.</param>
        public SqlDatabaseSink(string instanceName, string connectionString, string tableName, string storedProcedureName, TimeSpan bufferingInterval, int bufferingCount, int maxBufferSize, TimeSpan onCompletedTimeout)
        {
            Guard.ArgumentNotNullOrEmpty(instanceName, "instanceName");
            Guard.ArgumentNotNullOrEmpty(connectionString, "connectionString");
            Guard.ArgumentNotNullOrEmpty(tableName, "tableName");
            Guard.ArgumentNotNullOrEmpty(tableName, "storedProcedureName");
            Guard.ArgumentIsValidTimeout(onCompletedTimeout, "onCompletedTimeout");
            ValidateSqlConnectionString(connectionString, "connectionString");

            this.instanceName = instanceName;
            this.connectionString = connectionString;
            this.tableName = tableName;
            this.storedProcedureName = storedProcedureName;
            this.onCompletedTimeout = onCompletedTimeout;
            this.retryPolicy.Retrying += Retrying;
            string sinkId = string.Format(CultureInfo.InvariantCulture, "SqlDatabaseSink ({0})", instanceName);
            this.bufferedPublisher = BufferedEventPublisher<EventEntry>.CreateAndStart(sinkId, this.PublishEventsAsync, bufferingInterval, bufferingCount, maxBufferSize, this.cancellationTokenSource.Token);
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="SqlDatabaseSink"/> class.
        /// </summary>
        ~SqlDatabaseSink()
        {
            this.Dispose(false);
        }

        /// <summary>
        /// Flushes the buffer content to <see cref="PublishEventsAsync"/>.
        /// </summary>
        /// <returns>The Task that flushes the buffer.</returns>
        public Task FlushAsync()
        {
            return this.bufferedPublisher.FlushAsync();
        }

        /// <summary>
        /// Releases all resources used by the current instance of the <see cref="SqlDatabaseSink"/> class.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Notifies the observer that the provider has finished sending push-based notifications.
        /// </summary>
        public void OnCompleted()
        {
            this.FlushSafe();
            this.Dispose();
        }

        /// <summary>
        /// Notifies the observer that the provider has experienced an error condition.
        /// </summary>
        /// <param name="error">An object that provides additional information about the error.</param>
        public void OnError(Exception error)
        {
            this.FlushSafe();
            this.Dispose();
        }

        /// <summary>
        /// Provides the sink with new data to write.
        /// </summary>
        /// <param name="value">The current entry to write to the database.</param>
        public void OnNext(EventEntry value)
        {
            this.bufferedPublisher.TryPost(value);
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <param name="disposing">A value indicating whether or not the class is disposing.</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2213:DisposableFieldsShouldBeDisposed", MessageId = "cancellationTokenSource", Justification = "Token is cancelled")]
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.cancellationTokenSource.Cancel();
                this.bufferedPublisher.Dispose();
            }
        }

        private static void Retrying(object sender, RetryingEventArgs e)
        {
            SemanticLoggingEventSource.Log.DatabaseSinkPublishEventsTransientError(e.LastException.ToString());
        }

        private static void ValidateSqlConnectionString(string connectionStringValue, string argumentName)
        {
            Guard.ArgumentNotNullOrEmpty(connectionStringValue, argumentName);

            try
            {
                var builder = new SqlConnectionStringBuilder();
                builder.ConnectionString = connectionStringValue;
            }
            catch (ArgumentException e)
            {
                throw new ArgumentException(Properties.Resources.InvalidConnectionStringError, argumentName, e);
            }
        }

        private async Task<int> PublishEventsAsync(IList<EventEntry> collection)
        {
            int publishedEvents = collection.Count;

            try
            {
                if (collection.Count > 128)
                {
                    await this.UseSqlBulkCopy(collection).ConfigureAwait(false);
                }
                else
                {
                    await this.UseStoredProcedure(collection).ConfigureAwait(false);
                }

                return publishedEvents;
            }
            catch (OperationCanceledException)
            {
                return 0;
            }
            catch (Exception ex)
            {
                if (this.cancellationTokenSource.IsCancellationRequested)
                {
                    return 0;
                }

                SemanticLoggingEventSource.Log.DatabaseSinkPublishEventsFailed(ex.ToString());
                throw;
            }
        }

        private async Task UseSqlBulkCopy(IList<EventEntry> collection)
        {
            int initialCount = collection.Count;

            for (int retries = 0; retries < 3; retries++)
            {
                using (var reader = new EventEntryDataReader(collection, this.instanceName))
                {
                    try
                    {
                        await this.TrySqlBulkCopy(reader).ConfigureAwait(false);
                        return;
                    }
                    catch (InvalidOperationException ex)
                    {
                        //// if all events were published throw                       
                        if (reader.RecordsAffected == collection.Count)
                        {
                            throw;
                        }

                        int affectedRow = reader.RecordsAffected - 1;
                        SemanticLoggingEventSource.Log.DatabaseSinkPublishEventsFailedAndDiscardSingleEntry(ex.Message, affectedRow);
                        ////retry after removing the offending record
                        collection.RemoveAt(affectedRow);
                    }
                }
            }

            ////If still pending events after all retries, discard batch and log.
            if (initialCount != collection.Count)
            {
                SemanticLoggingEventSource.Log.DatabaseSinkPublishEventsFailedAndDiscardsEntries(collection.Count);
            }
        }

        private async Task TrySqlBulkCopy(IDataReader reader)
        {
            var token = this.cancellationTokenSource.Token;

            using (var sqlBulkCopy = new SqlBulkCopy(this.connectionString, SqlBulkCopyOptions.UseInternalTransaction))
            {
                sqlBulkCopy.DestinationTableName = this.tableName;
                sqlBulkCopy.ColumnMappings.Add("InstanceName", "InstanceName");
                sqlBulkCopy.ColumnMappings.Add("ProviderId", "ProviderId");
                sqlBulkCopy.ColumnMappings.Add("ProviderName", "ProviderName");
                sqlBulkCopy.ColumnMappings.Add("EventId", "EventId");
                sqlBulkCopy.ColumnMappings.Add("EventKeywords", "EventKeywords");
                sqlBulkCopy.ColumnMappings.Add("Level", "Level");
                sqlBulkCopy.ColumnMappings.Add("Opcode", "Opcode");
                sqlBulkCopy.ColumnMappings.Add("Task", "Task");
                sqlBulkCopy.ColumnMappings.Add("Timestamp", "Timestamp");
                sqlBulkCopy.ColumnMappings.Add("Version", "Version");
                sqlBulkCopy.ColumnMappings.Add("FormattedMessage", "FormattedMessage");
                sqlBulkCopy.ColumnMappings.Add("Payload", "Payload");
                sqlBulkCopy.ColumnMappings.Add("ActivityId", "ActivityId");
                sqlBulkCopy.ColumnMappings.Add("RelatedActivityId", "RelatedActivityId");
                sqlBulkCopy.ColumnMappings.Add("ProcessId", "ProcessId");
                sqlBulkCopy.ColumnMappings.Add("ThreadId", "ThreadId");

                await this.retryPolicy.ExecuteAsync(() => sqlBulkCopy.WriteToServerAsync(reader, token), token).ConfigureAwait(false);
            }
        }

        private async Task UseStoredProcedure(IList<EventEntry> collection)
        {
            var token = this.cancellationTokenSource.Token;

            await this.retryPolicy.ExecuteAsync(
                async () =>
                {
                    using (var conn = new SqlConnection(this.connectionString))
                    {
                        await conn.SuppressTransactionOpenAsync(token).ConfigureAwait(false);

                        using (var reader = new EventEntryDataReader(collection, this.instanceName))
                        using (var cmd = new SqlCommand(storedProcedureName, conn))
                        {
                            cmd.CommandType = CommandType.StoredProcedure;
                            cmd.Parameters.Add(new SqlParameter("@InsertTraces", SqlDbType.Structured));
                            cmd.Parameters[0].Value = reader;
                            cmd.Parameters[0].TypeName = "dbo.TracesType";

                            return await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                        }
                    }
                },
                token).ConfigureAwait(false);
        }

        private void FlushSafe()
        {
            try
            {
                this.FlushAsync().Wait(this.onCompletedTimeout);
            }
            catch (AggregateException ex)
            {
                // Flush operation will already log errors. Never expose this exception to the observable.
                ex.Handle(e => e is FlushFailedException);
            }
        }
    }
}
