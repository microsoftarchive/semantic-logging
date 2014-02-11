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
    public class ElasticSearchSink : IObserver<JsonEventEntry>, IDisposable
    {
        private const int BufferCountTrigger = 100;
        private readonly BufferedEventPublisher<JsonEventEntry> bufferedPublisher;
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        private readonly string hostName;
        private readonly string instanceName;
        private readonly TimeSpan onCompletedTimeout;
        private readonly int portNumber;


        /// <summary>
        /// Initializes a new instance of the <see cref="WindowsAzureTableSink"/> class with the specified connection string and table address.
        /// </summary>
        /// <param name="instanceName">The name of the instance originating the entries.</param>
        /// <param name="hostName">The connection string for the storage account.</param>
        /// <param name="portNumber">Default 9200, specify...</param>
        /// <param name="bufferInterval">The buffering interval to wait for events to accumulate before sending them to Windows Azure Storage.</param>
        /// <param name="maxBufferSize">The maximum number of entries that can be buffered while it's sending to Windows Azure Storage before the sink starts dropping entries.</param>
        /// <param name="onCompletedTimeout">Defines a timeout interval for when flushing the entries after an <see cref="OnCompleted"/> call is received and before disposing the sink.
        /// This means that if the timeout period elapses, some event entries will be dropped and not sent to the store. Normally, calling <see cref="IDisposable.Dispose"/> on 
        /// the <see cref="System.Diagnostics.Tracing.EventListener"/> will block until all the entries are flushed or the interval elapses.
        /// If <see langword="null"/> is specified, then the call will block indefinitely until the flush operation finishes.</param>
        public ElasticSearchSink(string instanceName, string hostName, int portNumber, TimeSpan bufferInterval,
            int maxBufferSize, TimeSpan onCompletedTimeout)
        {
            Guard.ArgumentNotNullOrEmpty(instanceName, "instanceName");
            Guard.ArgumentNotNullOrEmpty(hostName, "hostName");
            Guard.ArgumentIsValidTimeout(onCompletedTimeout, "onCompletedTimeout");

            this.onCompletedTimeout = onCompletedTimeout;

            this.instanceName = instanceName;
            this.hostName = hostName;
            this.portNumber = portNumber;
            var sinkId = string.Format(CultureInfo.InvariantCulture, "ElasticSearchSink ({0})", instanceName);
            bufferedPublisher = new BufferedEventPublisher<JsonEventEntry>(sinkId, PublishEventsAsync, bufferInterval,
                BufferCountTrigger, maxBufferSize, cancellationTokenSource.Token);
        }

        /// <summary>
        /// Releases all resources used by the current instance of the <see cref="WindowsAzureTableSink"/> class.
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

        public void OnNext(JsonEventEntry value)
        {
            if (value == null)
            {
                return;
            }

            value.InstanceName = value.InstanceName ?? instanceName;

            bufferedPublisher.TryPost(value);
        }

        public void OnError(Exception error)
        {
            FlushSafe();
            Dispose();
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="WindowsAzureTableSink"/> class.
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
            MessageId = "cancellationTokenSource", Justification = "Token is cancelled")]
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

        internal async Task<int> PublishEventsAsync(IList<JsonEventEntry> collection)
        {
            var bulkMessage = new StringBuilder();

            var es = new ElasticSearchLogEntry {Index = "ind", Type = "slab"};
            foreach (var entry in collection)
            {
                es.LogEntry = entry;
                bulkMessage.Append(JsonConvert.SerializeObject(es));
            }

            var client = new HttpClient();
            var uri = new Uri(String.Format("http://{0}:{1}/_bulk", hostName, portNumber));
            var content = new StringContent(bulkMessage.ToString());
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            try
            {
                var response = await client.PostAsync(uri, content, cancellationTokenSource.Token).ConfigureAwait(false);
                if (response.StatusCode != HttpStatusCode.OK) return 0;

                var responseString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var responseObject = JObject.Parse(responseString);
                var items = responseObject["items"] as JArray;

                return items != null
                    ? items.Count(t => t["create"]["ok"].Value<bool>().Equals(true))
                    : 0;
            }
            catch (OperationCanceledException)
            {
                return 0;
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