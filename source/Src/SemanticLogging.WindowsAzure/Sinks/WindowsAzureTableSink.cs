// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Properties;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Sinks.WindowsAzure;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Utility;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.RetryPolicies;
using Microsoft.WindowsAzure.Storage.Table;
using Guard = Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Utility.Guard;
using WindowsAzureResources = Microsoft.Practices.EnterpriseLibrary.SemanticLogging.WindowsAzure.Properties.Resources;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Sinks
{
    /// <summary>
    /// Sink that asynchronously writes entries to an Azure table.
    /// </summary>
    public class WindowsAzureTableSink : IObserver<EventEntry>, IDisposable
    {
        private const int BufferCountTrigger = 100;
        private static readonly char[] DisallowedCharsInPartitionAndRowKeys = new[] { '/', '\\', '#', '@' };

        private static int salt = 0;

        private readonly string instanceName;
        private readonly CloudTableClient client;
        private readonly CloudTable table;
        private readonly TimeSpan onCompletedTimeout;
        private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private bool sortKeysAscending;
        private BufferedEventPublisher<CloudEventEntry> bufferedPublisher;

        /// <summary>
        /// Initializes a new instance of the <see cref="WindowsAzureTableSink"/> class with the specified connection string and table address.
        /// </summary>
        /// <param name="instanceName">The name of the instance originating the entries.</param>
        /// <param name="connectionString">The connection string for the storage account.</param>
        /// <param name="tableAddress">Either the name of the table, or the absolute URI to the table.</param>
        /// <param name="bufferInterval">The buffering interval to wait for events to accumulate before sending them to Azure Storage.</param>
        /// <param name="maxBufferSize">The maximum number of entries that can be buffered while it's sending to Azure Storage before the sink starts dropping entries.</param>
        /// <param name="onCompletedTimeout">Defines a timeout interval for when flushing the entries after an <see cref="OnCompleted"/> call is received and before disposing the sink.
        /// This means that if the timeout period elapses, some event entries will be dropped and not sent to the store. Normally, calling <see cref="IDisposable.Dispose"/> on 
        /// the <see cref="System.Diagnostics.Tracing.EventListener"/> will block until all the entries are flushed or the interval elapses.
        /// If <see langword="null"/> is specified, then the call will block indefinitely until the flush operation finishes.</param>
        public WindowsAzureTableSink(string instanceName, string connectionString, string tableAddress, TimeSpan bufferInterval, int maxBufferSize, TimeSpan onCompletedTimeout)
        {
            Guard.ArgumentNotNullOrEmpty(instanceName, "instanceName");
            Guard.ArgumentNotNullOrEmpty(connectionString, "connectionString");
            Guard.ArgumentNotNullOrEmpty(tableAddress, "tableAddress");
            Guard.ArgumentIsValidTimeout(onCompletedTimeout, "onCompletedTimeout");

            this.onCompletedTimeout = onCompletedTimeout;

            CloudStorageAccount account = GetStorageAccount(connectionString);

            if (!IsValidTableName(tableAddress))
            {
                throw new ArgumentException(WindowsAzureResources.InvalidTableName, "tableAddress");
            }

            this.instanceName = NormalizeInstanceName(instanceName);
            this.client = account.CreateCloudTableClient();
            this.client.RetryPolicy = new ExponentialRetry(TimeSpan.FromSeconds(5), 7);
            this.table = this.client.GetTableReference(tableAddress);
            string sinkId = string.Format(CultureInfo.InvariantCulture, "WindowsAzureTableSink ({0})", instanceName);
            this.bufferedPublisher = BufferedEventPublisher<CloudEventEntry>.CreateAndStart(sinkId, this.PublishEventsAsync, bufferInterval, BufferCountTrigger, maxBufferSize, this.cancellationTokenSource.Token);
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="WindowsAzureTableSink"/> class.
        /// </summary>
        ~WindowsAzureTableSink()
        {
            this.Dispose(false);
        }

        /// <summary>
        /// Gets or sets a value indicating whether to sort the row keys in ascending order.
        /// </summary>
        /// <value>The value indicating whether to sort the row keys in ascending order.</value>
        public bool SortKeysAscending
        {
            get
            {
                return this.sortKeysAscending;
            }

            set
            {
                this.sortKeysAscending = value;

                if (value == true)
                {
                    salt = 0xFFFF;
                }
            }
        }

        /// <summary>
        /// Causes the buffer to be written immediately.
        /// </summary>
        /// <returns>The Task that flushes the buffer.</returns>
        public Task FlushAsync()
        {
            return this.bufferedPublisher.FlushAsync();
        }

        /// <summary>
        /// Releases all resources used by the current instance of the <see cref="WindowsAzureTableSink"/> class.
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
        /// <param name="value">The current entry to write.</param>
        public void OnNext(EventEntry value)
        {
            this.OnNext(value.TryConvertToCloudEventEntry());
        }

        internal void OnNext(CloudEventEntry value)
        {
            if (value == null)
            {
                return;
            }

            value.InstanceName = value.InstanceName != null ? NormalizeInstanceName(value.InstanceName) : this.instanceName;

            this.bufferedPublisher.TryPost(value);
        }

        internal virtual Task<IList<TableResult>> ExecuteBatchAsync(TableBatchOperation batch)
        {
            return this.table.ExecuteBatchAsync(batch, this.cancellationTokenSource.Token);
        }

        internal virtual async Task<bool> EnsureTableExistsAsync()
        {
            var token = this.cancellationTokenSource.Token;
            if (token.IsCancellationRequested)
            {
                return false;
            }

            try
            {
                await this.table.CreateIfNotExistsAsync(token).ConfigureAwait(false);
                return true;
            }
            catch (OperationCanceledException)
            {
                // This exception is never thrown as of the current version of the storage client library.
                // Keeping in case in the future this exception is thrown by the storage client.
                return false;
            }
            catch (Exception ex)
            {
                if (!IsOperationCanceled(ex as StorageException))
                {
                    SemanticLoggingEventSource.Log.WindowsAzureTableSinkTableCreationFailed(ex.ToString());
                }

                return false;
            }
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

        /// <summary>
        /// Normalizes the instance name, as it will be used as part of the row key for each of the entries.
        /// </summary>
        /// <param name="instanceName">The original desired instance name.</param>
        /// <returns>A normalized string that is limited in its length and removes reserved characters.</returns>
        private static string NormalizeInstanceName(string instanceName)
        {
            const int InstanceNameMaxLength = 255;

            if (instanceName.Length > InstanceNameMaxLength)
            {
                instanceName = instanceName.Substring(0, InstanceNameMaxLength);
            }

            foreach (var disallowedChar in DisallowedCharsInPartitionAndRowKeys)
            {
                instanceName = instanceName.Replace(disallowedChar, '_');
            }

            return instanceName;
        }

        private static bool IsValidTableName(string tableName)
        {
            var isMatch = Regex.IsMatch(tableName, @"^[A-Za-z][A-Za-z0-9]{2,62}$", RegexOptions.Compiled);

            return isMatch;
        }

        /// <summary>
        /// Validate the Azure connection string or throws an exception.
        /// </summary>
        /// <param name="connectionString">The connection string value.</param>
        /// <exception cref="ArgumentNullException">Null or empty connection string.</exception>
        /// <exception cref="ArgumentException">Invalid connection string.</exception>
        /// <returns>The <see cref="Microsoft.WindowsAzure.Storage.CloudStorageAccount"/>.</returns>
        private static CloudStorageAccount GetStorageAccount(string connectionString)
        {
            Guard.ArgumentNotNullOrEmpty(connectionString, "connectionString");

            try
            {
                return CloudStorageAccount.Parse(connectionString);
            }
            catch (FormatException e)
            {
                throw new ArgumentException(Resources.InvalidConnectionStringError, "connectionString", e);
            }
        }

        private static TableBatchOperation GetBatchOperation(IEnumerable<CloudEventEntry> entries)
        {
            const int MaxBatchSizeInBytes = 4000000;

            var batchOperation = new TableBatchOperation();

            int approximateBatchSize = 0;
            foreach (var item in entries)
            {
                var entity = item.CreateTableEntity();

                approximateBatchSize += GetApproximateEntitySize(entity);

                if (approximateBatchSize >= MaxBatchSizeInBytes)
                {
                    break;
                }

                batchOperation.InsertOrReplace(entity);
            }

            return batchOperation;
        }

        private static int GetApproximateEntitySize(DynamicTableEntity entity)
        {
            // rough estimate of entry size without accounting for payload
            int approximateEntitySize = entity.Properties.Count * 300;

            // Approximate payload size. It is very conservative, as the size is not accurate and it's
            // better to send batches with fewer items instead of getting exceptions due to size.
            if (entity.Properties.ContainsKey("Payload"))
            {
                // Payload arguments are serialized twice as UTF-16: as JSON and also there are individual columns.
                approximateEntitySize += entity.Properties["Payload"].StringValue.Length * 4;
            }

            // Approximate formatted message size.
            if (entity.Properties.ContainsKey("Message"))
            {
                // Payload arguments are serialized as UTF-16.
                approximateEntitySize += entity.Properties["Message"].StringValue.Length * 2;
            }

            return approximateEntitySize;
        }

        private static bool IsOperationCanceled(StorageException ex)
        {
            // Code used by the storage client library to identify cancellations.
            const int OperationCanceledStatusCode = 306;

            return ex != null && ex.RequestInformation != null && ex.RequestInformation.HttpStatusCode == OperationCanceledStatusCode;
        }

        private List<CloudEventEntry> FilterBatch(IList<CloudEventEntry> collection)
        {
            string partitionKey = null;
            var entries = new List<CloudEventEntry>();
            int index = 0;

            this.UpdateSalt();

            foreach (var entry in collection)
            {
                if (entry.PartitionKey == null)
                {
                    entry.CreateKey(this.SortKeysAscending, salt);
                }

                if (partitionKey == null)
                {
                    partitionKey = entry.PartitionKey;
                }

                if (entry.PartitionKey == partitionKey)
                {
                    entries.Add(entry);
                }
                else
                {
                    break;
                }

                // a batch cannot contain more than 100 items
                if (++index >= 100)
                {
                    break;
                }
            }

            foreach (var group in entries.GroupBy(x => x.RowKey))
            {
                var items = group.ToList();
                if (items.Count > 1)
                {
                    foreach (var item in items)
                    {
                        this.UpdateSalt();

                        // if there are more than 1 item in the group, regenerate their keys to make them unique.
                        item.CreateKey(this.sortKeysAscending, salt);
                    }
                }
            }

            return entries;
        }

        private void UpdateSalt()
        {
            if (this.sortKeysAscending)
            {
                salt++;
            }
            else
            {
                salt--;
            }
        }

        private async Task<IList<TableResult>> ExecuteBatchSafeAsync(TableBatchOperation batch)
        {
            try
            {
                return await this.ExecuteBatchAsync(batch).ConfigureAwait(false);
            }
            catch (StorageException ex)
            {
                if (ex.RequestInformation == null
                    || ex.RequestInformation.HttpStatusCode != 404 // Not Found
                    || ex.RequestInformation.ExtendedErrorInformation == null
                    || ex.RequestInformation.ExtendedErrorInformation.ErrorCode != "TableNotFound")
                {
                    throw;
                }
            }

            if (await this.EnsureTableExistsAsync().ConfigureAwait(false))
            {
                return await this.ExecuteBatchAsync(batch).ConfigureAwait(false);
            }

            return null;
        }

        private async Task<int> PublishEventsAsync(IList<CloudEventEntry> collection)
        {
            var entries = this.FilterBatch(collection);
            var batchOperation = GetBatchOperation(entries);

            try
            {
                var result = await this.ExecuteBatchSafeAsync(batchOperation).ConfigureAwait(false);
                return result != null ? batchOperation.Count : 0;
            }
            catch (StorageException ex)
            {
                if (IsOperationCanceled(ex))
                {
                    return 0;
                }

                if (ex.RequestInformation != null && ex.RequestInformation.HttpStatusCode == 400)
                {
                    // Bad request
                    SemanticLoggingEventSource.Log.WindowsAzureTableSinkPublishEventsFailedAndDiscardsEntries(batchOperation.Count);
                    return batchOperation.Count;
                }

                SemanticLoggingEventSource.Log.WindowsAzureTableSinkPublishEventsFailed(ex.ToString());
                throw;
            }
            catch (OperationCanceledException)
            {
                return 0;
            }
            catch (Exception ex)
            {
                SemanticLoggingEventSource.Log.WindowsAzureTableSinkPublishEventsFailed(ex.ToString());
                throw;
            }
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
