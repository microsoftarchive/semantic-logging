// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Utility;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Sinks
{
    /// <summary>
    /// Sink that asynchronously writes entries to a ElasticSearch server.
    /// </summary>
    public class ElasticSearchSink : IObserver<JsonEventEntry>, IDisposable
    {
        private const int BufferCountTrigger = 100;

        private const string BulkServiceOperationPath = "/_bulk";

        private readonly BufferedEventPublisher<JsonEventEntry> bufferedPublisher;
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        private readonly string index;
        private readonly string type;
        private readonly string instanceName;

        private readonly Uri elasticSearchUrl;
        private readonly TimeSpan onCompletedTimeout;


        /// <summary>
        /// Initializes a new instance of the <see cref="ElasticSearchSink"/> class with the specified connection string and table address.
        /// </summary>
        /// <param name="instanceName">The name of the instance originating the entries.</param>
        /// <param name="connectionString">The connection string for the storage account.</param>
        /// <param name="index">Index name prefix the default is logstash</param>
        /// <param name="type">ElasticSearch entry type, the default is etw</param>
        /// <param name="bufferInterval">The buffering interval to wait for events to accumulate before sending them to Windows Azure Storage.</param>
        /// <param name="maxBufferSize">The maximum number of entries that can be buffered while it's sending to Windows Azure Storage before the sink starts dropping entries.</param>
        /// <param name="onCompletedTimeout">Defines a timeout interval for when flushing the entries after an <see cref="OnCompleted"/> call is received and before disposing the sink.
        /// This means that if the timeout period elapses, some event entries will be dropped and not sent to the store. Normally, calling <see cref="IDisposable.Dispose"/> on 
        /// the <see cref="System.Diagnostics.Tracing.EventListener"/> will block until all the entries are flushed or the interval elapses.
        /// If <see langword="null"/> is specified, then the call will block indefinitely until the flush operation finishes.</param>
        public ElasticSearchSink(string instanceName, string connectionString, string index, string type, TimeSpan bufferInterval,
            int maxBufferSize, TimeSpan onCompletedTimeout)
        {
            Guard.ArgumentNotNullOrEmpty(instanceName, "instanceName");
            Guard.ArgumentNotNullOrEmpty(connectionString, "connectionString");
            Guard.ArgumentNotNullOrEmpty(index, "index");
            Guard.ArgumentNotNullOrEmpty(type, "type");
            Guard.ArgumentIsValidTimeout(onCompletedTimeout, "onCompletedTimeout");

            this.onCompletedTimeout = onCompletedTimeout;

            this.instanceName = instanceName;
            this.elasticSearchUrl = new Uri(new Uri(connectionString), BulkServiceOperationPath);
            this.index = index;
            this.type = type;
            var sinkId = string.Format(CultureInfo.InvariantCulture, "ElasticSearchSink ({0})", instanceName);
            bufferedPublisher = new BufferedEventPublisher<JsonEventEntry>(sinkId, PublishEventsAsync, bufferInterval,
                BufferCountTrigger, maxBufferSize, cancellationTokenSource.Token);
        }

        /// <summary>
        /// Releases all resources used by the current instance of the <see cref="ElasticSearchSink"/> class.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Notifies the observer that the provider has finished sending push-based notifications.
        /// </summary>
        public void OnCompleted()
        {
            FlushSafe();
            Dispose();
        }

        /// <summary>
        /// Provides the sink with new data to write.
        /// </summary>
        /// <param name="value">The current entry to write to Windows Azure.</param>
        public void OnNext(JsonEventEntry value)
        {
            if (value == null)
            {
                return;
            }

            value.InstanceName = value.InstanceName ?? instanceName;

            bufferedPublisher.TryPost(value);
        }

        /// <summary>
        /// Notifies the observer that the provider has experienced an error condition.
        /// </summary>
        /// <param name="error">An object that provides additional information about the error.</param>
        public void OnError(Exception error)
        {
            FlushSafe();
            Dispose();
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="ElasticSearchSink"/> class.
        /// </summary>
        ~ElasticSearchSink()
        {
            Dispose(false);
        }


        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <param name="disposing">A value indicating whether or not the class is disposing.</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2213:DisposableFieldsShouldBeDisposed",
            MessageId = "cancellationTokenSource", Justification = "Token is canceled")]
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                cancellationTokenSource.Cancel();
                bufferedPublisher.Dispose();
            }
        }

        /// <summary>
        /// Causes the buffer to be written immediately.
        /// </summary>
        /// <returns>The Task that flushes the buffer.</returns>
        public Task FlushAsync()
        {
            return bufferedPublisher.FlushAsync();
        }

        internal async Task<int> PublishEventsAsync(IEnumerable<JsonEventEntry> collection)
        {
            var bulkMessage = new StringBuilder();

            var es = new ElasticSearchLogEntry { Type = type };
            foreach (var entry in collection)
            {
                es.Index = GetIndexName(index, entry.EventDate);
                es.LogEntry = entry;
                bulkMessage.Append(JsonConvert.SerializeObject(es));
            }

            var client = new HttpClient();
            var content = new StringContent(bulkMessage.ToString());
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            try
            {
                var response = await client.PostAsync(this.elasticSearchUrl, content, cancellationTokenSource.Token).ConfigureAwait(false);

                // Anything but a 200 response will leave the entries in the buffer and we will try again
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    return 0;
                }

                var responseString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var responseObject = JObject.Parse(responseString);

                var items = responseObject["items"] as JArray;

                // If the reponse return items collection
                if (items != null)
                {
                    // NOTE: This only works with ElasticSearch 1.0
                    // Alternatively we could query ES as part of initialization check resutls or fall back to trying <1.0 parsing
                    // We should also consider logging errors for individual entries
                    return items.Count(t => t["create"]["status"].Value<int>().Equals(201));

                    // Pre-1.0 ElasticSearch
                    // return items.Count(t => t["create"]["ok"].Value<bool>().Equals(true));
                }

                // Check ElasticSearch response for status and error - this should be rare
                JToken status = responseObject["status"];
                if (status != null && status.Value<int>() == 400)
                {
                    // Possible multiple enumeration, but this should be rare occurance
                    var messagesDiscarded = collection.Count();

                    // We are unable to write the batch of event entries
                    // I don't like discarding events but we cannot let a single marlformed event prevent others from being written
                    // We might want to consider falling back to writing entries individually here
                    SemanticLoggingEventSource.Log.ElasticSearchSinkWriteEventsFailedAndDiscardsEntries(messagesDiscarded, responseObject["error"].Value<string>());

                    return messagesDiscarded;
                }

                return 0;
            }
            catch (OperationCanceledException)
            {
                return 0;
            }
            catch (Exception ex)
            {
                // Although this is generally considered an anti-pattern this is not logged upstream and we have context
                SemanticLoggingEventSource.Log.ElasticSearchSinkWriteEventsFailed(ex.ToString());
                throw;
            }
        }

        private static string GetIndexName(string indexName, DateTime entryDateTime)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}-{1:yyyy.MM.dd}", indexName, entryDateTime);
        }

        private void FlushSafe()
        {
            try
            {
                FlushAsync().Wait(onCompletedTimeout);
            }
            catch (AggregateException ex)
            {
                // Flush operation will already log errors. Never expose this exception to the observable.
                ex.Handle(e => e is FlushFailedException);
            }
        }
    }
}