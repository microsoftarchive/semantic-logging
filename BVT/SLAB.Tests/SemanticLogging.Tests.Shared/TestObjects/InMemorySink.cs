// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Formatters;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Schema;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.Shared.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics.Tracing;
using System.IO;
using System.Threading;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.Shared.TestObjects
{
    public class InMemorySink : EventListener, IObserver<EventEntry>, IDisposable
    {
        private readonly EventSourceSchemaCache schemaCache = EventSourceSchemaCache.Instance;
        private readonly object lockObject = new object();
        private MemoryStream memory;
        private StreamWriter writer;
        private long writenBytes;
        private ManualResetEventSlim waitOnAsync;
        private bool disposed;

        public InMemorySink()
            : this(null)
        {
        }

        public InMemorySink(IEventTextFormatter formatter = null)
        {
            this.Formatter = formatter ?? new EventTextFormatter(verbosityThreshold: EventLevel.LogAlways);
            this.memory = new MemoryStream();
            this.writer = new StreamWriter(this.memory) { AutoFlush = false };
            this.waitOnAsync = new ManualResetEventSlim();
        }

        public IEventTextFormatter Formatter { get; set; }

        public int EventWrittenCount { get; private set; }

        public Func<bool> WaitSignalCondition { get; set; }

        public override string ToString()
        {
            this.memory.Position = 0;
            return new StreamReader(this.memory).ReadToEnd();
        }

        public WaitHandle WaitOnAsyncEvents { get { return this.waitOnAsync.WaitHandle; } }

        public override sealed void Dispose()
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
                    if (this.memory != null)
                    {
                        this.memory.Dispose();
                    }

                    if (this.writer != null)
                    {
                        this.writer.Dispose();
                    }

                    if (this.waitOnAsync != null)
                    {
                        this.waitOnAsync.Dispose();
                    }
                }

                this.memory = null;
                this.writer = null;
                this.waitOnAsync = null;

                this.disposed = true;
            }
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            var entry = EventEntry.Create(eventData, this.schemaCache.GetSchema(eventData.EventId, eventData.EventSource));

            this.OnNext(entry);
        }

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void OnNext(EventEntry value)
        {
            lock (this.lockObject)
            {
                try
                {
                    this.writer.Write(this.Formatter.WriteEvent(value));
                    this.EventWrittenCount++;
                    // check that no data was already flushed
                    Assert.AreEqual(this.writenBytes, this.memory.Length);
                    this.writer.Flush();
                    this.writenBytes = this.memory.Length;
                }
                finally
                {
                    // mark any async event as done       
                    if (this.WaitSignalCondition == null || this.WaitSignalCondition())
                    {
                        this.waitOnAsync.Set();
                    }
                }
            }
        }
    }
}
