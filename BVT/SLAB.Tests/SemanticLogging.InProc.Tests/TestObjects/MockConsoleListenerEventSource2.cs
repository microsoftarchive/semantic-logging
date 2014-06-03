// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System.Diagnostics.Tracing;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.InProc.Tests.TestObjects
{
    public sealed class MockConsoleListenerEventSource2 : EventSource
    {
        public static readonly MockConsoleListenerEventSource2 Logger = new MockConsoleListenerEventSource2();

        //There is no default color mapped to to Informational
        [Event(100, Level = EventLevel.Informational)]
        public void Informational(string message)
        {
            if (this.IsEnabled(EventLevel.Informational, EventKeywords.None))
            {
                this.WriteEvent(100, message); 
            }
        }

        [Event(200, Level = EventLevel.Critical, Keywords = EventKeywords.None, Message = "Functional Test", Opcode = EventOpcode.Info, Task = EventTask.None, Version = 0)]
        public void Critical(string message) { this.WriteEvent(200, message); }

        [Event(300, Level = EventLevel.Error, Keywords = EventKeywords.None, Message = "Test Error", Opcode = EventOpcode.Info, Task = EventTask.None, Version = 3)]
        public void Error(string message) { this.WriteEvent(300, message); }

        [Event(400, Level = EventLevel.Verbose, Keywords = EventKeywords.None, Message = "Functional Test", Opcode = EventOpcode.Info, Task = EventTask.None, Version = 1)]
        public void Verbose(string message) { this.WriteEvent(400, message); }

        [Event(500, Level = EventLevel.LogAlways, Keywords = EventKeywords.None, Message = "Test LogAlways", Opcode = EventOpcode.Info, Task = EventTask.None, Version = 5)]
        public void LogAlways(string message) { this.WriteEvent(500, message); }

        [Event(600, Level = EventLevel.Warning, Keywords = EventKeywords.None, Message = "Test Warning", Opcode = EventOpcode.Info, Task = EventTask.None, Version = 6)]
        public void Warning(string message) { this.WriteEvent(600, message); }
    }
}
