// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Formatters;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Schema;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Utility;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw.Service
{
    internal class ServiceEventLogSink : IObserver<EventEntry>
    {
        private readonly IEventTextFormatter formatter;
        private EventLog eventLog;

        internal ServiceEventLogSink(EventLog eventLog)
        {
            this.eventLog = eventLog;
            this.formatter = new EventTextFormatter();
        }

        public void OnCompleted()
        {
            this.eventLog = null;
        }

        public void OnError(Exception error)
        {
            this.eventLog = null;
        }

        public void OnNext(EventEntry value)
        {
            var log = this.eventLog;
            if (log != null)
            {                
                log.WriteEntry(this.formatter.WriteEvent(value), this.ToEventLogEntryType(value.Schema.Level));
            }
        }

        private EventLogEntryType ToEventLogEntryType(EventLevel level)
        {
            switch (level)
            {
                case EventLevel.Critical:
                case EventLevel.Error:
                    return EventLogEntryType.Error;
                case EventLevel.Warning:
                    return EventLogEntryType.Warning;
                default:
                    return EventLogEntryType.Information;
            }
        }
    }
}
