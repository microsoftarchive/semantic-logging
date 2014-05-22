// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Formatters;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.InProc.Tests.TestObjects;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.Shared.TestObjects;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using System;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.Shared.TestSupport;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.InProc.Tests.Formatters
{
    [TestClass]
    public class JsonEventTextFormatterFixture
    {
        [TestMethod]
        public void EventWithPayloadKeywrdsNoMsgIndentedInJson()
        {
            var logger = MockEventSrcForJson.Logger;

            string rawOutput = string.Empty;
            using (var listener = new InMemoryEventListener() { Formatter = new JsonEventTextFormatter(EventTextFormatting.Indented) })
            {
                listener.EnableEvents(logger, EventLevel.LogAlways, MockEventSrcForJson.Keywords.Errors);
                try
                {
                    logger.UsingKeywords(MockEventSrcForJson.LogMessage, long.MaxValue);
                    rawOutput = Encoding.Default.GetString(listener.Stream.ToArray());
                }
                finally
                {
                    listener.DisableEvents(logger);
                }
            }

            Assert.AreEqual(19, rawOutput.Split('\n').Length); //Assert is indented
            var entries = JsonConvert.DeserializeObject<TestEventEntry[]>("[" + rawOutput + "]");
            var entry = entries.First();
            Assert.AreEqual<Guid>(EventSource.GetGuid(typeof(MockEventSrcForJson)), entry.ProviderId);
            Assert.AreEqual<int>(MockEventSrcForJson.UsingKeywordsEventID, entry.EventId);
            Assert.AreEqual<EventLevel>(EventLevel.Informational, entry.Level);
            Assert.AreEqual<string>("None", entry.EventKeywords.ToString());
            Assert.AreEqual<EventOpcode>(EventOpcode.Start, entry.Opcode);
            Assert.AreEqual<int>(System.Diagnostics.Process.GetCurrentProcess().Id, entry.ProcessId);
            Assert.AreEqual<int>(ThreadHelper.GetCurrentUnManagedThreadId(), entry.ThreadId);
            Assert.AreEqual<byte>(0, entry.Version);
            Assert.AreEqual<EventTask>(EventTask.None, entry.Task);
            Assert.AreEqual(null, entry.Message);
            Assert.AreEqual(2, entry.Payload.Count);
            StringAssert.Contains(entry.Payload.First().ToString(), MockEventSrcForJson.LogMessage);
            StringAssert.Contains(entry.Payload.Last().ToString(), long.MaxValue.ToString());
        }

        [TestMethod]
        public void EventWithPayloadKeywrdsNoMessageInJson()
        {
            var logger = MockEventSrcForJson.Logger;

            string rawOutput = string.Empty;
            using (var listener = new InMemoryEventListener() { Formatter = new JsonEventTextFormatter() })
            {
                listener.EnableEvents(logger, EventLevel.LogAlways, MockEventSrcForJson.Keywords.Errors);
                try
                {
                    logger.UsingKeywords(MockEventSrcForJson.LogMessage, long.MaxValue);
                    rawOutput = Encoding.Default.GetString(listener.Stream.ToArray());
                }
                finally
                {
                    listener.DisableEvents(logger);
                }
            }

            var entries = JsonConvert.DeserializeObject<TestEventEntry[]>("[" + rawOutput + "]");
            var entry = entries.First();
            Assert.IsFalse(rawOutput.StartsWith("{\r\n")); // No Formatting (Default)
            Assert.AreEqual<Guid>(EventSource.GetGuid(typeof(MockEventSrcForJson)), entry.ProviderId);
            Assert.AreEqual<int>(MockEventSrcForJson.UsingKeywordsEventID, entry.EventId);
            Assert.AreEqual<EventLevel>(EventLevel.Informational, entry.Level);
            Assert.AreEqual<string>("None", entry.EventKeywords.ToString());
            Assert.AreEqual<EventOpcode>(EventOpcode.Start, entry.Opcode);
            Assert.AreEqual<int>(System.Diagnostics.Process.GetCurrentProcess().Id, entry.ProcessId);
            Assert.AreEqual<int>(ThreadHelper.GetCurrentUnManagedThreadId(), entry.ThreadId);
            Assert.AreEqual<byte>(0, entry.Version);
            Assert.AreEqual<EventTask>(EventTask.None, entry.Task);
            Assert.AreEqual(null, entry.Message);
            Assert.AreEqual(2, entry.Payload.Count);
            StringAssert.Contains(entry.Payload.First().ToString(), MockEventSrcForJson.LogMessage);
            StringAssert.Contains(entry.Payload.Last().ToString(), long.MaxValue.ToString());
        }

        [TestMethod]
        public void EventWithPayloadAndMessageInJson()
        {
            var logger = MockEventSrcForJson.Logger;

            string rawOutput = string.Empty;
            using (var listener = new InMemoryEventListener() { Formatter = new JsonEventTextFormatter() })
            {
                listener.EnableEvents(logger, EventLevel.LogAlways, MockEventSrcForJson.Keywords.Errors);
                try
                {
                    logger.LogUsingMessage(MockEventSrcForJson.LogMessage);
                    rawOutput = Encoding.Default.GetString(listener.Stream.ToArray());
                }
                finally
                {
                    listener.DisableEvents(logger);
                }
            }

            var entries = JsonConvert.DeserializeObject<TestEventEntry[]>("[" + rawOutput + "]");
            var entry = entries.First();
            Assert.IsFalse(rawOutput.StartsWith("{\r\n")); // No Formatting (Default)
            Assert.AreEqual<Guid>(EventSource.GetGuid(typeof(MockEventSrcForJson)), entry.ProviderId);
            Assert.AreEqual<int>(MockEventSrcForJson.LogUsingMessageEventID, entry.EventId);
            Assert.AreEqual<EventLevel>(EventLevel.Informational, entry.Level);
            Assert.AreEqual<EventKeywords>(EventKeywords.None, entry.EventKeywords);
            Assert.AreEqual<EventOpcode>(EventOpcode.Start, entry.Opcode);
            Assert.AreEqual<int>(System.Diagnostics.Process.GetCurrentProcess().Id, entry.ProcessId);
            Assert.AreEqual<int>(ThreadHelper.GetCurrentUnManagedThreadId(), entry.ThreadId);
            Assert.AreEqual<byte>(0, entry.Version);
            Assert.AreEqual<EventTask>(EventTask.None, entry.Task);
            Assert.AreEqual(MockEventSrcForJson.LogMessage, entry.Message);
            Assert.AreEqual(1, entry.Payload.Count);
            StringAssert.Contains(entry.Payload.First().ToString(), MockEventSrcForJson.LogMessage);
        }

        [TestMethod]
        public void EventWithPayloadAndMessageAndDateTimeFormatInJson()
        {
            var logger = MockEventSrcForJson.Logger;
            var formatter = new JsonEventTextFormatter();
            formatter.DateTimeFormat = "dd/MM/yyyy";

            string rawOutput = string.Empty;
            using (var listener = new InMemoryEventListener() { Formatter = formatter })
            {
                listener.EnableEvents(logger, EventLevel.LogAlways, MockEventSrcForJson.Keywords.Errors);
                try
                {
                    logger.LogUsingMessage(MockEventSrcForJson.LogMessage);
                    rawOutput = Encoding.Default.GetString(listener.Stream.ToArray());
                }
                finally
                {
                    listener.DisableEvents(logger);
                }
            }

            string today = System.DateTime.Today.ToString(formatter.DateTimeFormat);
            string tomorrow = System.DateTime.Today.AddDays(1).ToString(formatter.DateTimeFormat);
            Assert.IsTrue(rawOutput.Contains(today) || rawOutput.Contains(tomorrow));
        }

        [TestMethod]
        public void EventWithNoOpCodeNoKeywordsNoVersionNoMsgInJson()
        {
            var logger = MockEventSourceNoTask.Logger;

            string rawOutput = string.Empty;
            using (var listener = new InMemoryEventListener() { Formatter = new JsonEventTextFormatter() })
            {
                listener.EnableEvents(logger, EventLevel.LogAlways);
                try
                {
                    logger.NoTaskNoOpCode1(1, 2, 3);
                    rawOutput = Encoding.Default.GetString(listener.Stream.ToArray());
                }
                finally
                {
                    listener.DisableEvents(logger);
                }
            }

            var entries = JsonConvert.DeserializeObject<TestEventEntry[]>("[" + rawOutput + "]");
            var entry = entries.First();
            Assert.IsFalse(rawOutput.StartsWith("{\r\n")); // No Formatting (Default)
            Assert.AreEqual<EventLevel>(EventLevel.Informational, entry.Level);
            Assert.AreEqual<EventKeywords>(EventKeywords.None, entry.EventKeywords);
            Assert.AreEqual<EventOpcode>(0, entry.Opcode);
            Assert.AreEqual<int>(System.Diagnostics.Process.GetCurrentProcess().Id, entry.ProcessId);
            Assert.AreEqual<int>(ThreadHelper.GetCurrentUnManagedThreadId(), entry.ThreadId);
            Assert.AreEqual<byte>(0, entry.Version);
            Assert.AreEqual(null, entry.Message);
            Assert.AreEqual(3, entry.Payload.Count);
            StringAssert.Contains(entry.Payload.First().ToString(), "[event3Arg0, 1]");
            StringAssert.Contains(entry.Payload.Last().ToString(), "[event3Arg2, 3]");
        }

        [TestMethod]
        public void EventWithInformationalMessageformatDetailedInJson()
        {
            var logger = MockEventSourceNoTask.Logger;

            string rawOutput = string.Empty;
            using (var listener = new InMemoryEventListener() { Formatter = new JsonEventTextFormatter() })
            {
                listener.EnableEvents(logger, EventLevel.LogAlways);
                try
                {
                    logger.InformationalMessageFormat("test");
                    rawOutput = Encoding.Default.GetString(listener.Stream.ToArray());
                }
                finally
                {
                    listener.DisableEvents(logger);
                }
            }

            var entries = JsonConvert.DeserializeObject<TestEventEntry[]>("[" + rawOutput + "]");
            var entry = entries.FirstOrDefault();
            Assert.IsFalse(rawOutput.StartsWith("{\r\n")); // No Formatting (Default)
            Assert.AreEqual<EventLevel>(EventLevel.Informational, entry.Level);
            Assert.AreEqual<EventKeywords>(EventKeywords.None, entry.EventKeywords);
            Assert.AreEqual<EventOpcode>(0, entry.Opcode);
            Assert.AreEqual<int>(System.Diagnostics.Process.GetCurrentProcess().Id, entry.ProcessId);
            Assert.AreEqual<int>(ThreadHelper.GetCurrentUnManagedThreadId(), entry.ThreadId);
            Assert.AreEqual("**test**", entry.Message);
            Assert.AreEqual<byte>(0, entry.Version);
            Assert.AreEqual(1, entry.Payload.Count);
            StringAssert.Contains(entry.Payload.First().ToString(), "test");
        }

        [TestMethod]
        public void EventWithActivityIdInJson()
        {
            var logger = MockEventSrcForJson.Logger;

            var activityId = Guid.NewGuid();
            var previousActivityId = Guid.Empty;
            string rawOutput = string.Empty;
            using (var listener = new InMemoryEventListener() { Formatter = new JsonEventTextFormatter() })
            {
                listener.EnableEvents(logger, EventLevel.LogAlways, MockEventSrcForJson.Keywords.Errors);
                try
                {
                    EventSource.SetCurrentThreadActivityId(activityId, out previousActivityId);
                    logger.LogUsingMessage(MockEventSrcForJson.LogMessage);
                    rawOutput = Encoding.Default.GetString(listener.Stream.ToArray());
                }
                finally
                {
                    listener.DisableEvents(logger);
                    EventSource.SetCurrentThreadActivityId(previousActivityId);
                }
            }

            var entries = JsonConvert.DeserializeObject<TestEventEntry[]>("[" + rawOutput + "]");
            var entry = entries.First();
            Assert.IsFalse(rawOutput.StartsWith("{\r\n"));
            Assert.AreEqual(MockEventSrcForJson.LogMessage, entry.Message);
            Assert.AreEqual(1, entry.Payload.Count);
            StringAssert.Contains(entry.Payload.First().ToString(), MockEventSrcForJson.LogMessage);
            Assert.AreEqual<Guid>(activityId, entry.ActivityId);
            Assert.AreEqual<Guid>(Guid.Empty, entry.RelatedActivityId);
        }

        [TestMethod]
        public void EventWithActivityIdAndRelatedActivityIdInJson()
        {
            var logger = MockEventSrcForJson.Logger;

            var activityId = Guid.NewGuid();
            var relatedActivityId = Guid.NewGuid();
            var previousActivityId = Guid.Empty;
            string rawOutput = string.Empty;
            using (var listener = new InMemoryEventListener() { Formatter = new JsonEventTextFormatter() })
            {
                listener.EnableEvents(logger, EventLevel.LogAlways, MockEventSrcForJson.Keywords.Errors);
                try
                {
                    EventSource.SetCurrentThreadActivityId(activityId, out previousActivityId);
                    logger.LogUsingMessageWithRelatedActivityId(MockEventSrcForJson.LogMessage, relatedActivityId);
                    rawOutput = Encoding.Default.GetString(listener.Stream.ToArray());
                }
                finally
                {
                    listener.DisableEvents(logger);
                    EventSource.SetCurrentThreadActivityId(previousActivityId);
                }
            }

            var entries = JsonConvert.DeserializeObject<TestEventEntry[]>("[" + rawOutput + "]");
            var entry = entries.First();
            Assert.IsFalse(rawOutput.StartsWith("{\r\n"));
            Assert.AreEqual(MockEventSrcForJson.LogMessage, entry.Message);
            Assert.AreEqual(1, entry.Payload.Count);
            StringAssert.Contains(entry.Payload.First().ToString(), MockEventSrcForJson.LogMessage);
            Assert.AreEqual<Guid>(activityId, entry.ActivityId);
            Assert.AreEqual<Guid>(relatedActivityId, entry.RelatedActivityId);
        }
    }
}