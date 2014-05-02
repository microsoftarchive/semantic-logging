// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Formatters;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.TestObjects;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.Formatters
{
    [TestClass]
    public class given_json_event_text_formatter_configuration : ContextBase
    {
        [TestMethod]
        public void when_creating_formatter_with_default_values()
        {
            var formatter = new JsonEventTextFormatter();
            Assert.IsNull(formatter.DateTimeFormat);
            Assert.AreEqual(EventTextFormatting.None, formatter.Formatting);
        }

        [TestMethod]
        public void when_creating_formatter_with_specific_values()
        {
            var formatter = new JsonEventTextFormatter(EventTextFormatting.Indented) { DateTimeFormat = "R" };
            Assert.AreEqual("R", formatter.DateTimeFormat);
            Assert.AreEqual(EventTextFormatting.Indented, formatter.Formatting);
        }

        [TestMethod]
        public void when_creating_formatter_with_null_dateTimeFormat()
        {
            var formatter = new JsonEventTextFormatter(EventTextFormatting.Indented) { DateTimeFormat = null };

            Assert.IsNull(formatter.DateTimeFormat);
        }
    }

    public abstract class given_json_event_text_formatter : ContextBase
    {
        private InMemoryEventListener listener;
        protected TestEventSource logger = TestEventSource.Log;
        private IEnumerable<TestEventEntry> entries;
        protected JsonEventTextFormatter formatter;

        protected override void Given()
        {
            formatter = new JsonEventTextFormatter();
            listener = new InMemoryEventListener() { Formatter = formatter };
            listener.EnableEvents(logger, EventLevel.LogAlways);
        }

        protected override void OnCleanup()
        {
            listener.DisableEvents(logger);
            listener.Dispose();
        }

        protected string RawOutput
        {
            get { return listener.ToString(); }
        }

        protected IEnumerable<TestEventEntry> Entries
        {
            get
            {
                if (entries == null)
                {
                    entries = JsonConvert.DeserializeObject<TestEventEntry[]>("[" + this.RawOutput + "]");
                }
                return entries;
            }
        }

        [TestClass]
        public class when_receiving_event_with_payload_and_message : given_json_event_text_formatter
        {
            protected override void When()
            {
                logger.EventWithPayloadAndMessage("Info", 100);
            }

            [TestMethod]
            public void then_writes_event_data()
            {
                var entry = this.Entries.SingleOrDefault();

                var processId = System.Diagnostics.Process.GetCurrentProcess().Id;
                var threadId = Utility.NativeMethods.GetCurrentThreadId();

                Assert.IsNotNull(entry);
                Assert.IsFalse(this.RawOutput.StartsWith("{\r\n")); // No Formatting (Default)
                Assert.AreEqual<int>(TestEventSource.EventWithPayloadAndMessageId, entry.EventId);
                Assert.AreEqual<Guid>(TestEventSource.Log.Guid, entry.ProviderId);
                Assert.AreEqual<int>((int)EventLevel.Warning, entry.Level);
                Assert.AreEqual<long>((long)EventKeywords.None, entry.EventKeywords);
                Assert.AreEqual<int>((int)EventOpcode.Info, entry.Opcode);
                Assert.AreEqual<int>(0, entry.Version);
                Assert.AreEqual<int>(65331, entry.Task);
                Assert.AreEqual<string>("Test message Info 100", entry.Message);
                Assert.IsTrue(entry.Payload.ContainsKey("payload1"));
                Assert.AreEqual("Info", entry.Payload["payload1"]);
                Assert.IsTrue(entry.Payload.ContainsKey("payload2"));
                Assert.AreEqual((long)100, entry.Payload["payload2"]);
                Assert.AreEqual(processId, entry.ProcessId);
                Assert.AreEqual(threadId, entry.ThreadId);
                Assert.AreEqual(Guid.Empty, entry.ActivityId);
                Assert.AreEqual(Guid.Empty, entry.RelatedActivityId);
            }
        }

        [TestClass]
        public class when_receiving_event_with_payload_and_message_with_enviroment_activity_id : given_json_event_text_formatter
        {
            private Guid activityId;
            private Guid previousActivityId;

            protected override void Given()
            {
                base.Given();

                this.activityId = Guid.NewGuid();
                EventSource.SetCurrentThreadActivityId(this.activityId, out this.previousActivityId);
            }

            protected override void When()
            {
                logger.EventWithPayloadAndMessage("Info", 100);
            }

            protected override void OnCleanup()
            {
                base.OnCleanup();

                EventSource.SetCurrentThreadActivityId(this.previousActivityId);
            }

            [TestMethod]
            public void then_writes_event_data()
            {
                var entry = this.Entries.SingleOrDefault();

                Assert.IsNotNull(entry);
                Assert.IsFalse(this.RawOutput.StartsWith("{\r\n")); // No Formatting (Default)
                Assert.AreEqual<int>(TestEventSource.EventWithPayloadAndMessageId, entry.EventId);
                Assert.AreEqual<Guid>(TestEventSource.Log.Guid, entry.ProviderId);
                Assert.AreEqual<int>((int)EventLevel.Warning, entry.Level);
                Assert.AreEqual<long>((long)EventKeywords.None, entry.EventKeywords);
                Assert.AreEqual<int>((int)EventOpcode.Info, entry.Opcode);
                Assert.AreEqual<int>(0, entry.Version);
                Assert.AreEqual<int>(65331, entry.Task);
                Assert.AreEqual<string>("Test message Info 100", entry.Message);
                Assert.IsTrue(entry.Payload.ContainsKey("payload1"));
                Assert.AreEqual("Info", entry.Payload["payload1"]);
                Assert.IsTrue(entry.Payload.ContainsKey("payload2"));
                Assert.AreEqual((long)100, entry.Payload["payload2"]);
                Assert.AreEqual(this.activityId, entry.ActivityId);
                Assert.AreEqual(Guid.Empty, entry.RelatedActivityId);
            }
        }

        [TestClass]
        public class when_receiving_event_with_payload_and_message_with_enviroment_activity_id_and_related_activity_id : given_json_event_text_formatter
        {
            private Guid activityId;
            private Guid relatedActivityId;
            private Guid previousActivityId;

            protected override void Given()
            {
                base.Given();

                this.activityId = Guid.NewGuid();
                this.relatedActivityId = Guid.NewGuid();
                EventSource.SetCurrentThreadActivityId(this.activityId, out this.previousActivityId);
            }

            protected override void When()
            {
                logger.EventWithPayloadAndMessageAndRelatedActivityId(this.relatedActivityId, "Info", 100);
            }

            protected override void OnCleanup()
            {
                base.OnCleanup();

                EventSource.SetCurrentThreadActivityId(this.previousActivityId);
            }

            [TestMethod]
            public void then_writes_event_data()
            {
                var entry = this.Entries.SingleOrDefault();

                Assert.IsNotNull(entry);
                Assert.IsFalse(this.RawOutput.StartsWith("{\r\n")); // No Formatting (Default)
                Assert.AreEqual<int>(TestEventSource.EventWithPayloadAndMessageAndRelatedActivityIdId, entry.EventId);
                Assert.AreEqual<Guid>(TestEventSource.Log.Guid, entry.ProviderId);
                Assert.AreEqual<int>((int)EventLevel.Warning, entry.Level);
                Assert.AreEqual<long>((long)EventKeywords.None, entry.EventKeywords);
                Assert.AreEqual<int>((int)EventOpcode.Send, entry.Opcode);
                Assert.AreEqual<int>(0, entry.Version);
                Assert.AreEqual<int>((int)TestEventSource.Tasks.Other, entry.Task);
                Assert.AreEqual<string>("Test message Info 100", entry.Message);
                Assert.IsTrue(entry.Payload.ContainsKey("payload1"));
                Assert.AreEqual("Info", entry.Payload["payload1"]);
                Assert.IsTrue(entry.Payload.ContainsKey("payload2"));
                Assert.AreEqual((long)100, entry.Payload["payload2"]);
                Assert.AreEqual(this.activityId, entry.ActivityId);
                Assert.AreEqual(this.relatedActivityId, entry.RelatedActivityId);
            }
        }

        [TestClass]
        public class when_receiving_multiple_events : given_json_event_text_formatter
        {
            protected override void When()
            {
                logger.Informational("Info");
                logger.Write("test");
                logger.Error("error");
            }

            [TestMethod]
            public void then_writes_event_data()
            {
                var entries = this.Entries;

                Assert.AreEqual<int>(3, entries.Count());
                Assert.AreEqual<int>(TestEventSource.InformationalEventId, entries.ElementAt(0).EventId);
                Assert.AreEqual<int>(TestEventSource.VerboseEventId, entries.ElementAt(1).EventId);
                Assert.AreEqual<int>(TestEventSource.ErrorEventId, entries.ElementAt(2).EventId);
            }
        }

        [TestClass]
        public class when_receiving_event_with_message : given_json_event_text_formatter
        {
            protected override void Given()
            {
                base.Given();
                listener.Formatter = new JsonEventTextFormatter(EventTextFormatting.Indented);
            }

            protected override void When()
            {
                logger.Informational("Info");
                logger.Error("error");
            }

            [TestMethod]
            public void then_writes_indented_formatted_data()
            {
                Assert.IsNotNull(this.RawOutput);
                Assert.IsTrue(this.RawOutput.StartsWith("{\r\n"));
            }
        }

        [TestClass]
        public class when_receiving_multiple_events_in_parallel : given_json_event_text_formatter
        {
            private const int MaxLoggedEntries = 20;

            protected override void When()
            {
                Parallel.For(0, MaxLoggedEntries, i => logger.Informational("Info " + i));
            }

            [TestMethod]
            public void then_writes_multiple_events_are_formatted()
            {
                Assert.AreEqual<int>(MaxLoggedEntries, this.Entries.Count());
                Assert.IsTrue(this.Entries.All(e => e.EventId == TestEventSource.InformationalEventId));
            }
        }

        [TestClass]
        public class when_receiving_event_with_payload_and_null_content : given_json_event_text_formatter
        {
            protected override void When()
            {
                logger.Write(null);
            }

            [TestMethod]
            public void then_writes_event_data()
            {
                var entry = this.Entries.SingleOrDefault();

                Assert.IsNull(entry.Message);
                Assert.IsTrue(string.IsNullOrEmpty((string)entry.Payload.FirstOrDefault().Value));
            }
        }

        [TestClass]
        public class when_receiving_event_with_payload_and_null_formatted_message : given_json_event_text_formatter
        {
            protected override void When()
            {
                logger.EventWithPayloadAndMessage(null, 0);
            }

            [TestMethod]
            public void then_writes_event_data()
            {
                var entry = this.Entries.SingleOrDefault();

                Assert.AreEqual<string>("Test message  0", entry.Message);

                // Note in current version of EventSource: when observing through ETW, the payload will be string.Empty instead of null.
                // https://connect.microsoft.com/VisualStudio/feedback/details/783857/eventlistener-gets-non-null-or-null-string-value-depending-if-the-same-eventsource-is-being-observed-with-etw
                Assert.IsTrue(string.IsNullOrEmpty((string)entry.Payload.FirstOrDefault().Value));
            }
        }

        [TestClass]
        public class when_writing_null_entry : given_json_event_text_formatter
        {
            private ArgumentNullException exception;
            protected override void When()
            {
                using (var writer = new System.IO.StringWriter())
                {
                    try
                    {
                        this.formatter.WriteEvent(null, writer);
                        Assert.Fail("should have thrown");
                    }
                    catch (ArgumentNullException e)
                    {
                        this.exception = e;
                    }
                }
            }

            [TestMethod]
            public void then_throws_argument_null_exception()
            {
                Assert.IsNotNull(this.exception);
            }
        }

        [TestClass]
        public class when_writing_to_null_writer : given_json_event_text_formatter
        {
            private ArgumentNullException exception;
            protected override void When()
            {
                try
                {
                    formatter.WriteEvent(new EventEntry(Guid.NewGuid(), 0, string.Empty, new System.Collections.ObjectModel.ReadOnlyCollection<object>(new object[0]), DateTimeOffset.MaxValue, new Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Schema.EventSourceSchemaReader().GetSchema(logger).Values.First()), null);
                    Assert.Fail("should have thrown");
                }
                catch (ArgumentNullException e)
                {
                    this.exception = e;
                }
            }

            [TestMethod]
            public void then_throws_argument_null_exception()
            {
                Assert.IsNotNull(this.exception);
            }
        }

        [TestClass]
        public class when_receiving_event_with_multiple_payload_types : given_json_event_text_formatter
        {
            protected override void Given()
            {
                base.Given();
                this.listener.EnableEvents(MultipleTypesEventSource.Log, EventLevel.LogAlways);
            }

            protected override void When()
            {
                MultipleTypesEventSource.Log.ManyTypes(1, 2, 3, 4, 5, 6, 7, 8, 9, true, "test", Guid.NewGuid(), MultipleTypesEventSource.Color.Blue, 10);
            }

            protected override void OnCleanup()
            {
                this.listener.DisableEvents(MultipleTypesEventSource.Log);
                base.OnCleanup();
            }

            [TestMethod]
            public void then_writes_event_data()
            {
                var entries = this.Entries.SingleOrDefault();

                Assert.IsNotNull(entries);
            }
        }

        [TestClass]
        public class when_receiving_event_with_enum_in_payload : given_json_event_text_formatter
        {
            protected override void Given()
            {
                base.Given();
                listener.Formatter = new JsonEventTextFormatter(EventTextFormatting.Indented);
            }

            protected override void When()
            {
                logger.UsingEnumArguments(MyLongEnum.Value1, MyIntEnum.Value2);
            }

            [TestMethod]
            public void then_writes_integral_value()
            {
                StringAssert.Contains(this.RawOutput, "\"arg1\": 0");
                StringAssert.Contains(this.RawOutput, "\"arg2\": 1");
            }
        }

        [TestClass]
        public class when_receiving_event_with_short_enum_in_payload : given_json_event_text_formatter
        {
            protected override void Given()
            {
                base.Given();
                listener.Formatter = new JsonEventTextFormatter(EventTextFormatting.Indented);
                listener.EnableEvents(DifferentEnumsEventSource.Log, EventLevel.LogAlways, Keywords.All);
            }

            protected override void When()
            {
                DifferentEnumsEventSource.Log.UsingEnumArguments(MyLongEnum.Value1, MyIntEnum.Value2, MyShortEnum.Value3);
            }

            [TestMethod]
            public void then_writes_integral_value()
            {
                StringAssert.Contains(this.RawOutput, "\"arg1\": 0");
                StringAssert.Contains(this.RawOutput, "\"arg2\": 1");
                StringAssert.Contains(this.RawOutput, "\"arg3\": 2");
            }
        }

        [TestClass]
        public class when_receiving_event_with_multiple_enums_in_payload : given_json_event_text_formatter
        {
            protected override void Given()
            {
                base.Given();
                listener.Formatter = new JsonEventTextFormatter(EventTextFormatting.Indented);
                listener.EnableEvents(DifferentEnumsEventSource.Log, EventLevel.LogAlways, Keywords.All);
            }

            protected override void When()
            {
                DifferentEnumsEventSource.Log.UsingAllEnumArguments(MyLongEnum.Value1, MyIntEnum.Value2, MyShortEnum.Value3,
                    MyByteEnum.Value1, MySByteEnum.Value2, MyUShortEnum.Value3, MyUIntEnum.Value1, MyULongEnum.Value2);
            }

            [TestMethod]
            public void then_writes_integral_value()
            {
                StringAssert.Contains(this.RawOutput, "\"arg1\": 0");
                StringAssert.Contains(this.RawOutput, "\"arg2\": 1");
                StringAssert.Contains(this.RawOutput, "\"arg3\": 2");
                StringAssert.Contains(this.RawOutput, "\"arg4\": 0");
                StringAssert.Contains(this.RawOutput, "\"arg5\": 1");
                StringAssert.Contains(this.RawOutput, "\"arg6\": 2");
                StringAssert.Contains(this.RawOutput, "\"arg7\": 0");
                StringAssert.Contains(this.RawOutput, "\"arg8\": 1");
            }
        }
    }
}
