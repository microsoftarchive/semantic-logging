using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Formatters;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Schema;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Utility;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.TestObjects
{
    public class StringCollectionEventListener : EventListener, IObserver<EventEntry>
    {
        private readonly EventSourceSchemaCache schemaCache = EventSourceSchemaCache.Instance;
        private IEventTextFormatter formatter;

        public List<string> EventsWritten { get; set; }

        public StringCollectionEventListener(IEventTextFormatter formatter = null)
        {
            this.formatter = formatter ?? new JsonEventTextFormatter();
            EventsWritten = new List<string>();
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            var entry = EventEntry.Create(eventData, this.schemaCache.GetSchema(eventData.EventId, eventData.EventSource));
            OnNext(entry);
        }

        public void OnNext(EventEntry value)
        {
            EventsWritten.Add(formatter.WriteEvent(value));
        }

        public void OnError(Exception error)
        {
        }

        public void OnCompleted()
        {
        }
    }
}