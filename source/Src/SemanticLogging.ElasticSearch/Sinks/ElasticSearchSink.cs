// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Properties;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Utility;

using Newtonsoft.Json.Linq;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Sinks
{
    /// <summary>
    /// Sink that asynchronously writes entries to a ElasticSearch server.
    /// </summary>
    public class ElasticSearchSink : IObserver<JsonEventEntry>, IDisposable
    {

        private const string BulkServiceOperationPath = "/_bulk";

        private readonly BufferedEventPublisher<JsonEventEntry> bufferedPublisher;
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        private readonly string index;
        private readonly string type;
        private readonly string instanceName;

        private readonly bool flattenPayload;

        private readonly Uri elasticSearchUrl;
        private readonly TimeSpan onCompletedTimeout;

        /// <summary>
        /// Initializes a new instance of the <see cref="ElasticSearchSink"/> class with the specified connection string and table address.
        /// </summary>
        /// <param name="instanceName">The name of the instance originating the entries.</param>
        /// <param name="connectionString">The connection string for the storage account.</param>
        /// <param name="index">Index name prefix formatted as index-{0:yyyy.MM.DD}</param>
        /// <param name="type">ElasticSearch entry type, the default is etw</param>
        /// <param name="flattenPayload">Flatten the payload collection when serializing event entries</param>
        /// <param name="bufferInterval">The buffering interval to wait for events to accumulate before sending them to Elasticsearch.</param>
        /// <param name="bufferingCount">The buffering event entry count to wait before sending events to Elasticsearch </param>
        /// <param name="maxBufferSize">The maximum number of entries that can be buffered while it's sending to Windows Azure Storage before the sink starts dropping entries.</param>
        /// <param name="onCompletedTimeout">Defines a timeout interval for when flushing the entries after an <see cref="OnCompleted"/> call is received and before disposing the sink.
        /// This means that if the timeout period elapses, some event entries will be dropped and not sent to the store. Normally, calling <see cref="IDisposable.Dispose"/> on 
        /// the <see cref="System.Diagnostics.Tracing.EventListener"/> will block until all the entries are flushed or the interval elapses.
        /// If <see langword="null"/> is specified, then the call will block indefinitely until the flush operation finishes.</param>
        public ElasticSearchSink(string instanceName, string connectionString, string index, string type, bool? flattenPayload, TimeSpan bufferInterval,
            int bufferingCount, int maxBufferSize, TimeSpan onCompletedTimeout)
        {
            Guard.ArgumentNotNullOrEmpty(instanceName, "instanceName");
            Guard.ArgumentNotNullOrEmpty(connectionString, "connectionString");
            Guard.ArgumentNotNullOrEmpty(index, "index");
            Guard.ArgumentNotNullOrEmpty(type, "type");
            Guard.ArgumentIsValidTimeout(onCompletedTimeout, "onCompletedTimeout");
            Guard.ArgumentGreaterOrEqualThan(0, bufferingCount, "bufferingCount");

            if (Regex.IsMatch(index, "[\\\\/*?\",<>|\\sA-Z]"))
            {
                throw new ArgumentException(Resource.InvalidElasticSearchIndexNameError, "index");
            }

            this.onCompletedTimeout = onCompletedTimeout;

            this.instanceName = instanceName;
            this.flattenPayload = flattenPayload ?? true;
            this.elasticSearchUrl = new Uri(new Uri(connectionString), BulkServiceOperationPath);
            this.index = index;
            this.type = type;
            var sinkId = string.Format(CultureInfo.InvariantCulture, "ElasticSearchSink ({0})", instanceName);
            bufferedPublisher = new BufferedEventPublisher<JsonEventEntry>(sinkId, PublishEventsAsync, bufferInterval,
                bufferingCount, maxBufferSize, cancellationTokenSource.Token);
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
            HttpClient client = null;

            try
            {
                client = new HttpClient();

                var serializer = new ElasticSearchEventEntrySerializer(this.index, this.type, this.flattenPayload);

                string logMessages = serializer.Serialize(collection);

                var content = new StringContent(logMessages);
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                var response = await client.PostAsync(this.elasticSearchUrl, content, cancellationTokenSource.Token).ConfigureAwait(false);

                // If there is an exception
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    // Check the response for 400 bad request
                    if (response.StatusCode == HttpStatusCode.BadRequest)
                    {
                        // Possible multiple enumeration, but this should be an extremely rare occurrance
                        var messagesDiscarded = collection.Count();

                        var errorContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                        string serverErrorMessage;

                        // Try to parse the exception message
                        try
                        {
                            var errorObject = JObject.Parse(errorContent);
                            serverErrorMessage = errorObject["error"].Value<string>();
                        }
                        catch (Exception)
                        {
                            // If for some reason we cannot extract the server error message log the entire response
                            serverErrorMessage = errorContent;
                        }

                        // We are unable to write the batch of event entries - Possible poison message
                        // I don't like discarding events but we cannot let a single malformed event prevent others from being written
                        // We might want to consider falling back to writing entries individually here
                        SemanticLoggingEventSource.Log.ElasticSearchSinkWriteEventsFailedAndDiscardsEntries(messagesDiscarded, serverErrorMessage);

                        return messagesDiscarded;
                    }

                    // This will leave the messages in the buffer
                    return 0;
                }

                var responseString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var responseObject = JObject.Parse(responseString);

                var items = responseObject["items"] as JArray;

                // If the response return items collection
                if (items != null)
                {
                    // NOTE: This only works with ElasticSearch 1.0
                    // Alternatively we could query ES as part of initialization check results or fall back to trying <1.0 parsing
                    // We should also consider logging errors for individual entries
                    return items.Count(t => t["create"]["status"].Value<int>().Equals(201));

                    // Pre-1.0 ElasticSearch
                    // return items.Count(t => t["create"]["ok"].Value<bool>().Equals(true));
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
            finally
            {
                if (client != null)
                {
                    client.Dispose();
                }
            }
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