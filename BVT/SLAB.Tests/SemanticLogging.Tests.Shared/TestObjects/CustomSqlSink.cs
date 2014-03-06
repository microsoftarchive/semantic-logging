// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using System.Xml.Linq;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Configuration;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Observable;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Schema;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Sinks.Database;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Utility;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.Shared.TestObjects
{
    public class MyCustomSinkSqlElement : ISinkElement
    {
        private readonly XName sinkName = XName.Get("CustomSqlSink", "urn:sqlTest");

        public bool CanCreateSink(XElement element)
        {
            return element.Name == this.sinkName;
        }

        public IObserver<EventEntry> CreateSink(XElement element)
        {
            string bufferingIntervalInSecondsAttr = (string)element.Attribute("bufferingIntervalInSeconds");
            TimeSpan? bufferingInterval = string.IsNullOrWhiteSpace(bufferingIntervalInSecondsAttr) ? (TimeSpan?)null : TimeSpan.FromSeconds(int.Parse(bufferingIntervalInSecondsAttr));

            int bufferingCount = (int?)element.Attribute("bufferingCount") ?? default(int);

            var subject = new EventEntrySubject();

            subject.LogToCustomSqlDatabase(
                (string)element.Attribute("instanceName"),
                (string)element.Attribute("connectionString"),
                (string)element.Attribute("tableName"),
                bufferingInterval,
                bufferingCount);

            return subject;
        }
    }

    /// <summary>
    /// Sink that asynchronously writes entries to SQL Server database.
    /// </summary>
    [ComVisible(false)]
    public class CustomSqlSink : IObserver<EventRecord>, IDisposable
    {
        /// <summary>
        /// Default table name used to write traces.
        /// </summary>
        public const string DefaultTableName = "Traces";

        private readonly string instanceName;
        private readonly string connectionString;
        private readonly string tableName;
        private readonly BufferedEventPublisher<EventRecord> bufferedPublisher;
        private readonly EventSourceSchemaCache schemaCache = EventSourceSchemaCache.Instance;
        private readonly DbProviderFactory dbProviderFactory;
        private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlDatabaseSink" /> class with the specified instance name, connection string and table name.
        /// </summary>
        /// <param name="instanceName">The name of the instance originating the entries.</param>
        /// <param name="connectionString">The connection string.</param>
        /// <param name="tableName">The name of the table.</param>
        /// <param name="bufferingInterval">The buffering interval between each batch publishing.</param>
        /// <param name="bufferingCount">The number of entries that will trigger a batch publishing.</param>
        public CustomSqlSink(string instanceName, string connectionString, string tableName = "Traces", TimeSpan? bufferingInterval = null, int bufferingCount = Buffering.DefaultBufferingCount)
        {
            Guard.ArgumentNotNullOrEmpty(instanceName, "instanceName");
            ValidateSqlConnectionString(connectionString, "connectionString");

            string sinkId = string.Format(CultureInfo.InvariantCulture, "DatabaseSink ({0})", instanceName);
            this.dbProviderFactory = SqlClientFactory.Instance;
            this.bufferedPublisher = new BufferedEventPublisher<EventRecord>(sinkId, this.PublishEventsAsync, bufferingInterval ?? Buffering.DefaultBufferingInterval, bufferingCount, 30000, this.cancellationTokenSource.Token);
            this.instanceName = instanceName;
            this.connectionString = connectionString;
            this.tableName = tableName ?? "Traces";
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="SqlDatabaseSink"/> class.
        /// </summary>
        ~CustomSqlSink()
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
            this.FlushAsync().Wait();
            this.Dispose();
        }

        /// <summary>
        /// Notifies the observer that the provider has experienced an error condition.
        /// </summary>
        /// <param name="error">An object that provides additional information about the error.</param>
        public void OnError(Exception error)
        {
            this.FlushAsync().Wait();
            this.Dispose();
        }

        /// <summary>
        /// Provides the sink with new data to write.
        /// </summary>
        /// <param name="value">The current entry to write to the database.</param>
        public void OnNext(EventRecord value)
        {
            if (value != null)
            {
                if (string.IsNullOrEmpty(value.InstanceName))
                {
                    value.InstanceName = this.instanceName;
                }

                this.bufferedPublisher.TryPost(value);
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                if (disposing)
                {
                    if (this.bufferedPublisher != null)
                    {
                        this.bufferedPublisher.Dispose();
                    }

                    if (this.cancellationTokenSource != null)
                    {
                        this.cancellationTokenSource.Dispose();
                    }
                }

                this.cancellationTokenSource = null;
                this.disposed = true;
            }
        }

        //private static void Retrying(object sender, Transient.RetryingEventArgs e)
        //{
        //    SemanticLoggingEventSource.Log.DatabaseSinkOpenDatabaseFailed(e.LastException.ToString());
        //}

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
                throw new ArgumentException("Invalid Connection string", argumentName, e);
            }
        }

        private static DataTable GetDataTable(string instanceName, IEnumerable<EventRecord> collection)
        {
            var table = new DataTable();
            table.Columns.Add("InstanceName", typeof(string));
            table.Columns.Add("ProviderId", typeof(Guid));
            table.Columns.Add("ProviderName", typeof(string));
            table.Columns.Add("EventId", typeof(int));
            table.Columns.Add("EventKeywords", typeof(long));
            table.Columns.Add("Level", typeof(string));
            table.Columns.Add("Opcode", typeof(int));
            table.Columns.Add("Task", typeof(int));
            table.Columns.Add("Timestamp", typeof(DateTime));
            table.Columns.Add("Version", typeof(int));
            table.Columns.Add("FormattedMessage", typeof(string));
            table.Columns.Add("Payload", typeof(string));

            foreach (var entry in collection)
            {
                var values = new object[]
                {
                    entry.InstanceName,
                    entry.ProviderId,
                    entry.ProviderName,
                    entry.EventId,
                    entry.EventKeywords,
                    entry.Level,
                    entry.Opcode,
                    entry.Task,
                    entry.Timestamp.UtcDateTime,
                    entry.Version,
                    (object)entry.FormattedMessage ?? DBNull.Value,
                    (object)entry.Payload ?? DBNull.Value
                };

                table.Rows.Add(values);
            }

            return table;
        }

        private async Task<int> PublishEventsAsync(IList<EventRecord> collection)
        {
            var token = this.cancellationTokenSource.Token;
            var table = GetDataTable(this.instanceName, collection);

            // Prevent the connection from getting enlisted in an ambient transaction
            using (var connection = this.dbProviderFactory.CreateConnection())
            {
                connection.ConnectionString = this.connectionString;

                try
                {
                    Task openTask;
                    using (new TransactionScope(TransactionScopeOption.Suppress))
                    {
                        // Opt-out of using ambient transactions while opening the connection.
                        // Disposing the transaction scope needs to happen in the same thread where it was created,
                        // and that is why the await is done after the using finishes.
                        openTask = connection.OpenAsync(token);
                    }

                    await openTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return 0;
                }
                catch (Exception)
                {
                    //SemanticLoggingEventSource.Log.DatabaseSinkOpenFailed(dbe.ToString());
                    return 0;
                }

                try
                {
                    if (token.IsCancellationRequested)
                    {
                        return 0;
                    }

                    using (var adapter = this.dbProviderFactory.CreateDataAdapter())
                    {
                        adapter.InsertCommand = this.CreateCommand(connection);
                        adapter.UpdateBatchSize = 2;  //TODO: review
                        adapter.Update(table);
                    }
                }
                catch (OperationCanceledException)
                {
                    return 0;
                }
                finally
                {
                    connection.Close();
                }
            }

            return collection.Count;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "Reviewed")]
        private DbCommand CreateCommand(DbConnection connection)
        {
            var cmd = this.dbProviderFactory.CreateCommand();
            cmd.Connection = connection;
            cmd.CommandText = string.Format(CultureInfo.InvariantCulture, InsertSql, this.tableName);
            cmd.UpdatedRowSource = UpdateRowSource.None;

            cmd.Parameters.Add(this.CreateParameter("@InstanceName", DbType.String, "InstanceName"));
            cmd.Parameters.Add(this.CreateParameter("@ProviderId", DbType.Guid, "ProviderId"));
            cmd.Parameters.Add(this.CreateParameter("@ProviderName", DbType.String, "ProviderName"));
            cmd.Parameters.Add(this.CreateParameter("@EventId", DbType.Int32, "EventId"));
            cmd.Parameters.Add(this.CreateParameter("@EventKeywords", DbType.Int64, "EventKeywords"));
            cmd.Parameters.Add(this.CreateParameter("@Level", DbType.Int32, "Level"));
            cmd.Parameters.Add(this.CreateParameter("@Opcode", DbType.Int32, "Opcode"));
            cmd.Parameters.Add(this.CreateParameter("@Task", DbType.Int32, "Task"));
            cmd.Parameters.Add(this.CreateParameter("@Timestamp", DbType.DateTime, "Timestamp"));
            cmd.Parameters.Add(this.CreateParameter("@Version", DbType.Int32, "Version"));
            cmd.Parameters.Add(this.CreateParameter("@FormattedMessage", DbType.String, "FormattedMessage"));
            cmd.Parameters.Add(this.CreateParameter("@Payload", DbType.String, "Payload"));

            return cmd;
        }

        private const string InsertSql = @"INSERT INTO {0} ([InstanceName], [ProviderId], [ProviderName], [EventId], [EventKeywords], [Level], [Opcode], [Task], [Timestamp], [Version], [FormattedMessage], [Payload]) VALUES (@InstanceName, @ProviderId, @ProviderName, @EventId, @EventKeywords, @Level, @Opcode, @Task, @Timestamp, @Version, @FormattedMessage, @Payload)";
        private bool disposed;

        private DbParameter CreateParameter(string parameterName, DbType dbType, string sourceColumn)
        {
            var param = this.dbProviderFactory.CreateParameter();
            param.ParameterName = parameterName;
            param.DbType = dbType;
            param.SourceColumn = sourceColumn;

            return param;
        }
    }
}
