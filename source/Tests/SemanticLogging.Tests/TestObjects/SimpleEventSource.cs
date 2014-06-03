// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System.Diagnostics.Tracing;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.TestObjects
{
    [EventSource(Name = "SimpleEventSource-CustomName")]
    public sealed class SimpleEventSource : EventSource
    {
        public static class Opcodes
        {
            public const EventOpcode Opcode1 = (EventOpcode)100;
            public const EventOpcode Opcode2 = (EventOpcode)101;
        }

        public static class Tasks
        {
            public const EventTask Custom = (EventTask)1;
            public const EventTask Opcode = (EventTask)2;
        }

        public static class Keywords
        {
            public const EventKeywords LongKeyword = (EventKeywords)(1L << 33);
        }

        public static readonly SimpleEventSource Log = new SimpleEventSource();

        [Event(1, Level = EventLevel.Warning, Version = 1)]
        public void MyEvent1(string event1Arg0, int event1Arg1)
        {
            if (IsEnabled(EventLevel.Informational, EventKeywords.None)) { WriteEvent(1, event1Arg0, event1Arg1); }
        }

        public void MyEvent2(string event2Arg0, int event2Arg1)
        {
            WriteEvent(2, event2Arg0, event2Arg1);
        }

#if !EVENT_SOURCE_PACKAGE
        [Event(3, Opcode = EventOpcode.Start)]
        public void NoTaskSpecfied(int event3Arg0, int event3Arg1, int event3Arg2) { }
#endif

        [Event(4, Task = Tasks.Opcode, Opcode = EventOpcode.Info)]
        public void OpcodeInfo() { }

        [Event(5, Task = Tasks.Opcode, Opcode = EventOpcode.Start)]
        public void OpcodeStart() { }

        [Event(6, Task = Tasks.Opcode, Opcode = EventOpcode.Stop)]
        public void OpcodeStop() { }

        [Event(7, Task = Tasks.Opcode, Opcode = EventOpcode.DataCollectionStart)]
        public void OpcodeDataCollectionStart() { }

        [Event(8, Task = Tasks.Opcode, Opcode = EventOpcode.DataCollectionStop)]
        public void OpcodeDataCollectionStop() { }

        [Event(9, Task = Tasks.Opcode, Opcode = EventOpcode.Extension)]
        public void OpcodeExtension() { }

        [Event(10, Task = Tasks.Opcode, Opcode = EventOpcode.Reply)]
        public void OpcodeReply() { }

        [Event(11, Task = Tasks.Opcode, Opcode = EventOpcode.Resume)]
        public void OpcodeResume() { }

        [Event(12, Task = Tasks.Opcode, Opcode = EventOpcode.Suspend)]
        public void OpcodeSuspend() { }

        [Event(13, Task = Tasks.Opcode, Opcode = EventOpcode.Send)]
        public void OpcodeSend() { }

        [Event(14, Task = Tasks.Opcode, Opcode = EventOpcode.Receive)]
        public void OpcodeReceive() { }

        [Event(15, Task = Tasks.Custom, Opcode = Opcodes.Opcode1)]
        public void CustomOpcode1() { }

        [Event(16, Task = Tasks.Custom, Opcode = Opcodes.Opcode2)]
        public void CustomOpcode2() { }

        [Event(21, Level = EventLevel.LogAlways)]
        public void LevelLogAlways() { }

        [Event(22, Level = EventLevel.Critical)]
        public void LevelCritical() { }

        [Event(23, Level = EventLevel.Error)]
        public void LevelError() { }

        [Event(24, Level = EventLevel.Warning)]
        public void LevelWarning() { }

        [Event(25, Level = EventLevel.Informational)]
        public void LevelInformational() { }

        [Event(26, Level = EventLevel.Verbose)]
        public void LevelVerbose() { }

        [Event(27, Keywords = Keywords.LongKeyword)]
        public void EventWithLongKeywords() { }
    }
}
