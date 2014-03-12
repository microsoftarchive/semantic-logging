// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Diagnostics.Tracing;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.InProc.Tests.TestObjects
{
    public class MockEventSource : EventSource
    {
        public const int ErrorWithKeywordDiagnosticEventId = 1020;
        public const int CriticalWithKeywordPageEventId = 1021;
        public const int InfoWithKeywordDiagnosticEventId = 1022;
        public const int VerboseWithKeywordPageEventId = 1023;
        public const int CriticalWithTaskNameEventId = 1500;

        public static readonly MockEventSource Logger = new MockEventSource();

        public class Keywords
        {
            public const EventKeywords Page = (EventKeywords)1;
            public const EventKeywords Diagnostic = (EventKeywords)4;
        }

        public class Tasks
        {
            public const EventTask Page = (EventTask)1;
            public const EventTask DBQuery = (EventTask)2;
        }

        [Event(1, Level = EventLevel.Informational)]
        public void Informational(string message)
        {
            if (this.IsEnabled(EventLevel.Informational, EventKeywords.None))
            {
                this.WriteEvent(1, message); 
            }
        }

        [Event(2, Level = EventLevel.Critical, Keywords = EventKeywords.None, Message = "Functional Test", Opcode = EventOpcode.Info, Task = EventTask.None, Version = 0)]
        public void Critical(string message) { this.WriteEvent(2, message); }

        [Event(3, Level = EventLevel.Error, Keywords = EventKeywords.None, Message = "Test Error", Opcode = EventOpcode.Info, Task = EventTask.None, Version = 3)]
        public void Error(string message)
        {
            if (this.IsEnabled(EventLevel.Error, EventKeywords.None))
            {
                this.WriteEvent(3, message); 
            }
        }

        [Event(4, Level = EventLevel.Verbose, Keywords = EventKeywords.None, Message = "Functional Test", Opcode = EventOpcode.Info, Task = EventTask.None, Version = 1)]
        public void Verbose(string message) { this.WriteEvent(4, message); }

        [Event(5, Level = EventLevel.LogAlways, Keywords = EventKeywords.None, Message = "Test LogAlways", Opcode = EventOpcode.Info, Task = EventTask.None, Version = 5)]
        public void LogAlways(string message) { this.WriteEvent(5, message); }

        [Event(6, Level = EventLevel.Warning, Keywords = EventKeywords.None, Message = "Test Warning", Opcode = EventOpcode.Info, Task = EventTask.None, Version = 6)]
        public void Warning(string message) { this.WriteEvent(6, message); }

        [Event(7, Level = EventLevel.Warning, Keywords = EventKeywords.None, Message = "Test OpCode", Opcode = EventOpcode.Resume, Task = EventTask.None, Version = 6)]
        public void WriteWithOpCode(string message) { this.WriteEvent(7, message); }

        [Event(8, Level = EventLevel.Informational)]
        public void LogSomeMessage(string message)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(8, message); 
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

        [Event(InfoWithKeywordDiagnosticEventId, Level = EventLevel.Informational, Keywords = Keywords.Diagnostic)]
        public void InfoWithKeywordDiagnostic(string message)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(InfoWithKeywordDiagnosticEventId, message); 
            }
        }

        [Event(VerboseWithKeywordPageEventId, Level = EventLevel.Verbose, Keywords = Keywords.Page)]
        public void VerboseWithKeywordPage(string message)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(VerboseWithKeywordPageEventId, message); 
            }
        }

        [Event(CriticalWithTaskNameEventId, Level = EventLevel.Critical, Keywords = Keywords.Page, Task = Tasks.Page)]
        public void CriticalWithTaskName(string message)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(CriticalWithTaskNameEventId, message); 
            }
        }

        [Event(14, Level = EventLevel.Informational)]
        public void InformationalWithRelatedActivityId(string message, Guid relatedActivityId)
        {
            if (this.IsEnabled(EventLevel.Informational, EventKeywords.None))
            {
                this.WriteEventWithRelatedActivityId(14, relatedActivityId, message);
            }
        }

        [Event(15, Level = EventLevel.Critical, Keywords = EventKeywords.None, Message = "Functional Test", Opcode = EventOpcode.Info, Task = EventTask.None, Version = 0)]
        public void CriticalWithRelatedActivityId(string message, Guid relatedActivityId)
        {
            this.WriteEventWithRelatedActivityId(15, relatedActivityId, message);
        }
    }

    public class MockEventSource2 : EventSource
    {
        public static readonly MockEventSource2 Logger = new MockEventSource2();

        [Event(1, Level = EventLevel.Error, Keywords = EventKeywords.None, Message = "Test Error", Opcode = EventOpcode.Info, Task = EventTask.None, Version = 3)]
        public void Error(string message) { this.WriteEvent(1, message); }
    }
    public class MockEventSource3 : EventSource
    {
        public static readonly MockEventSource3 Logger = new MockEventSource3();

        [Event(1, Level = EventLevel.Critical, Keywords = EventKeywords.None, Message = "Functional Test", Opcode = EventOpcode.Info, Task = EventTask.None, Version = 0)]
        public void Critical(string message) { this.WriteEvent(1, message); }
    }
}
