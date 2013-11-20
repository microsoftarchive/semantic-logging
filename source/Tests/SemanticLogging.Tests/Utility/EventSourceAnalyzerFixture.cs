// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Diagnostics.Tracing;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.TestObjects;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Utility;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.Utility
{
    [TestClass]
    public class given_eventSourceAnalyzer
    {
        [TestMethod]
        [ExpectedException(typeof(EventSourceAnalyzerException))]
        public void when_inspecting_event_with_bad_format()
        {
            EventSourceAnalyzer.InspectAll(BadFormatEventSource.Log);
        }

        [TestMethod] //Note: type in error message comes from ETW internal API
        [ExpectedException(typeof(ArgumentException))]
        public void when_inspecting_event_with_eventId_mismatch()
        {
            EventSourceAnalyzer.InspectAll(EventIdMismatchEventSource.Log);
        }

        [TestMethod]
        [ExpectedException(typeof(EventSourceAnalyzerException))]
        public void when_inspecting_event_with_missing_writeEvent_call()
        {
            EventSourceAnalyzer.InspectAll(MissingWriteEventCallEventSource.Log);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void when_inspecting_event_with_duplicate_events()
        {
            EventSourceAnalyzer.InspectAll(DuplicateEventsEventSource.Log);
        }

        [TestMethod]
        [ExpectedException(typeof(EventSourceAnalyzerException))]
        public void when_inspecting_source_with_no_events()
        {
            EventSourceAnalyzer.InspectAll(NoEventsEventSource.Log);
        }

        [TestMethod]
        [ExpectedException(typeof(EventSourceAnalyzerException))]
        public void when_inspecting_source_with_argument_order_mismatch_of_same_type()
        {
            EventSourceAnalyzer.InspectAll(SameTypeArgumentsOrderMismatchEventSource.Log);
        }

        [TestMethod]
        [ExpectedException(typeof(EventSourceAnalyzerException))]
        public void when_inspecting_source_with_argument_order_mismatch_of_same_type_mixed()
        {
            EventSourceAnalyzer.InspectAll(BooleanTypeArgumentsOrderMismatchEventSource.Log);
        }

        [TestMethod]
        [ExpectedException(typeof(EventSourceAnalyzerException))]
        public void when_inspecting_source_with_argument_order_mismatch_of_different_type()
        {
            EventSourceAnalyzer.InspectAll(DifferentTypeArgumentsOrderMismatchEventSource.Log);
        }

        [TestMethod]
        [ExpectedException(typeof(EventSourceAnalyzerException))]
        public void when_inspecting_event_with_incorrect_level_filtering()
        {
            EventSourceAnalyzer.InspectAll(IncorrectLevelFilterEventSource.Log);
        }

        [TestMethod]
        [ExpectedException(typeof(EventSourceAnalyzerException))]
        public void when_inspecting_event_with_incorrect_keywords_filtering()
        {
            EventSourceAnalyzer.InspectAll(IncorrectKeywordsFilterEventSource.Log);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void when_not_defined_opcode()
        {
            EventSourceAnalyzer.InspectAll(NonDefinedOpcodeEventSource.Log);
        }
       
        [TestMethod]
        [ExpectedException(typeof(EventSourceAnalyzerException))]
        public void when_inspecting_event_with_null_argument_in_writeEvent()
        {
            EventSourceAnalyzer.InspectAll(NullTypeArgumentEventSource.Log);
        }
        
        [TestMethod]
        [ExpectedException(typeof(EventSourceAnalyzerException))]
        public void when_inspecting_event_with_less_writeEvent_arguments()
        {
            EventSourceAnalyzer.InspectAll(LessWriteEventArgumentsEventSource.Log);
        }

        [TestMethod]
        [ExpectedException(typeof(EventSourceAnalyzerException))]
        public void when_inspecting_event_with_more_writeEvent_arguments()
        {
            EventSourceAnalyzer.InspectAll(MoreWriteEventArgumentsEventSource.Log);
        }

        [TestMethod]
        [ExpectedException(typeof(EventSourceAnalyzerException))]
        public void when_inspecting_event_with_differentTypeArguments_in_writeEvent()
        {
            EventSourceAnalyzer.InspectAll(DifferentTypeArgumentsWriteEventEventSource.Log);
        }
         
        [TestMethod]
        [ExpectedException(typeof(EventSourceAnalyzerException))]
        public void when_inspecting_event_enum_types_that_generates_invalid_manifest()
        {
            EventSourceAnalyzer.InspectAll(DifferentEnumsEventSource.Log);
        }

        [TestMethod]
        public void when_inspecting_valid_eventSources()
        {
            EventSourceAnalyzer.InspectAll(MultipleTypesEventSource.Log);
            EventSourceAnalyzer.InspectAll(TestEventSource.Log);
            EventSourceAnalyzer.InspectAll(MyCompanyEventSource.Log);
            EventSourceAnalyzer.InspectAll(SemanticLoggingEventSource.Log);
        }        

        #region Test EventSource classes

        [EventSource]
        private class BadFormatEventSource : EventSource
        {
            internal static readonly BadFormatEventSource Log = new BadFormatEventSource();

            [Event(1, Message = "<bad message>")]
            public void EventWithBadMessage()
            {
            }
        }

        [EventSource]
        private class NoSingletonEventSource : EventSource
        {
            [Event(1)]
            public void SimpleEvent()
            {
                if (IsEnabled()) { WriteEvent(1); }
            }
        }

        [EventSource]
        private class EventIdMismatchEventSource : EventSource
        {
            internal static readonly EventIdMismatchEventSource Log = new EventIdMismatchEventSource();

            [Event(1)]
            public void EventWithIdMismatch()
            {
                if (IsEnabled()) { WriteEvent(2); }
            }
        }

        [EventSource]
        private class NoEventsEventSource : EventSource
        {
            internal static readonly NoEventsEventSource Log = new NoEventsEventSource();

            [NonEvent]
            public void NoEvent()
            {
            }
        }

        [EventSource]
        private class DuplicateEventsEventSource : EventSource
        {
            internal static readonly DuplicateEventsEventSource Log = new DuplicateEventsEventSource();

            [Event(1)]
            public void Event1()
            {
                if (IsEnabled()) { WriteEvent(1); }
            }

            [Event(1)]
            public void DuplicateEvent1()
            {
                if (IsEnabled()) { WriteEvent(1); }
            }
        }

        [EventSource]
        private class NotDecoratedPublicMethodsEventSource : EventSource
        {
            internal static readonly NotDecoratedPublicMethodsEventSource Log = new NotDecoratedPublicMethodsEventSource();

            [NonEvent]
            public void NotAnEvent()
            {
            }

            public void MissingEventAttribute1()
            {
                WriteEvent(1);
            }

            public void MissingEventAttribute2()
            {
                WriteEvent(2);
            }
        }

        [EventSource]
        private class MissingWriteEventCallEventSource : EventSource
        {
            internal static readonly MissingWriteEventCallEventSource Log = new MissingWriteEventCallEventSource();

            [Event(1)]
            public void MissingWriteEventCall()
            {
            }
        }

        [EventSource]
        private class IncorrectLevelFilterEventSource : EventSource
        {
            internal static readonly IncorrectLevelFilterEventSource Log = new IncorrectLevelFilterEventSource();

            [Event(1, Level = EventLevel.Critical)]
            public void IncorrectEventLevelFilter()
            {
                if (this.IsEnabled(EventLevel.Informational, EventKeywords.None))
                {
                    WriteEvent(1);
                }
            }
        }

        [EventSource]
        private class IncorrectKeywordsFilterEventSource : EventSource
        {
            public class Keywords
            {
                public const EventKeywords CustomKeyword = (EventKeywords)0x0001;
            }

            internal static readonly IncorrectKeywordsFilterEventSource Log = new IncorrectKeywordsFilterEventSource();

            [Event(1, Level = EventLevel.Error, Keywords = Keywords.CustomKeyword)]
            public void IncorrectEventKeywordsFilter()
            {
                if (this.IsEnabled(EventLevel.Error, EventKeywords.None))
                {
                    WriteEvent(1);
                }
            }
        }

        [EventSource]
        private class SameTypeArgumentsOrderMismatchEventSource : EventSource
        {
            internal static readonly SameTypeArgumentsOrderMismatchEventSource Log = new SameTypeArgumentsOrderMismatchEventSource();

            [Event(1)]
            internal void AllSameType(int arg0, int arg1, int arg2)
            {
                WriteEvent(1, arg1, arg2, arg0);
            }
        }

        [EventSource]
        private class BooleanTypeArgumentsOrderMismatchEventSource : EventSource
        {
            internal static readonly BooleanTypeArgumentsOrderMismatchEventSource Log = new BooleanTypeArgumentsOrderMismatchEventSource();

            [Event(1)]
            internal void MixedAndSameType(int i0, bool b1, bool b2, int i3)
            {
                WriteEvent(1, i0, Convert.ToInt32(b2), Convert.ToInt32(b1), i3);
            }
        }

        [EventSource]
        private class DifferentTypeArgumentsOrderMismatchEventSource : EventSource
        {
            internal static readonly DifferentTypeArgumentsOrderMismatchEventSource Log = new DifferentTypeArgumentsOrderMismatchEventSource();

            [Event(1)]
            internal void AllDifferentType(int arg0, string arg1, byte arg2)
            {
                WriteEvent(1, arg0, arg2, arg1);
            }
        }

        [EventSource]
        private class NonDefinedOpcodeEventSource : EventSource
        {
            internal static readonly NonDefinedOpcodeEventSource Log = new NonDefinedOpcodeEventSource();

            [Event(1, Opcode = (EventOpcode)13)]
            public void IncorrectOpcodeValue()
            {
                WriteEvent(1);
            }
        }

        [EventSource]
        private class NullTypeArgumentEventSource : EventSource
        {
            internal static readonly NullTypeArgumentEventSource Log = new NullTypeArgumentEventSource();

            [Event(1)]
            internal void WriteEventWithNull(int arg0, string arg1, long arg2)
            {
                arg1 = null;
                WriteEvent(1, arg0, arg1, arg2);
            }
        }

        [EventSource]
        private class LessWriteEventArgumentsEventSource : EventSource
        {
            internal static readonly LessWriteEventArgumentsEventSource Log = new LessWriteEventArgumentsEventSource();

            [Event(1)]
            internal void LessWriteEventArgs(int arg0, string arg1, long arg2)
            {
                WriteEvent(1, arg0, arg1);
            }
        }

        [EventSource]
        private class MoreWriteEventArgumentsEventSource : EventSource
        {
            internal static readonly MoreWriteEventArgumentsEventSource Log = new MoreWriteEventArgumentsEventSource();

            [Event(1)]
            internal void MoreWriteEventArgs(int arg0, string arg1)
            {
                long arg2 = 1;
                WriteEvent(1, arg0, arg1, arg2);
            }
        }

        [EventSource]
        private class DifferentTypeArgumentsWriteEventEventSource : EventSource
        {
            internal static readonly DifferentTypeArgumentsWriteEventEventSource Log = new DifferentTypeArgumentsWriteEventEventSource();

            [Event(1)]
            internal void IntMappedToEnum(int id)
            {
                WriteEvent(1, EventKeywords.AuditFailure);
            }
        }

        #endregion
    }
}
