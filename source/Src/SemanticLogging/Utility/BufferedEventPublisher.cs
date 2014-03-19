// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Properties;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Sinks;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Utility
{
    /// <summary>
    /// Buffering and batching utility for sinks that can benefit from batched writes
    /// </summary>
    /// <typeparam name="TEntry">The type of the entry to buffer.</typeparam>
    public sealed class BufferedEventPublisher<TEntry> : IDisposable
    {
        private static readonly TimeSpan MinimumInterval = TimeSpan.FromMilliseconds(500);
        private readonly string sinkId;
        private readonly Func<IList<TEntry>, Task<int>> eventPublisher;
        private readonly TimeSpan bufferingInterval;
        private readonly int bufferingCount;
        private readonly int maxBufferSize;
        private readonly BlockingCollection<TEntry> buffer;
        private readonly object lockObject = new object();
        private readonly int maxBatchSize;
        private readonly Lazy<Task> cachedCompletedTask = new Lazy<Task>(() =>
        {
            var tcs = new TaskCompletionSource<bool>();
            tcs.SetResult(true);
            return tcs.Task;
        });

        private ExponentialBackoff nonTransientErrorWait = new ExponentialBackoff(TimeSpan.FromSeconds(45), TimeSpan.FromHours(1), TimeSpan.FromSeconds(30));
        private bool autoFlushByCountDisabled;
        private volatile bool disposed;
        private CancellationTokenSource flushTokenSource = new CancellationTokenSource();
        private CancellationTokenSource cancellationTokenSource;
        private TaskCompletionSource<bool> flushCompletionSource;
        private int isBufferFull;

        /// <summary>
        /// Initializes a new instance of the <see cref="BufferedEventPublisher{TEntry}" /> class.
        /// </summary>
        /// <param name="sinkId">An identifier for the sink.</param>
        /// <param name="eventPublisher">The event publisher.</param>
        /// <param name="bufferingInterval">The buffering interval.</param>
        /// <param name="bufferingCount">The buffering count.</param>
        /// <param name="maxBufferSize">The maximum number of entries that can be buffered before the sink starts dropping entries.</param>
        /// <param name="cancellationToken">Cancels any pending operation.</param>
        /// <exception cref="System.ArgumentOutOfRangeException">BufferingCount out of range.</exception>
        /// <exception cref="System.ArgumentException">Argument valdation error.</exception>
        private BufferedEventPublisher(string sinkId, Func<IList<TEntry>, Task<int>> eventPublisher, TimeSpan bufferingInterval, int bufferingCount, int maxBufferSize, CancellationToken cancellationToken)
        {
            Guard.ArgumentNotNullOrEmpty(sinkId, "sinkId");
            Guard.ArgumentNotNull(eventPublisher, "eventPublisherAction");
            Guard.ArgumentGreaterOrEqualThan(500, maxBufferSize, "maxBufferSize");
            Guard.ArgumentNotNull(cancellationToken, "cancellationToken");
            Guard.ArgumentGreaterOrEqualThan(0, bufferingCount, "bufferingCount");
            Guard.ArgumentIsValidTimeout(bufferingInterval, "bufferingInterval");

            if (maxBufferSize < (bufferingCount * 3) && bufferingCount != int.MaxValue)
            {
                throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Resources.MaxBufferSizeShouldBeLargerThanBufferingCount, maxBufferSize, bufferingCount), "maxBufferSize");
            }

            // throw if not auto flush parameter was supplied
            if (bufferingInterval == Timeout.InfiniteTimeSpan && bufferingCount == 0)
            {
                throw new ArgumentException(Resources.InvalidBufferingArguments);
            }

            this.sinkId = sinkId;
            this.eventPublisher = eventPublisher;
            this.bufferingCount = bufferingCount;
            //// set minimal interval if less than default value (MinimumInterval).
            this.bufferingInterval = (bufferingInterval < MinimumInterval && bufferingInterval != Timeout.InfiniteTimeSpan) ? MinimumInterval : bufferingInterval;
            this.maxBufferSize = maxBufferSize;
            this.maxBatchSize = this.bufferingCount == 0 ? this.maxBufferSize : this.bufferingCount;
            this.cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(new[] { cancellationToken });

            this.buffer = new BlockingCollection<TEntry>(this.maxBufferSize);
        }

        /// <summary>
        /// Initializes and starts a new instance of the <see cref="BufferedEventPublisher{TEntry}" /> class.
        /// </summary>
        /// <param name="sinkId">An identifier for the sink.</param>
        /// <param name="eventPublisher">The event publisher.</param>
        /// <param name="bufferingInterval">The buffering interval.</param>
        /// <param name="bufferingCount">The buffering count.</param>
        /// <param name="maxBufferSize">The maximum number of entries that can be buffered before the sink starts dropping entries.</param>
        /// <param name="cancellationToken">Cancels any pending operation.</param>
        /// <exception cref="System.ArgumentOutOfRangeException">BufferingCount out of range.</exception>
        /// <exception cref="System.ArgumentException">Argument valdation error.</exception>
        /// <returns>An instance of BufferedEventPublisher{TEntry}.</returns>
        public static BufferedEventPublisher<TEntry> CreateAndStart(string sinkId, Func<IList<TEntry>, Task<int>> eventPublisher, TimeSpan bufferingInterval, int bufferingCount, int maxBufferSize, CancellationToken cancellationToken)
        {
            var publisher = new BufferedEventPublisher<TEntry>(sinkId, eventPublisher, bufferingInterval, bufferingCount, maxBufferSize, cancellationToken);
            publisher.StartBackgroundTask();
            return publisher;
        }

        /// <summary>
        /// Tries posting the specified entry.
        /// </summary>
        /// <param name="entry">The entry.</param>
        /// <returns>True on successful post, false otherwise.</returns>
        public bool TryPost(TEntry entry)
        {
            var bufferInstance = this.buffer;
            if (bufferInstance.TryAdd(entry))
            {
                if (!this.autoFlushByCountDisabled && this.bufferingCount > 0 && bufferInstance.Count >= this.bufferingCount)
                {
                    this.TriggerFlush();
                }

                return true;
            }
            else
            {
                if (Interlocked.Exchange(ref this.isBufferFull, 1) == 0)
                {
                    // log only if we are switching from normal buffering state
                    SemanticLoggingEventSource.Log.BufferedEventPublisherCapacityOverloaded(bufferInstance.BoundedCapacity, this.sinkId);
                }
            }

            return false;
        }

        /// <summary>
        /// Flushes all the buffered entries.
        /// </summary>
        /// <returns>The task to wait for flush completion.</returns>
        public Task FlushAsync()
        {
            // if there are no entries in the buffer, just return a completed operation
            if (this.buffer.Count == 0)
            {
                return this.cachedCompletedTask.Value;
            }

            // create an awaitable task to know when the Flush operation finished.
            var completionSource = this.flushCompletionSource;
            if (completionSource == null)
            {
                lock (this.lockObject)
                {
                    if (this.flushCompletionSource == null)
                    {
                        this.flushCompletionSource = new TaskCompletionSource<bool>();
                    }

                    completionSource = this.flushCompletionSource;
                }
            }

            this.TriggerFlush();

            return completionSource.Task;
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            if (!this.disposed)
            {
                this.disposed = true;

                this.cancellationTokenSource.Cancel();
                this.flushTokenSource.Cancel();
                this.NotifyOnBufferNotEmpty();
            }
        }

        private void StartBackgroundTask()
        {
            Task.Factory.StartNew((Func<Task>)this.TransferEntries, CancellationToken.None, TaskCreationOptions.HideScheduler, TaskScheduler.Default)
                .Unwrap()
                .ContinueWith(this.RestartBackgroundTask);
        }

        // Logs any unobserved exception and restarts the background task.
        private void RestartBackgroundTask(Task predecesor)
        {
            if (predecesor.Exception != null)
            {
                SemanticLoggingEventSource.Log.BufferedEventPublisherUnobservedTaskFault(this.sinkId, predecesor.Exception.ToString());
            }

            if (!this.cancellationTokenSource.IsCancellationRequested)
            {
                // Restart background task
                this.StartBackgroundTask();
            }
        }

        //// Main processing loop for publishing entries based on time interval, buffer count and explicit flush triggers.
        private async Task TransferEntries()
        {
            var token = this.cancellationTokenSource.Token;

            var bufferInstance = this.buffer;
            while (true)
            {
                if (token.IsCancellationRequested)
                {
                    // if cancelled, break the processing loop.
                    this.SetAsFlushed();
                    return;
                }

                var count = bufferInstance.Count;
                if (count == 0 || count < this.bufferingCount)
                {
                    if (count == 0)
                    {
                        // if all entries were published, mark as flushed.
                        this.SetAsFlushed();
                    }

                    if (this.flushCompletionSource == null)
                    {
                        // if no consumers triggered an explicit flush operation, then
                        // wait for the buffering interval.
                        await this.WaitForIntervalAsync(this.bufferingInterval).ConfigureAwait(false);

                        if (token.IsCancellationRequested || bufferInstance.Count == 0)
                        {
                            // if there are no new entries yet, restart the loop that will
                            // normally cause a new waiting interval.
                            continue;
                        }
                    }
                }

                var entries = this.GetBatch();

                int entriesSent = 0;
                try
                {
                    entriesSent = await this.eventPublisher(entries).ConfigureAwait(false);
                    if (entriesSent > 0)
                    {
                        // if entries were successfully published, remove them from the queue.
                        for (int i = 0; i < entriesSent; i++)
                        {
                            var deleted = bufferInstance.Take();

                            // System.Diagnostics.Debug.Assert(typeof(TEntry).IsValueType ? object.Equals(entries[i], deleted) : object.ReferenceEquals(entries[i], deleted), "Elements in the queue were removed outside of the main loop.");
                        }

                        if (Interlocked.Exchange(ref this.isBufferFull, 0) == 1)
                        {
                            // log only if we are switching from overload state
                            SemanticLoggingEventSource.Log.BufferedEventPublisherCapacityRestored(this.sinkId);
                        }

                        this.nonTransientErrorWait.Reset();
                        this.autoFlushByCountDisabled = false;
                    }
                }
                catch (Exception ex)
                {
                    // There are non-transient errors, so return error in flush calls.
                    this.FailFlushOperation(ex);
                }

                if (entriesSent == 0 && !token.IsCancellationRequested)
                {
                    this.autoFlushByCountDisabled = true;

                    // if there are errors that are non transient, wait and retry later, but don't ever break the loop.
                    await this.WaitForIntervalAsync(this.nonTransientErrorWait.GetNextDelay());
                }
            }
        }

        /// <summary>
        /// If there are consumers waiting for the flush operation to finish, it completes the operation.
        /// </summary>
        private void SetAsFlushed()
        {
            lock (this.lockObject)
            {
                if (this.flushCompletionSource != null)
                {
                    // TODO: notify that the Flush operation was terminated, as the process is stopping.
                    this.flushCompletionSource.TrySetResult(true);
                    this.flushCompletionSource = null;
                }
            }
        }

        // If there are consumers waiting for the flush operation to finish, it returns a failed task.
        private void FailFlushOperation(Exception exception)
        {
            AggregateException aggregateException = exception as AggregateException;
            if (aggregateException != null)
            {
                exception = aggregateException.Flatten().InnerException;
            }

            lock (this.lockObject)
            {
                if (this.flushCompletionSource != null)
                {
                    this.flushCompletionSource.TrySetException(new FlushFailedException(exception));
                    this.flushCompletionSource = null;
                }
            }
        }

        private void TriggerFlush()
        {
            // notify to stop waiting for polling interval
            var tokenSource = this.flushTokenSource;
            if (!tokenSource.IsCancellationRequested)
            {
                tokenSource.Cancel();
            }
        }

        /// <summary>
        /// Waits for the polling interval or until a flush operation occurs.
        /// </summary>
        /// <param name="interval">The interval to wait.</param>
        /// <returns>Returns a task that will run completion when the time expires, and will reset the flush cancellation token.</returns>
        private async Task WaitForIntervalAsync(TimeSpan interval)
        {
            lock (this.lockObject)
            {
                if (this.flushCompletionSource != null || this.disposed)
                {
                    return;
                }

                this.flushTokenSource = new CancellationTokenSource();
            }

            // any call to flush will terminate the Delay task early.
            await
                Task.Delay(interval, this.flushTokenSource.Token)
                    .ContinueWith(IgnoreTaskCancelation, TaskContinuationOptions.ExecuteSynchronously)
                    .ConfigureAwait(false);
        }

        private static void IgnoreTaskCancelation(Task task)
        {
        }

        private IList<TEntry> GetBatch()
        {
            return Enumerable.Take(this.buffer, this.maxBatchSize).ToList();
        }

        private void NotifyOnBufferNotEmpty()
        {
            if (this.buffer.Count > 0)
            {
                SemanticLoggingEventSource.Log.BufferedEventPublisherEventsLostWhileDisposing(this.buffer.Count, this.sinkId);
            }
        }

        private class ExponentialBackoff
        {
            private readonly double minBackoffMilliseconds;
            private readonly double maxBackoffMilliseconds;
            private readonly double deltaBackoffMilliseconds;

            private int currentPower;

            public ExponentialBackoff(TimeSpan minBackoff, TimeSpan maxBackoff, TimeSpan deltaBackoff)
            {
                Guard.ArgumentGreaterOrEqualThan(0, minBackoff.TotalMilliseconds, "minBackoff");
                Guard.ArgumentGreaterOrEqualThan(0, maxBackoff.TotalMilliseconds, "maxBackoff");
                Guard.ArgumentGreaterOrEqualThan(0, deltaBackoff.TotalMilliseconds, "deltaBackoff");
                Guard.ArgumentGreaterOrEqualThan(minBackoff.TotalMilliseconds, maxBackoff.TotalMilliseconds, "maxBackoff");

                this.minBackoffMilliseconds = minBackoff.TotalMilliseconds;
                this.maxBackoffMilliseconds = maxBackoff.TotalMilliseconds;
                this.deltaBackoffMilliseconds = deltaBackoff.TotalMilliseconds;
            }

            public TimeSpan GetNextDelay()
            {
                var random = new Random();

                int delta = (int)((Math.Pow(2.0, this.currentPower) - 1.0) * random.Next((int)(this.deltaBackoffMilliseconds * 0.8), (int)(this.deltaBackoffMilliseconds * 1.2)));
                int interval = (int)Math.Min(checked(this.minBackoffMilliseconds + delta), this.maxBackoffMilliseconds);

                if (interval < this.maxBackoffMilliseconds)
                {
                    this.currentPower++;
                }

                return TimeSpan.FromMilliseconds(interval);
            }

            public void Reset()
            {
                this.currentPower = 0;
            }
        }
    }
}