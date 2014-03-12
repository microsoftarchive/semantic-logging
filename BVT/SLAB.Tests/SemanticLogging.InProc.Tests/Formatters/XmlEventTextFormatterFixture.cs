// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Formatters;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.InProc.Tests.TestObjects;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.Shared.TestObjects;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.Shared.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics.Tracing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.InProc.Tests.Formatters
{
    [TestClass]
    public class XmlEventTextFormatterFixture
    {
        [TestMethod]
        public void EventWithPayloadKeywrdsNoMsgIndentedInXml()
        {
            var logger = MockEventSrcForXml.Logger;
            var formatter = new XmlEventTextFormatter(EventTextFormatting.Indented);

            string rawOutput = string.Empty;
            using (var listener = new InMemoryEventListener(formatter))
            {
                listener.EnableEvents(logger, EventLevel.LogAlways, MockEventSrcForXml.Keywords.Errors);
                try
                {
                    logger.UsingKeywords(MockEventSrcForXml.LogMessage, long.MaxValue);
                    rawOutput = listener.ToString();
                }
                finally
                {
                    listener.DisableEvents(logger);
                }
            }

            Assert.AreEqual(21, rawOutput.Split('\n').Length);
            var entries = XDocument.Parse("<Events>" + rawOutput + "</Events>").Root.Elements();
            XmlFormattedEntry.Fill(entries.Single());
            Assert.AreEqual<Guid>(EventSource.GetGuid(typeof(MockEventSrcForXml)), Guid.Parse(XmlFormattedEntry.Provider.Attribute("Guid").Value));
            Assert.AreEqual<int>(MockEventSrcForXml.UsingKeywordsEventID, Convert.ToInt32(XmlFormattedEntry.EventId.Value));
            Assert.AreEqual<byte>(0, Convert.ToByte(XmlFormattedEntry.Version.Value));
            Assert.AreEqual<int>((int)EventLevel.Informational, Int32.Parse(XmlFormattedEntry.Level.Value));
            Assert.AreEqual<int>((int)EventTask.None, Int32.Parse(XmlFormattedEntry.Task.Value));
            Assert.AreEqual<long>((long)MockEventSrcForXml.Keywords.Errors, Int64.Parse(XmlFormattedEntry.Keywords.Value.Replace("0x", string.Empty)));
            Assert.AreEqual<int>((int)EventOpcode.Start, Int32.Parse(XmlFormattedEntry.Opcode.Value));
            DateTime dt;
            Assert.IsTrue(DateTime.TryParseExact(XmlFormattedEntry.TimeCreated.Attribute("SystemTime").Value, EventEntry.DefaultDateTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt));
            Assert.AreEqual(2, XmlFormattedEntry.Payload.Elements().Count());
            Assert.AreEqual("message", XmlFormattedEntry.Payload.Elements().First().Attribute("Name").Value);
            Assert.AreEqual(MockEventSrcForXml.LogMessage, XmlFormattedEntry.Payload.Elements().First().Value);
            Assert.AreEqual("longArg", XmlFormattedEntry.Payload.Elements().Last().Attribute("Name").Value);
            Assert.AreEqual(long.MaxValue.ToString(), XmlFormattedEntry.Payload.Elements().Last().Value);
        }

        [TestMethod]
        public void EventWithPayloadKeywrdsNoMsgInXml()
        {
            var logger = MockEventSrcForXml.Logger;
            var formatter = new XmlEventTextFormatter();

            string rawOutput = string.Empty;
            using (var listener = new InMemoryEventListener(formatter))
            {
                listener.EnableEvents(logger, EventLevel.LogAlways, MockEventSrcForXml.Keywords.Errors);
                try
                {
                    logger.UsingKeywords(MockEventSrcForXml.LogMessage, long.MaxValue);
                    rawOutput = listener.ToString();
                }
                finally
                {
                    listener.DisableEvents(logger);
                }
            }

            Assert.AreEqual(-1, rawOutput.IndexOf("\r\n"));
            var entries = XDocument.Parse("<Events>" + rawOutput + "</Events>").Root.Elements();
            XmlFormattedEntry.Fill(entries.Single());
            Assert.AreEqual<Guid>(EventSource.GetGuid(typeof(MockEventSrcForXml)), Guid.Parse(XmlFormattedEntry.Provider.Attribute("Guid").Value));
            Assert.AreEqual<int>(MockEventSrcForXml.UsingKeywordsEventID, Convert.ToInt32(XmlFormattedEntry.EventId.Value));
            Assert.AreEqual<byte>(0, Convert.ToByte(XmlFormattedEntry.Version.Value));
            Assert.AreEqual<int>((int)EventLevel.Informational, Int32.Parse(XmlFormattedEntry.Level.Value));
            Assert.AreEqual<int>((int)EventTask.None, Int32.Parse(XmlFormattedEntry.Task.Value));
            Assert.AreEqual<long>((long)MockEventSrcForXml.Keywords.Errors, Int64.Parse(XmlFormattedEntry.Keywords.Value.Replace("0x", string.Empty)));
            Assert.AreEqual<int>((int)EventOpcode.Start, Int32.Parse(XmlFormattedEntry.Opcode.Value));
            DateTime dt;
            Assert.IsTrue(DateTime.TryParseExact(XmlFormattedEntry.TimeCreated.Attribute("SystemTime").Value, EventEntry.DefaultDateTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt));
            Assert.AreEqual(2, XmlFormattedEntry.Payload.Elements().Count());
            Assert.AreEqual("message", XmlFormattedEntry.Payload.Elements().First().Attribute("Name").Value);
            Assert.AreEqual(MockEventSrcForXml.LogMessage, XmlFormattedEntry.Payload.Elements().First().Value);
            Assert.AreEqual("longArg", XmlFormattedEntry.Payload.Elements().Last().Attribute("Name").Value);
            Assert.AreEqual(long.MaxValue.ToString(), XmlFormattedEntry.Payload.Elements().Last().Value);
        }

        [TestMethod]
        public void EventWithPayloadAndMessageInXml()
        {
            var logger = MockEventSrcForXml.Logger;
            var formatter = new XmlEventTextFormatter();

            string rawOutput = string.Empty;
            using (var listener = new InMemoryEventListener(formatter))
            {
                listener.EnableEvents(logger, EventLevel.LogAlways, MockEventSrcForXml.Keywords.Errors);
                try
                {
                    logger.LogUsingMessage(MockEventSrcForXml.LogMessage);
                    rawOutput = Encoding.Default.GetString(listener.Stream.ToArray());
                }
                finally
                {
                    listener.DisableEvents(logger);
                }
            }

            var entries = XDocument.Parse("<Events>" + rawOutput + "</Events>").Root.Elements();
            XmlFormattedEntry.Fill(entries.First());
            Assert.IsFalse(rawOutput.StartsWith("{\r\n")); // No Formatting (Default)
            Assert.AreEqual<Guid>(EventSource.GetGuid(typeof(MockEventSrcForXml)), Guid.Parse(XmlFormattedEntry.Provider.Attribute("Guid").Value));
            Assert.AreEqual<int>(MockEventSrcForXml.LogUsingMessageEventID, Convert.ToInt32(XmlFormattedEntry.EventId.Value));
            Assert.AreEqual<byte>(0, Convert.ToByte(XmlFormattedEntry.Version.Value));
            Assert.AreEqual<int>((int)EventLevel.Informational, Int32.Parse(XmlFormattedEntry.Level.Value));
            Assert.AreEqual<int>((int)EventTask.None, Int32.Parse(XmlFormattedEntry.Task.Value));
            Assert.AreEqual<long>((long)EventKeywords.None, Int64.Parse(XmlFormattedEntry.Keywords.Value.Replace("0x", string.Empty)));
            Assert.AreEqual<int>((int)EventOpcode.Start, Int32.Parse(XmlFormattedEntry.Opcode.Value));
            Assert.AreEqual<byte>(0, Convert.ToByte(XmlFormattedEntry.Version.Value));
            DateTime dt;
            Assert.IsTrue(DateTime.TryParseExact(XmlFormattedEntry.TimeCreated.Attribute("SystemTime").Value, EventEntry.DefaultDateTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt));
            Assert.AreEqual(1, XmlFormattedEntry.Payload.Elements().Count());
            Assert.AreEqual("message", XmlFormattedEntry.Payload.Elements().First().Attribute("Name").Value);
            Assert.AreEqual(MockEventSrcForXml.LogMessage, XmlFormattedEntry.Payload.Elements().First().Value);
            Assert.AreEqual(MockEventSrcForXml.LogMessage, XmlFormattedEntry.Message.Elements().First().Value);
        }

        [TestMethod]
        public void EventWithNullPayloadInXml()
        {
            var formatter = new XmlEventTextFormatter();
            var logger = MockEventSrcForXml.Logger;

            string rawOutput = string.Empty;
            using (var listener = new InMemoryEventListener(formatter))
            {
                listener.EnableEvents(logger, EventLevel.LogAlways, MockEventSrcForXml.Keywords.Errors);
                try
                {
                    logger.LogUsingMessage(null);
                    rawOutput = Encoding.Default.GetString(listener.Stream.ToArray());
                }
                finally
                {
                    listener.DisableEvents(logger);
                }
            }

            var entries = XDocument.Parse("<Events>" + rawOutput + "</Events>").Root.Elements();
            XmlFormattedEntry.Fill(entries.First());
            Assert.IsFalse(rawOutput.StartsWith("{\r\n")); // No Formatting (Default)
            Assert.AreEqual<Guid>(EventSource.GetGuid(typeof(MockEventSrcForXml)), Guid.Parse(XmlFormattedEntry.Provider.Attribute("Guid").Value));
            Assert.AreEqual<int>(MockEventSrcForXml.LogUsingMessageEventID, Convert.ToInt32(XmlFormattedEntry.EventId.Value));
            Assert.AreEqual<byte>(0, Convert.ToByte(XmlFormattedEntry.Version.Value));
            Assert.AreEqual<int>((int)EventLevel.Informational, Int32.Parse(XmlFormattedEntry.Level.Value));
            Assert.AreEqual<int>((int)EventTask.None, Int32.Parse(XmlFormattedEntry.Task.Value));
            Assert.AreEqual<long>((long)EventKeywords.None, Int64.Parse(XmlFormattedEntry.Keywords.Value.Replace("0x", string.Empty)));
            Assert.AreEqual<int>((int)EventOpcode.Start, Int32.Parse(XmlFormattedEntry.Opcode.Value));
            Assert.AreEqual<byte>(0, Convert.ToByte(XmlFormattedEntry.Version.Value));
            DateTime dt;
            Assert.IsTrue(DateTime.TryParseExact(XmlFormattedEntry.TimeCreated.Attribute("SystemTime").Value, EventEntry.DefaultDateTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt));
        }

        [TestMethod]
        public void EventWithNullPayloadInFormattedMessageInXml()
        {
            var formatter = new XmlEventTextFormatter();
            var logger = MockEventSrcForXml.Logger;

            string rawOutput = string.Empty;
            using (var listener = new InMemoryEventListener(formatter))
            {
                listener.EnableEvents(logger, EventLevel.LogAlways, MockEventSrcForXml.Keywords.Errors);
                try
                {
                    logger.LogUsingMessageFormat(null);
                    rawOutput = Encoding.Default.GetString(listener.Stream.ToArray());
                }
                finally
                {
                    listener.DisableEvents(logger);
                }
            }

            var entries = XDocument.Parse("<Events>" + rawOutput + "</Events>").Root.Elements();
            XmlFormattedEntry.Fill(entries.First());
            Assert.IsFalse(rawOutput.StartsWith("{\r\n")); // No Formatting (Default)
            Assert.AreEqual<Guid>(EventSource.GetGuid(typeof(MockEventSrcForXml)), Guid.Parse(XmlFormattedEntry.Provider.Attribute("Guid").Value));
            Assert.AreEqual<int>(MockEventSrcForXml.LogUsingMessageFormatEventID, Convert.ToInt32(XmlFormattedEntry.EventId.Value));
            Assert.AreEqual<byte>(0, Convert.ToByte(XmlFormattedEntry.Version.Value));
            Assert.AreEqual<int>((int)EventLevel.Informational, Int32.Parse(XmlFormattedEntry.Level.Value));
            Assert.AreEqual<int>((int)EventTask.None, Int32.Parse(XmlFormattedEntry.Task.Value));
            Assert.AreEqual<long>((long)EventKeywords.None, Int64.Parse(XmlFormattedEntry.Keywords.Value.Replace("0x", string.Empty)));
            Assert.AreEqual<int>((int)EventOpcode.Start, Int32.Parse(XmlFormattedEntry.Opcode.Value));
            Assert.AreEqual<byte>(0, Convert.ToByte(XmlFormattedEntry.Version.Value));
            DateTime dt;
            Assert.IsTrue(DateTime.TryParseExact(XmlFormattedEntry.TimeCreated.Attribute("SystemTime").Value, EventEntry.DefaultDateTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt));
        }

        [TestMethod]
        public void EventWithPayloadAndMessageFormatInXml()
        {
            var formatter = new XmlEventTextFormatter();
            var logger = MockEventSrcForXml.Logger;

            string rawOutput = string.Empty;
            using (var listener = new InMemoryEventListener(formatter))
            {
                listener.EnableEvents(logger, EventLevel.LogAlways, MockEventSrcForXml.Keywords.Errors);
                try
                {
                    logger.LogUsingMessageFormat("<This is a test />");
                    rawOutput = Encoding.Default.GetString(listener.Stream.ToArray());
                }
                finally
                {
                    listener.DisableEvents(logger);
                }
            }

            var entries = XDocument.Parse("<Events>" + rawOutput + "</Events>").Root.Elements();
            XmlFormattedEntry.Fill(entries.First());
            Assert.IsFalse(rawOutput.StartsWith("{\r\n")); // No Formatting (Default)
            Assert.AreEqual<Guid>(EventSource.GetGuid(typeof(MockEventSrcForXml)), Guid.Parse(XmlFormattedEntry.Provider.Attribute("Guid").Value));
            Assert.AreEqual<int>(MockEventSrcForXml.LogUsingMessageFormatEventID, Convert.ToInt32(XmlFormattedEntry.EventId.Value));
            Assert.AreEqual<byte>(0, Convert.ToByte(XmlFormattedEntry.Version.Value));
            Assert.AreEqual<int>((int)EventLevel.Informational, Int32.Parse(XmlFormattedEntry.Level.Value));
            Assert.AreEqual<int>((int)EventTask.None, Int32.Parse(XmlFormattedEntry.Task.Value));
            Assert.AreEqual<long>((long)EventKeywords.None, Int64.Parse(XmlFormattedEntry.Keywords.Value.Replace("0x", string.Empty)));
            Assert.AreEqual<int>((int)EventOpcode.Start, Int32.Parse(XmlFormattedEntry.Opcode.Value));
            Assert.AreEqual<byte>(0, Convert.ToByte(XmlFormattedEntry.Version.Value));
            DateTime dt;
            Assert.IsTrue(DateTime.TryParseExact(XmlFormattedEntry.TimeCreated.Attribute("SystemTime").Value, EventEntry.DefaultDateTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt));
            Assert.AreEqual(1, XmlFormattedEntry.Payload.Elements().Count());
            Assert.AreEqual("message", XmlFormattedEntry.Payload.Elements().First().Attribute("Name").Value);
            Assert.AreEqual("<This is a test />", XmlFormattedEntry.Payload.Elements().First().Value);
            Assert.AreEqual("<This is a test />", XmlFormattedEntry.Message.Elements().First().Value);
        }

        [TestMethod]
        public void EventWithPayloadAndMessageWithDateTimeFormatInXml()
        {
            var formatter = new XmlEventTextFormatter();
            formatter.DateTimeFormat = "dd/MM/yyyy";
            var logger = MockEventSrcForXml.Logger;

            string rawOutput = string.Empty;
            using (var listener = new InMemoryEventListener(formatter))
            {
                listener.EnableEvents(logger, EventLevel.LogAlways, MockEventSrcForXml.Keywords.Errors);
                try
                {
                    logger.LogUsingMessage(MockEventSrcForXml.LogMessage);
                    rawOutput = Encoding.Default.GetString(listener.Stream.ToArray());
                }
                finally
                {
                    listener.DisableEvents(logger);
                }
            }

            var entries = XDocument.Parse("<Events>" + rawOutput + "</Events>").Root.Elements();
            XmlFormattedEntry.Fill(entries.First());
            Assert.IsFalse(rawOutput.StartsWith("{\r\n")); // No Formatting (Default)
            string today = System.DateTime.Today.ToString(formatter.DateTimeFormat);
            string tomorrow = System.DateTime.Today.AddDays(1).ToString("dd/MM/yyyy");
            Assert.IsTrue(rawOutput.Contains(today) || rawOutput.Contains(tomorrow));
            Assert.AreEqual<Guid>(EventSource.GetGuid(typeof(MockEventSrcForXml)), Guid.Parse(XmlFormattedEntry.Provider.Attribute("Guid").Value));
            Assert.AreEqual<int>(MockEventSrcForXml.LogUsingMessageEventID, Convert.ToInt32(XmlFormattedEntry.EventId.Value));
            Assert.AreEqual<byte>(0, Convert.ToByte(XmlFormattedEntry.Version.Value));
            Assert.AreEqual<int>((int)EventLevel.Informational, Int32.Parse(XmlFormattedEntry.Level.Value));
            Assert.AreEqual<int>((int)EventTask.None, Int32.Parse(XmlFormattedEntry.Task.Value));
            Assert.AreEqual<long>((long)EventKeywords.None, Int64.Parse(XmlFormattedEntry.Keywords.Value.Replace("0x", string.Empty)));
            Assert.AreEqual<int>((int)EventOpcode.Start, Int32.Parse(XmlFormattedEntry.Opcode.Value));
            Assert.AreEqual<byte>(0, Convert.ToByte(XmlFormattedEntry.Version.Value));
            DateTime dt;
            Assert.IsTrue(DateTime.TryParseExact(XmlFormattedEntry.TimeCreated.Attribute("SystemTime").Value, formatter.DateTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt));
            Assert.AreEqual(1, XmlFormattedEntry.Payload.Elements().Count());
            Assert.AreEqual("message", XmlFormattedEntry.Payload.Elements().First().Attribute("Name").Value);
            Assert.AreEqual(MockEventSrcForXml.LogMessage, XmlFormattedEntry.Payload.Elements().First().Value);
            Assert.AreEqual(MockEventSrcForXml.LogMessage, XmlFormattedEntry.Message.Elements().First().Value);
        }

        [TestMethod]
        public void TwoEventsWithPayloadsAndMessageInXml()
        {
            var formatter = new XmlEventTextFormatter();
            var logger = MockEventSrcForXml.Logger;

            string rawOutput = string.Empty;
            using (var listener = new InMemoryEventListener(formatter))
            {
                listener.EnableEvents(logger, EventLevel.LogAlways, MockEventSrcForXml.Keywords.Errors);
                try
                {
                    logger.LogUsingMessage(MockEventSrcForXml.LogMessage);
                    logger.LogUsingMessage(MockEventSrcForXml.LogMessage + "2");
                    rawOutput = Encoding.Default.GetString(listener.Stream.ToArray());
                }
                finally
                {
                    listener.DisableEvents(logger);
                }
            }

            var entries = XDocument.Parse("<Events>" + rawOutput + "</Events>").Root.Elements();
            Assert.AreEqual(2, entries.Count());
            XmlFormattedEntry.Fill(entries.First());
            Assert.AreEqual("message", XmlFormattedEntry.Payload.Elements().First().Attribute("Name").Value);
            Assert.AreEqual(MockEventSrcForXml.LogMessage, XmlFormattedEntry.Payload.Elements().First().Value);
            XmlFormattedEntry.Fill(entries.Last());
            Assert.AreEqual("message", XmlFormattedEntry.Payload.Elements().Last().Attribute("Name").Value);
            Assert.AreEqual(MockEventSrcForXml.LogMessage + "2", XmlFormattedEntry.Payload.Elements().First().Value);
            Assert.AreEqual(MockEventSrcForXml.LogMessage, XmlFormattedEntry.Message.Elements().First().Value);
        }

        [TestMethod]
        public void EventWithPayloadAloneInXml()
        {
            var formatter = new XmlEventTextFormatter();
            string payloadMsg = ("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<test/>");
            var logger = MockEventSrcForXml.Logger;

            string rawOutput = string.Empty;
            using (var listener = new InMemoryEventListener(formatter))
            {
                listener.EnableEvents(logger, EventLevel.LogAlways, MockEventSrcForXml.Keywords.Errors);
                try
                {
                    logger.LogUsingMessage(payloadMsg);
                    rawOutput = Encoding.Default.GetString(listener.Stream.ToArray());
                }
                finally
                {
                    listener.DisableEvents(logger);
                }
            }

            var entries = XDocument.Parse("<Events>" + rawOutput + "</Events>").Root.Elements();
            XmlFormattedEntry.Fill(entries.First());
            Assert.IsFalse(rawOutput.StartsWith("{\r\n")); // No Formatting (Default)
            Assert.AreEqual<Guid>(EventSource.GetGuid(typeof(MockEventSrcForXml)), Guid.Parse(XmlFormattedEntry.Provider.Attribute("Guid").Value));
            Assert.AreEqual<int>(MockEventSrcForXml.LogUsingMessageEventID, Convert.ToInt32(XmlFormattedEntry.EventId.Value));
            Assert.AreEqual<byte>(0, Convert.ToByte(XmlFormattedEntry.Version.Value));
            Assert.AreEqual<int>((int)EventLevel.Informational, Int32.Parse(XmlFormattedEntry.Level.Value));
            Assert.AreEqual<int>((int)EventTask.None, Int32.Parse(XmlFormattedEntry.Task.Value));
            Assert.AreEqual<long>((long)EventKeywords.None, Int64.Parse(XmlFormattedEntry.Keywords.Value.Replace("0x", string.Empty)));
            Assert.AreEqual<int>((int)EventOpcode.Start, Int32.Parse(XmlFormattedEntry.Opcode.Value));
            Assert.AreEqual<byte>(0, Convert.ToByte(XmlFormattedEntry.Version.Value));
            DateTime dt;
            Assert.IsTrue(DateTime.TryParseExact(XmlFormattedEntry.TimeCreated.Attribute("SystemTime").Value, EventEntry.DefaultDateTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt));
            Assert.AreEqual(1, XmlFormattedEntry.Payload.Elements().Count());
            Assert.AreEqual("message", XmlFormattedEntry.Payload.Elements().First().Attribute("Name").Value);
            Assert.AreEqual(payloadMsg, XmlFormattedEntry.Payload.Elements().First().Value);
        }

        [TestMethod]
        public void EventWithPayloadAndEnumsInXml()
        {
            string fileName = "LogUsingPayloadWithEnumsInProc";
            File.Delete(fileName);
            var formatter = new XmlEventTextFormatter(EventTextFormatting.Indented, "d");
            var logger = MockEventSourceInProcEnum.Logger;

            using (var listener = new ObservableEventListener())
            {
                listener.EnableEvents(logger, EventLevel.LogAlways, Keywords.All);
                listener.LogToFlatFile(fileName, formatter);
                try
                {
                    logger.SendEnumsEvent16(MockEventSourceInProcEnum.MyColor.Green, MockEventSourceInProcEnum.MyFlags.Flag1);
                }
                finally
                {
                    listener.DisableEvents(logger);
                }
            }

            var rawOutput = FlatFileHelper.GetAllText(fileName);
            var entries = XDocument.Parse("<Events>" + rawOutput + "</Events>").Root.Elements();
            Assert.AreEqual(1, entries.Count());
            XmlFormattedEntry.Fill(entries.First());
            Assert.AreEqual("a", XmlFormattedEntry.Payload.Elements().First().Attribute("Name").Value);
            Assert.AreEqual("2", XmlFormattedEntry.Payload.Elements().First().Value);
            Assert.AreEqual("b", XmlFormattedEntry.Payload.Elements().Last().Attribute("Name").Value);
            Assert.AreEqual("1", XmlFormattedEntry.Payload.Elements().Last().Value);
        }

        [TestMethod]
        public void EventWithActivityIdInXml()
        {
            var formatter = new XmlEventTextFormatter();
            var logger = MockEventSrcForXml.Logger;

            var activityId = Guid.NewGuid();
            var previousActivityId = Guid.Empty;
            string rawOutput = string.Empty;
            using (var listener = new InMemoryEventListener(formatter))
            {
                listener.EnableEvents(logger, EventLevel.LogAlways, MockEventSrcForXml.Keywords.Errors);
                try
                {
                    EventSource.SetCurrentThreadActivityId(activityId, out previousActivityId);
                    logger.LogUsingMessage(MockEventSrcForXml.LogMessage);
                    rawOutput = Encoding.Default.GetString(listener.Stream.ToArray());
                }
                finally
                {
                    listener.DisableEvents(logger);
                    EventSource.SetCurrentThreadActivityId(previousActivityId);
                }
            }

            var entries = XDocument.Parse("<Events>" + rawOutput + "</Events>").Root.Elements();
            XmlFormattedEntry.Fill(entries.First());
            Assert.AreEqual<Guid>(EventSource.GetGuid(typeof(MockEventSrcForXml)), Guid.Parse(XmlFormattedEntry.Provider.Attribute("Guid").Value));
            Assert.AreEqual(1, XmlFormattedEntry.Payload.Elements().Count());
            Assert.AreEqual("message", XmlFormattedEntry.Payload.Elements().First().Attribute("Name").Value);
            Assert.AreEqual(MockEventSrcForXml.LogMessage, XmlFormattedEntry.Payload.Elements().First().Value);
            Assert.AreEqual<Guid>(activityId, Guid.Parse(XmlFormattedEntry.Correlation.Attribute("ActivityID").Value));
            Assert.IsNull(XmlFormattedEntry.Correlation.Attribute("RelatedActivityID"));
        }

        [TestMethod]
        public void EventWithActivityIdAndRelatedActivityIdInXml()
        {
            var formatter = new XmlEventTextFormatter();
            var logger = MockEventSrcForXml.Logger;

            var activityId = Guid.NewGuid();
            var relatedActivityId = Guid.NewGuid();
            var previousActivityId = Guid.Empty;
            string rawOutput = string.Empty;
            using (var listener = new InMemoryEventListener(formatter))
            {
                listener.EnableEvents(logger, EventLevel.LogAlways, MockEventSrcForXml.Keywords.Errors);
                try
                {
                    EventSource.SetCurrentThreadActivityId(activityId, out previousActivityId);
                    logger.LogUsingMessageWithRelatedActivityId(MockEventSrcForXml.LogMessage, relatedActivityId);
                    rawOutput = Encoding.Default.GetString(listener.Stream.ToArray());
                }
                finally
                {
                    listener.DisableEvents(logger);
                    EventSource.SetCurrentThreadActivityId(previousActivityId);
                }
            }

            var entries = XDocument.Parse("<Events>" + rawOutput + "</Events>").Root.Elements();
            XmlFormattedEntry.Fill(entries.First());
            Assert.AreEqual<Guid>(EventSource.GetGuid(typeof(MockEventSrcForXml)), Guid.Parse(XmlFormattedEntry.Provider.Attribute("Guid").Value));
            Assert.AreEqual(1, XmlFormattedEntry.Payload.Elements().Count());
            Assert.AreEqual("message", XmlFormattedEntry.Payload.Elements().First().Attribute("Name").Value);
            Assert.AreEqual(MockEventSrcForXml.LogMessage, XmlFormattedEntry.Payload.Elements().First().Value);
            Assert.AreEqual<Guid>(activityId, Guid.Parse(XmlFormattedEntry.Correlation.Attribute("ActivityID").Value));
            Assert.AreEqual<Guid>(relatedActivityId, Guid.Parse(XmlFormattedEntry.Correlation.Attribute("RelatedActivityID").Value));
        }
    }
}
