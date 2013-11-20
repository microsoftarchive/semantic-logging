// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System.Diagnostics.Tracing;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.TestObjects
{
    [EventSource(Name = "Test")]
    public class TestEventSource : EventSource
    {
        public const int InformationalEventId = 4;
        public const int ErrorEventId = 5;
        public const int CriticalEventId = 6;
        public const int VerboseEventId = 100;
        public const int NonDefaultOpcodeNonDefaultVersionEventId = 103; 
        public const int EventWithoutPayloadNorMessageId = 200;
        public const int EventWithPayloadId = 201;
        public const int EventWithMessageId = 202;
        public const int EventWithPayloadAndMessageId = 203;        
        public const int EventWithHighIdId = (int)ushort.MaxValue + 100;
        public const int EventWithLowIdId = -100;

        [Event(InformationalEventId, Level = EventLevel.Informational)]
        public void Informational(string message)
        {
            if (IsEnabled(EventLevel.Informational, EventKeywords.None)) { WriteEvent(InformationalEventId, message); }
        }

        [Event(ErrorEventId, Level = EventLevel.Error)]
        public void Error(string message)
        {
            if (IsEnabled(EventLevel.Error, EventKeywords.None)) { WriteEvent(ErrorEventId, message); }
        }

        [Event(CriticalEventId, Level = EventLevel.Critical)]
        public void Critical(string message)
        {
            if (IsEnabled(EventLevel.Critical, EventKeywords.None)) { WriteEvent(CriticalEventId, message); }
        }

        [Event(VerboseEventId, Level = EventLevel.Verbose)]
        public void Write(string message) { WriteEvent(VerboseEventId, message); }

        [Event(EventWithoutPayloadNorMessageId, Level = EventLevel.Warning)]
        public void EventWithoutPayloadNorMessage()
        {
            if (IsEnabled(EventLevel.Warning, EventKeywords.None)) { WriteEvent(EventWithoutPayloadNorMessageId); }
        }

        [Event(EventWithPayloadId, Level = EventLevel.Warning)]
        public void EventWithPayload(string payload1, int payload2)
        {
            if (IsEnabled(EventLevel.Warning, EventKeywords.None)) { WriteEvent(EventWithPayloadId, payload1, payload2); }
        }

        [Event(EventWithMessageId, Level = EventLevel.Warning, Message = "Test message")]
        public void EventWithMessage()
        {
            if (IsEnabled(EventLevel.Warning, EventKeywords.None)) { WriteEvent(EventWithMessageId); }
        }

        [Event(EventWithPayloadAndMessageId, Level = EventLevel.Warning, Keywords = EventKeywords.None, Message = "Test message {0} {1}")]
        public void EventWithPayloadAndMessage(string payload1, int payload2)
        {
            if (IsEnabled()) { WriteEvent(EventWithPayloadAndMessageId, payload1, payload2); }
        }

        [Event(NonDefaultOpcodeNonDefaultVersionEventId, Opcode = EventOpcode.Reply, Version = 0x02, Task = Tasks.DBQuery, 
            Message = "arg1- {0},arg2- {1},arg3- {2}")]
        public void NonDefaultOpcodeNonDefaultVersionEvent(int arg1, int arg2, int arg3)
        {
            if (IsEnabled()) { WriteEvent(NonDefaultOpcodeNonDefaultVersionEventId, arg1, arg2, arg3); }
        }

        [Event(305)]
        public void UsingEnumArguments(MyLongEnum arg1, MyIntEnum arg2)
        {
            if (IsEnabled()) { WriteEvent(305, arg1, arg2); }
        }

        [Event(50)]
        public void FastEvent(int arg)
        { 
            WriteEvent(50, arg); 
        }

        public static readonly TestEventSource Log = new TestEventSource();

        public class Tasks
        {
            public const EventTask Page = (EventTask)1;
            public const EventTask DBQuery = (EventTask)2;
        }
    }
}
