// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Formatters;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Utility;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Sinks
{
    /// <summary>
    /// A sink that writes to a flat file with a rolling overwrite behavior. 
    /// </summary>
    /// <remarks>This class is thread-safe.</remarks>
    public partial class RollingFlatFileSink : IObserver<EventEntry>, IDisposable
    {
        private readonly FileInfo file;
        private readonly IEventTextFormatter formatter;

        private readonly StreamWriterRollingHelper rollingHelper;
        private readonly RollFileExistsBehavior rollFileExistsBehavior;
        private readonly RollInterval rollInterval;
        private readonly long rollSizeInBytes;
        private readonly string timestampPattern;
        private readonly int maxArchivedFiles;
        private readonly Timer timer;
        private readonly object lockObject = new object();
        private readonly object flushLockObject = new object();

        private readonly bool isAsync;
        private BlockingCollection<EventEntry> pendingEntries;
        private volatile TaskCompletionSource<bool> flushSource = new TaskCompletionSource<bool>();
        private CancellationTokenSource cancellationTokenSource;
        private Task asyncProcessorTask;

        private TallyKeepingFileStreamWriter writer;
        private bool disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="RollingFlatFileSink"/> class with the specified values.
        /// </summary>
        /// <param name="fileName">The filename where the entries will be logged.</param>
        /// <param name="rollSizeKB">The maximum file size (KB) before rolling.</param>
        /// <param name="timestampPattern">The date format that will be appended to the new roll file.</param>
        /// <param name="rollFileExistsBehavior">Expected behavior that will be used when the roll file has to be created.</param>
        /// <param name="rollInterval">The time interval that makes the file to be rolled.</param>
        /// <param name="maxArchivedFiles">The maximum number of archived files to keep.</param>
        /// <param name="formatter">The event entry formatter.</param>
        /// <param name="isAsync">Specifies if the writing should be done asynchronously, or synchronously with a blocking call.</param>
        public RollingFlatFileSink(string fileName, int rollSizeKB, string timestampPattern, RollFileExistsBehavior rollFileExistsBehavior, RollInterval rollInterval, int maxArchivedFiles, IEventTextFormatter formatter, bool isAsync)
        {
            Guard.ArgumentNotNull(formatter, "formatter");

            this.file = FileUtil.ProcessFileNameForLogging(fileName);
            this.formatter = formatter;

            if (rollInterval == RollInterval.None)
            {
                if (!string.IsNullOrWhiteSpace(timestampPattern))
                {
                    Guard.ValidateTimestampPattern(timestampPattern, "timestampPattern");
                }
            }
            else
            {
                Guard.ValidateTimestampPattern(timestampPattern, "timestampPattern");
            }

            this.writer = new TallyKeepingFileStreamWriter(this.file.Open(FileMode.Append, FileAccess.Write, FileShare.Read));

            this.rollSizeInBytes = rollSizeKB * 1024L;
            this.timestampPattern = timestampPattern;
            this.rollFileExistsBehavior = rollFileExistsBehavior;
            this.rollInterval = rollInterval;
            this.maxArchivedFiles = maxArchivedFiles;
            this.isAsync = isAsync;

            this.rollingHelper = new StreamWriterRollingHelper(this);

            if (rollInterval == RollInterval.Midnight && !isAsync)
            {
                var now = this.rollingHelper.DateTimeProvider.CurrentDateTime;
                var midnight = now.AddDays(1).Date;

                var callback = new TimerCallback(delegate
                {
                    lock (this.lockObject)
                    {
                        this.rollingHelper.RollIfNecessary();
                    }
                });

                this.timer = new Timer(callback, null, midnight.Subtract(now), TimeSpan.FromDays(1));
            }

            this.flushSource.SetResult(true);
            if (isAsync)
            {
                this.cancellationTokenSource = new CancellationTokenSource();
                this.pendingEntries = new BlockingCollection<EventEntry>();
                this.asyncProcessorTask = Task.Factory.StartNew(this.WriteEntries, TaskCreationOptions.LongRunning);
            }
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="RollingFlatFileSink"/> class.
        /// </summary>
        ~RollingFlatFileSink()
        {
            this.Dispose(false);
        }

        /// <summary>
        /// Gets the <see cref="StreamWriterRollingHelper"/> for the flat file.
        /// </summary>
        /// <value>
        /// The <see cref="StreamWriterRollingHelper"/> for the flat file.
        /// </value>
        public StreamWriterRollingHelper RollingHelper
        {
            get { return this.rollingHelper; }
        }

        /// <summary>
        /// Gets the tally of the length of the string.
        /// </summary>
        /// <value>
        /// The tally of the length of the string.
        /// </value>
        public long Tally
        {
            get { return this.writer.Tally; }
        }

        /// <summary>
        /// Flushes the buffer content to the file.
        /// </summary>
        /// <returns>The Task that gets completed when the buffer is flushed.</returns>
        public Task FlushAsync()
        {
            lock (this.flushLockObject)
            {
                return this.flushSource.Task;
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <param name="disposing">A value indicating whether or not the class is disposing.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (!this.disposed)
                {
                    this.disposed = true;

                    using (this.timer) { }

                    lock (this.lockObject)
                    {
                        if (this.isAsync)
                        {
                            this.cancellationTokenSource.Cancel();
                            this.asyncProcessorTask.Wait();
                            this.pendingEntries.Dispose();
                            this.cancellationTokenSource.Dispose();
                        }

                        this.writer.Dispose();
                        using (this.rollingHelper) { }
                    }
                }
            }
        }

        private void OnSingleEventWritten(EventEntry entry)
        {
            var formattedEntry = entry.TryFormatAsString(this.formatter);

            if (formattedEntry != null)
            {
                try
                {
                    lock (this.lockObject)
                    {
                        this.rollingHelper.RollIfNecessary();
                        this.writer.Write(formattedEntry);
                        this.writer.Flush();
                    }
                }
                catch (Exception e)
                {
                    SemanticLoggingEventSource.Log.RollingFlatFileSinkWriteFailed(e.ToString());
                }
            }
        }

        private void WriteEntries()
        {
            EventEntry entry;
            var token = this.cancellationTokenSource.Token;

            while (!token.IsCancellationRequested)
            {
                // lock free, as it is the single writer.
                try
                {
                    if (this.pendingEntries.Count == 0 && !this.flushSource.Task.IsCompleted && !token.IsCancellationRequested)
                    {
                        this.writer.Flush();
                        this.rollingHelper.RollIfNecessary();
                        lock (this.flushLockObject)
                        {
                            if (this.pendingEntries.Count == 0 && !this.flushSource.Task.IsCompleted && !token.IsCancellationRequested)
                            {
                                this.flushSource.TrySetResult(true);
                            }
                        }
                    }

                    if (!this.pendingEntries.TryTake(out entry, this.GetNextRollTimeout(), token))
                    {
                        // continue the loop if timed out, to give a chance to roll if necessary.
                        this.rollingHelper.RollIfNecessary();
                        continue;
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }

                this.rollingHelper.RollIfNecessary();

                var formattedEntry = entry.TryFormatAsString(this.formatter);
                if (formattedEntry != null)
                {
                    try
                    {
                        this.writer.Write(formattedEntry);
                    }
                    catch (Exception e)
                    {
                        SemanticLoggingEventSource.Log.RollingFlatFileSinkWriteFailed(e.ToString());
                    }
                }
            }

            lock (this.flushLockObject)
            {
                this.flushSource.TrySetResult(true);
            }
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
        /// <param name="value">The current entry to write to the file.</param>
        public void OnNext(EventEntry value)
        {
            if (this.isAsync)
            {
                this.pendingEntries.Add(value);

                if (this.flushSource.Task.IsCompleted)
                {
                    lock (this.flushLockObject)
                    {
                        if (this.flushSource.Task.IsCompleted)
                        {
                            this.flushSource = new TaskCompletionSource<bool>();
                        }
                    }
                }
            }
            else
            {
                this.OnSingleEventWritten(value);
            }
        }

        /// <summary>
        /// Releases all resources used by the current instance of the <see cref="RollingFlatFileSink"/> class.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        private int GetNextRollTimeout()
        {
            const long MaxTime = (long)int.MaxValue;
            var nextRollDate = this.rollingHelper.NextRollDateTime;

            int nextRollTimeout;
            if (nextRollDate.HasValue)
            {
                var milliseconds = (long)(nextRollDate.Value - this.rollingHelper.DateTimeProvider.CurrentDateTime).TotalMilliseconds;
                if (milliseconds > MaxTime)
                {
                    milliseconds = MaxTime;
                }

                nextRollTimeout = (int)milliseconds;
            }
            else
            {
                nextRollTimeout = -1;
            }

            return nextRollTimeout;
        }
    }
}
