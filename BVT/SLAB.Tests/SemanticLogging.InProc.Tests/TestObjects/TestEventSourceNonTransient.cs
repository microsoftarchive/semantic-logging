// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System.Diagnostics.Tracing;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.InProc.Tests.TestObjects
{
    public class TestEventSourceNonTransient : EventSource
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

        public static readonly TestEventSourceNonTransient Logger = new TestEventSourceNonTransient();

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

        [Event(EventWithPayloadId, Level = EventLevel.Warning)]
        public void EventWithPayload(string payload1, int payload2)
        {
            if (this.IsEnabled(EventLevel.Warning, EventKeywords.None))
            {
                this.WriteEvent(EventWithPayloadId, payload1, payload2); 
            }
        }
    }
}
