// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using Microsoft.Practices.EnterpriseLibrary.SemanticLogging;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Formatters;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Schema;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Utility;
using System;
using System.Collections.Concurrent;
using System.Diagnostics.Tracing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.Shared.TestObjects
{
    public class MockFlatFileSink : IObserver<EventEntry>, IDisposable
    {
        private readonly object lockObject = new object();
        private readonly object flushLockObject = new object();
        private readonly EventSourceSchemaCache schemaCache = EventSourceSchemaCache.Instance;
        private StreamWriter writer;
        private bool disposed;
        private volatile TaskCompletionSource<bool> flushSource = new TaskCompletionSource<bool>();

        public MockFlatFileSink(string fileName, string header)
            : this(fileName, header, null)
        {
        }

        public MockFlatFileSink(string fileName, string header, IEventTextFormatter formatter = null)
        {
            FileUtil.ValidFile(fileName);

            this.Formatter = formatter ?? new EventTextFormatter(header: header, verbosityThreshold: EventLevel.LogAlways);

            var file = FileUtil.ProcessFileNameForLogging(fileName);

            this.writer = new StreamWriter(file.Open(FileMode.Append, FileAccess.Write, FileShare.Read));

            this.flushSource.SetResult(true);
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
        /// Finalizes an instance of the <see cref="FlatFileSink"/> class.
        /// </summary>
        ~MockFlatFileSink()
        {
            this.Dispose(false);
        }

        public IEventTextFormatter Formatter { get; set; }
        public int EventWrittenCount { get; private set; }
        public Func<bool> WaitSignalCondition { get; set; }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                if (disposing)
                {
                    if (this.writer != null)
                    {
                        this.writer.Dispose();
                    }
                }

                this.writer = null;
                this.disposed = true;
            }
        }

        private void OnSingleEventWritten(EventEntry entry)
        {
            try
            {
                lock (this.lockObject)
                {
                    try
                    {
                        string strEntry = Formatter.WriteEvent(entry);

                        this.writer.Write(strEntry);
                        this.writer.Flush();
                    }
                    catch (Exception)
                    {
                    }
                }
            }
            catch (Exception e)
            {
                SemanticLoggingEventSource.Log.FlatFileSinkWriteFailed(e.ToString());
            }
        }

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
            this.OnSingleEventWritten(value);
        }
    }
}