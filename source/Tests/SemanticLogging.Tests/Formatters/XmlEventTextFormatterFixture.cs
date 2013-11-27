// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Formatters;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.TestObjects;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.Formatters
{
    [TestClass]
    public class given_xml_event_text_formatter_configuration : ContextBase
    {
        [TestMethod]
        public void when_creating_formatter_with_default_values()
        {
            var formatter = new XmlEventTextFormatter();
            Assert.IsNull(formatter.DateTimeFormat);
            Assert.AreEqual(EventTextFormatting.None, formatter.Formatting);
        }

        [TestMethod]
        public void when_creating_formatter_with_specific_values()
        {
            var formatter = new XmlEventTextFormatter(EventTextFormatting.Indented) { DateTimeFormat = "R" };
            Assert.AreEqual("R", formatter.DateTimeFormat);
            Assert.AreEqual(EventTextFormatting.Indented, formatter.Formatting);
        }
    }

    public abstract class given_xml_event_text_formatter : ContextBase
    {
        private const string EventNamespace = "http://schemas.microsoft.com/win/2004/08/events/event";
        private const string EventNS = "{" + EventNamespace + "}";
        protected InMemoryEventListener listener;
        protected TestEventSource logger = TestEventSource.Log;
        private IEnumerable<XElement> entries;
        protected XmlEventTextFormatter formatter;

        protected override void Given()
        {
            formatter = new XmlEventTextFormatter(EventTextFormatting.Indented);
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

        protected IEnumerable<XElement> Entries
        {
            get { return entries ?? (entries = XDocument.Parse("<Events>" + this.RawOutput + "</Events>").Root.Elements()); }
        }

        [TestClass]
        public class when_receiving_event_with_payload_and_message : given_xml_event_text_formatter
        {
            protected override void When()
            {
                logger.EventWithPayloadAndMessage("Info", 100);
            }

            [TestMethod]
            public void then_writes_event_data()
            {
                var element = this.Entries.Single();

                var provider = element.Descendants(EventNS + "Provider").Single();
                var eventId = element.Descendants(EventNS + "EventID").Single();
                var version = element.Descendants(EventNS + "Version").Single();
                var level = element.Descendants(EventNS + "Level").Single();
                var task = element.Descendants(EventNS + "Task").Single();
                var opcode = element.Descendants(EventNS + "Opcode").Single();
                var keywords = element.Descendants(EventNS + "Keywords").Single();
                var timeCreated = element.Descendants(EventNS + "TimeCreated").Single();
                var payload = element.Descendants(EventNS + "EventData").Single();
                var message = element.Descendants(EventNS + "Message").Single();
                var correlation = element.Descendants(EventNS + "Correlation").Single();

                Assert.AreEqual<Guid>(TestEventSource.Log.Guid, Guid.Parse(provider.Attribute("Guid").Value));
                Assert.AreEqual<int>(TestEventSource.EventWithPayloadAndMessageId, Convert.ToInt32(eventId.Value));
                Assert.AreEqual<byte>(0, Convert.ToByte(version.Value));
                Assert.AreEqual<int>((int)EventLevel.Warning, Int32.Parse(level.Value));
                Assert.AreEqual<int>(65331, Int32.Parse(task.Value));
                Assert.AreEqual<long>((long)EventKeywords.None, Int64.Parse(keywords.Value.Replace("0x", string.Empty)));
                Assert.AreEqual<int>((int)EventOpcode.Info, Int32.Parse(opcode.Value));
                DateTime dt;
                Assert.IsTrue(DateTime.TryParseExact(timeCreated.Attribute("SystemTime").Value, formatter.DateTimeFormat ?? EventEntry.DefaultDateTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt));
                Assert.AreEqual(2, payload.Elements().Count());
                Assert.AreEqual("payload1", payload.Elements().First().Attribute("Name").Value);
                Assert.AreEqual("Info", payload.Elements().First().Value);
                Assert.AreEqual("payload2", payload.Elements().Last().Attribute("Name").Value);
                Assert.AreEqual("100", payload.Elements().Last().Value);
                Assert.AreEqual("Test message Info 100", message.Value);
                Assert.AreEqual(Guid.Empty, Guid.Parse(correlation.Attribute("ActivityID").Value));
                Assert.AreEqual(Guid.Empty, Guid.Parse(correlation.Attribute("RelatedActivityID").Value));
            }
        }

        [TestClass]
        public class when_receiving_event_with_payload_and_message_with_ambient_activity_id : given_xml_event_text_formatter
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
                var element = this.Entries.Single();

                var provider = element.Descendants(EventNS + "Provider").Single();
                var eventId = element.Descendants(EventNS + "EventID").Single();
                var version = element.Descendants(EventNS + "Version").Single();
                var level = element.Descendants(EventNS + "Level").Single();
                var task = element.Descendants(EventNS + "Task").Single();
                var opcode = element.Descendants(EventNS + "Opcode").Single();
                var keywords = element.Descendants(EventNS + "Keywords").Single();
                var timeCreated = element.Descendants(EventNS + "TimeCreated").Single();
                var payload = element.Descendants(EventNS + "EventData").Single();
                var message = element.Descendants(EventNS + "Message").Single();
                var correlation = element.Descendants(EventNS + "Correlation").Single();

                Assert.AreEqual<Guid>(TestEventSource.Log.Guid, Guid.Parse(provider.Attribute("Guid").Value));
                Assert.AreEqual<int>(TestEventSource.EventWithPayloadAndMessageId, Convert.ToInt32(eventId.Value));
                Assert.AreEqual<byte>(0, Convert.ToByte(version.Value));
                Assert.AreEqual<int>((int)EventLevel.Warning, Int32.Parse(level.Value));
                Assert.AreEqual<int>(65331, Int32.Parse(task.Value));
                Assert.AreEqual<long>((long)EventKeywords.None, Int64.Parse(keywords.Value.Replace("0x", string.Empty)));
                Assert.AreEqual<int>((int)EventOpcode.Info, Int32.Parse(opcode.Value));
                DateTime dt;
                Assert.IsTrue(DateTime.TryParseExact(timeCreated.Attribute("SystemTime").Value, formatter.DateTimeFormat ?? EventEntry.DefaultDateTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt));
                Assert.AreEqual(2, payload.Elements().Count());
                Assert.AreEqual("payload1", payload.Elements().First().Attribute("Name").Value);
                Assert.AreEqual("Info", payload.Elements().First().Value);
                Assert.AreEqual("payload2", payload.Elements().Last().Attribute("Name").Value);
                Assert.AreEqual("100", payload.Elements().Last().Value);
                Assert.AreEqual("Test message Info 100", message.Value);
                Assert.AreEqual(this.activityId, Guid.Parse(correlation.Attribute("ActivityID").Value));
                Assert.AreEqual(Guid.Empty, Guid.Parse(correlation.Attribute("RelatedActivityID").Value));
            }
        }

        [TestClass]
        public class when_receiving_event_with_payload_and_message_and_related_activity_id_with_ambient_activity_id : given_xml_event_text_formatter
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
                var element = this.Entries.Single();

                var provider = element.Descendants(EventNS + "Provider").Single();
                var eventId = element.Descendants(EventNS + "EventID").Single();
                var version = element.Descendants(EventNS + "Version").Single();
                var level = element.Descendants(EventNS + "Level").Single();
                var task = element.Descendants(EventNS + "Task").Single();
                var opcode = element.Descendants(EventNS + "Opcode").Single();
                var keywords = element.Descendants(EventNS + "Keywords").Single();
                var timeCreated = element.Descendants(EventNS + "TimeCreated").Single();
                var payload = element.Descendants(EventNS + "EventData").Single();
                var message = element.Descendants(EventNS + "Message").Single();
                var correlation = element.Descendants(EventNS + "Correlation").Single();

                Assert.AreEqual<Guid>(TestEventSource.Log.Guid, Guid.Parse(provider.Attribute("Guid").Value));
                Assert.AreEqual<int>(TestEventSource.EventWithPayloadAndMessageAndRelatedActivityIdId, Convert.ToInt32(eventId.Value));
                Assert.AreEqual<byte>(0, Convert.ToByte(version.Value));
                Assert.AreEqual<int>((int)EventLevel.Warning, Int32.Parse(level.Value));
                Assert.AreEqual<int>((int)TestEventSource.Tasks.Other, Int32.Parse(task.Value));
                Assert.AreEqual<long>((long)EventKeywords.None, Int64.Parse(keywords.Value.Replace("0x", string.Empty)));
                Assert.AreEqual<int>((int)EventOpcode.Send, Int32.Parse(opcode.Value));
                DateTime dt;
                Assert.IsTrue(DateTime.TryParseExact(timeCreated.Attribute("SystemTime").Value, formatter.DateTimeFormat ?? EventEntry.DefaultDateTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt));
                Assert.AreEqual(2, payload.Elements().Count());
                Assert.AreEqual("payload1", payload.Elements().First().Attribute("Name").Value);
                Assert.AreEqual("Info", payload.Elements().First().Value);
                Assert.AreEqual("payload2", payload.Elements().Last().Attribute("Name").Value);
                Assert.AreEqual("100", payload.Elements().Last().Value);
                Assert.AreEqual("Test message Info 100", message.Value);
                Assert.AreEqual(this.activityId, Guid.Parse(correlation.Attribute("ActivityID").Value));
                Assert.AreEqual(this.relatedActivityId, Guid.Parse(correlation.Attribute("RelatedActivityID").Value));
            }
        }

        [TestClass]
        public class when_receiving_event_with_payload_and_xml_content : given_xml_event_text_formatter
        {
            private const string Content = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<test/>";

            protected override void When()
            {
                logger.Write(Content);
            }

            [TestMethod]
            public void then_writes_event_data()
            {
                var element = this.Entries.Single();

                var payload = element.Descendants(EventNS + "EventData").Single();
                Assert.AreEqual("message", payload.Elements().First().Attribute("Name").Value);
                Assert.AreEqual(Content, payload.Elements().First().Value);
            }
        }

        [TestClass]
        public class when_receiving_multiple_events_with_payload_and_message : given_xml_event_text_formatter
        {
            protected override void When()
            {
                logger.Informational("info");
                logger.Error("error");
            }

            [TestMethod]
            public void then_writes_multiple_events_data()
            {
                Assert.AreEqual(2, this.Entries.Count());

                var payload1 = this.Entries.First().Descendants(EventNS + "EventData").Single();
                Assert.AreEqual("message", payload1.Elements().First().Attribute("Name").Value);
                Assert.AreEqual("info", payload1.Elements().First().Value);

                var payload2 = this.Entries.Last().Descendants(EventNS + "EventData").Single();
                Assert.AreEqual("message", payload2.Elements().First().Attribute("Name").Value);
                Assert.AreEqual("error", payload2.Elements().First().Value);
            }
        }

        [TestClass]
        public class when_receiving_event_with_enum_in_payload : given_xml_event_text_formatter
        {
            protected override void When()
            {
                logger.UsingEnumArguments(MyLongEnum.Value1, MyIntEnum.Value2);
            }

            [TestMethod]
            public void then_writes_integral_value()
            {
                var entry = this.Entries.Single();

                var payload = entry.Descendants(EventNS + "EventData").Single();
                Assert.AreEqual("arg1", payload.Elements().First().Attribute("Name").Value);
                Assert.AreEqual<long>(0, (long)payload.Elements().First());

                Assert.AreEqual("arg2", payload.Elements().ElementAt(1).Attribute("Name").Value);
                Assert.AreEqual<int>(1, (int)payload.Elements().ElementAt(1));
            }
        }

        [TestClass]
        public class when_receiving_event_with_short_enums_in_payload : given_xml_event_text_formatter
        {
            protected override void When()
            {
                listener.EnableEvents(DifferentEnumsEventSource.Log, EventLevel.LogAlways, Keywords.All);
                DifferentEnumsEventSource.Log.UsingEnumArguments(MyLongEnum.Value1, MyIntEnum.Value2, MyShortEnum.Value3);
            }

            [TestMethod]
            public void then_writes_integral_value()
            {
                var entry = this.Entries.Single();

                var payload = entry.Descendants(EventNS + "EventData").Single();
                Assert.AreEqual("arg1", payload.Elements().First().Attribute("Name").Value);
                Assert.AreEqual<long>(0, (long)payload.Elements().First());

                Assert.AreEqual("arg2", payload.Elements().ElementAt(1).Attribute("Name").Value);
                Assert.AreEqual<int>(1, (int)payload.Elements().ElementAt(1));

                Assert.AreEqual("arg3", payload.Elements().ElementAt(2).Attribute("Name").Value);
                Assert.AreEqual<short>(2, (short)payload.Elements().ElementAt(2));
            }
        }

        [TestClass]
        public class when_receiving_event_with_multiple_enums_in_payload : given_xml_event_text_formatter
        {
            protected override void When()
            {
                listener.EnableEvents(DifferentEnumsEventSource.Log, EventLevel.LogAlways, Keywords.All);
                DifferentEnumsEventSource.Log.UsingAllEnumArguments(MyLongEnum.Value1, MyIntEnum.Value2, MyShortEnum.Value3,
                    MyByteEnum.Value1, MySByteEnum.Value2, MyUShortEnum.Value3, MyUIntEnum.Value1, MyULongEnum.Value2);
            }

            [TestMethod]
            public void then_writes_integral_value()
            {
                var entry = this.Entries.Single();

                var payload = entry.Descendants(EventNS + "EventData").Single();
                Assert.AreEqual("arg1", payload.Elements().First().Attribute("Name").Value);
                Assert.AreEqual<long>(0, (long)payload.Elements().First());

                Assert.AreEqual("arg2", payload.Elements().ElementAt(1).Attribute("Name").Value);
                Assert.AreEqual<int>(1, (int)payload.Elements().ElementAt(1));

                Assert.AreEqual("arg3", payload.Elements().ElementAt(2).Attribute("Name").Value);
                Assert.AreEqual<short>(2, (short)payload.Elements().ElementAt(2));

                Assert.AreEqual("arg4", payload.Elements().ElementAt(3).Attribute("Name").Value);
                Assert.AreEqual<byte>(0, Convert.ToByte(payload.Elements().ElementAt(3).Value));

                Assert.AreEqual("arg5", payload.Elements().ElementAt(4).Attribute("Name").Value);
                Assert.AreEqual<sbyte>(1, (sbyte)payload.Elements().ElementAt(4));

                Assert.AreEqual("arg6", payload.Elements().ElementAt(5).Attribute("Name").Value);
                Assert.AreEqual<ushort>(2, Convert.ToUInt16(payload.Elements().ElementAt(5).Value));

                Assert.AreEqual("arg7", payload.Elements().ElementAt(6).Attribute("Name").Value);
                Assert.AreEqual<uint>(0, (uint)payload.Elements().ElementAt(6));

                Assert.AreEqual("arg8", payload.Elements().ElementAt(7).Attribute("Name").Value);
                Assert.AreEqual<ulong>(1, (ulong)payload.Elements().ElementAt(7));
            }
        }

        [TestClass]
        public class when_setting_indentation_none : given_xml_event_text_formatter
        {
            protected override void Given()
            {
                listener = new InMemoryEventListener() { Formatter = new XmlEventTextFormatter(EventTextFormatting.None) };
                listener.EnableEvents(logger, EventLevel.LogAlways);
            }

            protected override void When()
            {
                logger.Informational("info");
                logger.Error("error");
            }

            [TestMethod]
            public void then_writes_indented_xml_data()
            {
                Assert.AreEqual(-1, this.RawOutput.IndexOf("\r\n")); // With no Indent, no CRs
            }
        }

        [TestClass]
        public class when_validating_with_event_schema : given_xml_event_text_formatter
        {
            protected override void Given()
            {
                base.Given();
                listener.EnableEvents(MyCompanyEventSource.Log, EventLevel.LogAlways, Keywords.All);
            }

            protected override void When()
            {
                MyCompanyEventSource.Log.PageStart(10, "test");
            }

            [TestMethod]
            public void then_generates_event_schema_compliant_xmldata()
            {
                XmlSchemaSet schemas = new XmlSchemaSet();
                schemas.Add(EventNamespace, XmlReader.Create("Event.xsd", new XmlReaderSettings() { CloseInput = true }));

                string error = null;
                XDocument document = XDocument.Parse(this.RawOutput);
                document.Validate(schemas, (o, e) => error = e.Message);

                Assert.IsNull(error, error);
            }
        }

        [TestClass]
        public class when_receiving_event_with_payload_and_null_content : given_xml_event_text_formatter
        {
            protected override void When()
            {
                logger.Write(null);
            }

            [TestMethod]
            public void then_writes_event_data()
            {
                var message = this.Entries.Single().Descendants(EventNS + "Message").Single();
                Assert.AreEqual(string.Empty, message.Value);
            }
        }

        [TestClass]
        public class when_receiving_event_with_payload_and_null_formatted_message : given_xml_event_text_formatter
        {
            protected override void When()
            {
                logger.EventWithPayloadAndMessage(null, 0);
            }

            [TestMethod]
            public void then_writes_event_data()
            {
                var message = this.Entries.Single().Descendants(EventNS + "Message").Single();
                Assert.AreEqual("Test message  0", message.Value);
            }
        }

        [TestClass]
        public class when_writing_null_entry : given_xml_event_text_formatter
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
        public class when_writing_to_null_writer : given_xml_event_text_formatter
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
        public class when_receiving_event_with_multiple_payload_types : given_xml_event_text_formatter
        {
            private Guid guidValue;

            protected override void Given()
            {
                base.Given();
                this.listener.EnableEvents(MultipleTypesEventSource.Log, EventLevel.LogAlways);
            }

            protected override void When()
            {
                this.guidValue = Guid.NewGuid();
                MultipleTypesEventSource.Log.ManyTypes(1, 2, 3, 4, 5, 6, 7, 8, 9, true, "test", this.guidValue, MultipleTypesEventSource.Color.Blue, 10);
            }

            protected override void OnCleanup()
            {
                this.listener.DisableEvents(MultipleTypesEventSource.Log);
                base.OnCleanup();
            }

            [TestMethod]
            public void then_writes_event_data()
            {
                var element = this.Entries.Single();
                var payload = element.Descendants(EventNS + "EventData").SingleOrDefault();

                Assert.IsNotNull(payload);
                Assert.IsTrue(payload.Elements().Any());
            }

            [TestMethod]
            public void then_writes_guid_value()
            {
                var entry = this.Entries.Single();

                var payload = entry.Descendants(EventNS + "EventData").Single();
                var value = payload.Elements().Single(e => string.Equals(e.Attribute("Name").Value, "arg14", StringComparison.Ordinal)).Value;
                Assert.AreEqual<Guid>(this.guidValue, Guid.Parse(value));
            }
        }
    }
}
