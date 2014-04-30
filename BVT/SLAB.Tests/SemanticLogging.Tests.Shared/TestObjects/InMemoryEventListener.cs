// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Formatters;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Schema;
using System;
using System.Diagnostics.Tracing;
using System.IO;
using System.Threading;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.Shared.TestObjects
{
    public class InMemoryEventListener : EventListener, IDisposable
    {
        public MemoryStream Stream;
        private readonly EventSourceSchemaCache schemaCache = EventSourceSchemaCache.Instance;
        private readonly object lockObject = new object();
        private StreamWriter writer;
        private ManualResetEventSlim waitEvents;
        private long writenBytes;
        private bool disposed;

        public InMemoryEventListener()
            : this(new EventTextFormatter())
        {
            this.waitEvents = new ManualResetEventSlim(); 
        }

        public InMemoryEventListener(IEventTextFormatter formatter)
        {
            this.Formatter = formatter;
            this.Stream = new MemoryStream();
            this.writer = new StreamWriter(this.Stream);
        }

        public ManualResetEventSlim WaitEvents
        {
            get { return this.waitEvents; }
        }

        public IEventTextFormatter Formatter { get; set; }

        public override string ToString()
        {
            if (false == this.Stream.CanRead)
            {
                return string.Empty; 
            }

            this.Stream.Position = 0;
            var reader = new StreamReader(this.Stream);
            return reader.ReadToEnd();
        }

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
                    if (this.waitEvents != null)
                    {
                        this.waitEvents.Dispose();
                    }

                    if (this.writer != null)
                    {
                        this.writer.Dispose();
                    }

                    if (this.Stream != null)
                    {
                        this.Stream.Dispose();
                    }
                }

                this.waitEvents = null;
                this.writer = null;
                this.Stream = null;

                this.disposed = true;
            }
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            var entry = EventEntry.Create(eventData, this.schemaCache.GetSchema(eventData.EventId, eventData.EventSource));

            lock (this.lockObject)
            {
                this.Formatter.WriteEvent(entry, this.writer);
                this.writer.Flush();
                this.writenBytes = this.Stream.Length;

                if (this.waitEvents != null)
                {
                    this.waitEvents.Reset(); 
                }
            }
        }

        ~InMemoryEventListener()
        {
            this.Dispose(false);
        }
    }
}
