// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Formatters;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Schema;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Utility;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics.Tracing;
using System.IO;
using System.Threading;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.TestObjects
{
    public class InMemoryEventListener : EventListener, IObserver<EventEntry>
    {
        private readonly EventSourceSchemaCache schemaCache = EventSourceSchemaCache.Instance;
        private readonly object lockObject = new object();
        private MemoryStream memory;
        private StreamWriter writer;
        private long writenBytes;
        private ManualResetEventSlim waitOnAsync;

        public InMemoryEventListener()
            : this(null)
        {
        }

        public InMemoryEventListener(IEventTextFormatter formatter = null)
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

        public override void Dispose()
        {
            base.Dispose();
            this.writer.Dispose();
            this.waitOnAsync.Dispose();
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            var entry = EventEntry.Create(eventData, this.schemaCache.GetSchema(eventData.EventId, eventData.EventSource));

            OnNext(entry);
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
                    this.writer.Flush();
                    writenBytes = this.memory.Length;
                }
                finally
                {
                    // mark any async event as done       
                    if (WaitSignalCondition == null ||
                        WaitSignalCondition())
                    {
                        this.waitOnAsync.Set();
                    }
                }
            }
        }
    }
}
