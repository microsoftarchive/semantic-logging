// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System.Diagnostics.Tracing;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.InProc.Tests.TestObjects
{
    public sealed class TestEventSource : EventSource
    {
        public const int InformationalEventId = 4;
        public const int AuditSuccessEventId = 20;
        public const int ErrorEventId = 5;
        public const int CriticalEventId = 6;
        public const int LogAlwaysEventId = 7;
        public const int VerboseEventId = 100;
        public const int NonDefaultOpcodeNonDefaultVersionEventId = 103;
        public const int EventWithoutPayloadNorMessageId = 200;
        public const int EventWithPayloadId = 201;
        public const int EventWithMessageId = 202;
        public const int EventWithPayloadAndMessageId = 203;
        public const int EventIdForAllParameters = 150;
        public const int EventWithMultiplePayloadsId = 205;
        public const int ErrorWithKeywordDiagnosticEventId = 1020;
        public const int CriticalWithKeywordPageEventId = 1021;
        public const int CriticalWithTaskNameEventId = 1500;

        public static readonly TestEventSource Logger = new TestEventSource();

        public class Keywords
        {
            public const EventKeywords Page = (EventKeywords)1;
            public const EventKeywords DataBase = (EventKeywords)2;
            public const EventKeywords Diagnostic = (EventKeywords)4;
            public const EventKeywords Perf = (EventKeywords)8;
        }

        public class Tasks
        {
            public const EventTask Page = (EventTask)1;
            public const EventTask DBQuery = (EventTask)2;
        }

        [Event(InformationalEventId, Level = EventLevel.Informational)]
        public void Informational(string message)
        {
            if (this.IsEnabled(EventLevel.Informational, EventKeywords.None))
            {
                this.WriteEvent(InformationalEventId, message); 
            }
        }

        [Event(ErrorEventId, Level = EventLevel.Error)]
        public void Error(string message)
        {
            if (this.IsEnabled(EventLevel.Error, EventKeywords.None))
            {
                this.WriteEvent(ErrorEventId, message); 
            }
        }

        [Event(CriticalEventId, Level = EventLevel.Critical)]
        public void Critical(string message)
        {
            if (this.IsEnabled(EventLevel.Critical, EventKeywords.None))
            {
                this.WriteEvent(CriticalEventId, message); 
            }
        }

        [Event(VerboseEventId, Level = EventLevel.Verbose)]
        public void Write(string message) { this.WriteEvent(VerboseEventId, message); }

        [Event(EventWithoutPayloadNorMessageId, Level = EventLevel.Warning)]
        public void EventWithoutPayloadNorMessage()
        {
            if (this.IsEnabled(EventLevel.Warning, EventKeywords.None))
            {
                this.WriteEvent(EventWithoutPayloadNorMessageId); 
            }
        }

        [Event(EventWithPayloadId, Level = EventLevel.Warning)]
        public void EventWithPayload(string payload1, int payload2)
        {
            if (this.IsEnabled(EventLevel.Warning, EventKeywords.None))
            {
                this.WriteEvent(EventWithPayloadId, payload1, payload2); 
            }
        }

        [Event(EventWithMultiplePayloadsId, Level = EventLevel.Warning)]
        public void EventWithMultiplePayloads(string payload1, string payload2, string payload3)
        {
            if (this.IsEnabled(EventLevel.Warning, EventKeywords.None))
            {
                this.WriteEvent(EventWithMultiplePayloadsId, payload1, payload2, payload3); 
            }
        }

        [Event(EventWithMessageId, Level = EventLevel.Warning, Message = "Test message")]
        public void EventWithMessage()
        {
            if (this.IsEnabled(EventLevel.Warning, EventKeywords.None))
            {
                this.WriteEvent(EventWithMessageId); 
            }
        }

        [Event(EventWithPayloadAndMessageId, Level = EventLevel.Warning, Message = "Test message {0} {1}")]
        public void EventWithPayloadAndMessage(string payload1, int payload2)
        {
            if (this.IsEnabled(EventLevel.Warning, EventKeywords.None))
            {
                this.WriteEvent(EventWithPayloadAndMessageId, payload1, payload2); 
            }
        }

        [Event(EventIdForAllParameters, Keywords = EventKeywords.None, Level = EventLevel.Informational, Message = "Test All Parameters", Opcode = EventOpcode.Info, Task = EventTask.None, Version = 3)]
        public void AllParameters()
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(EventIdForAllParameters); 
            }
        }

        [Event(10001, Keywords = Keywords.Page, Level = EventLevel.Informational, Message = "Test All Parameters with custom values", Opcode = EventOpcode.Info, Task = Tasks.Page, Version = 3)]
        public void AllParametersWithCustomValues()
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(10001); 
            }
        }

        [Event(ErrorWithKeywordDiagnosticEventId, Level = EventLevel.Error, Keywords = Keywords.Diagnostic)]
        public void ErrorWithKeywordDiagnostic(string message)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(ErrorWithKeywordDiagnosticEventId, message); 
            }
        }

        [Event(CriticalWithKeywordPageEventId, Level = EventLevel.Critical, Keywords = Keywords.Page)]
        public void CriticalWithKeywordPage(string message)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(CriticalWithKeywordPageEventId, message); 
            }
        }

        [Event(CriticalWithTaskNameEventId, Level = EventLevel.Critical, Keywords = Keywords.Page, Task = Tasks.DBQuery)]
        public void CriticalWithTaskName(string message)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(CriticalWithTaskNameEventId, message); 
            }
        }
    }
}
