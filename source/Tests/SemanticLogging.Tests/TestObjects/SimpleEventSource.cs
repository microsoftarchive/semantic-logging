// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System.Diagnostics.Tracing;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.TestObjects
{
    [EventSource(Name = "SimpleEventSource-CustomName")]
    public class SimpleEventSource : EventSource
    {
        public class Opcodes
        {
            public const EventOpcode CustomOpcode1 = (EventOpcode)100;
            public const EventOpcode CustomOpcode2 = (EventOpcode)101;
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

        [Event(3, Opcode = EventOpcode.Start)]
        public void NoTaskSpecfied(int event3Arg0, int event3Arg1, int event3Arg2) { }

        [Event(4, Opcode = EventOpcode.Info)]
        public void OpcodeInfo() { }

        [Event(5, Opcode = EventOpcode.Start)]
        public void OpcodeStart() { }

        [Event(6, Opcode = EventOpcode.Stop)]
        public void OpcodeStop() { }

        [Event(7, Opcode = EventOpcode.DataCollectionStart)]
        public void OpcodeDataCollectionStart() { }

        [Event(8, Opcode = EventOpcode.DataCollectionStop)]
        public void OpcodeDataCollectionStop() { }

        [Event(9, Opcode = EventOpcode.Extension)]
        public void OpcodeExtension() { }

        [Event(10, Opcode = EventOpcode.Reply)]
        public void OpcodeReply() { }

        [Event(11, Opcode = EventOpcode.Resume)]
        public void OpcodeResume() { }

        [Event(12, Opcode = EventOpcode.Suspend)]
        public void OpcodeSuspend() { }

        [Event(13, Opcode = EventOpcode.Send)]
        public void OpcodeSend() { }

        [Event(14, Opcode = EventOpcode.Receive)]
        public void OpcodeReceive() { }

        [Event(15, Opcode = Opcodes.CustomOpcode1)]
        public void CustomOpcode1Event() { }

        [Event(16, Opcode = Opcodes.CustomOpcode2)]
        public void CustomOpcode2Event() { }

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
    }
}
