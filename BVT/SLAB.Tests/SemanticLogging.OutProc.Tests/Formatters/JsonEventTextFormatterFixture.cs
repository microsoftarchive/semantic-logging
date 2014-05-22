// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw.Configuration;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.OutProc.Tests.TestObjects;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.OutProc.Tests.TestScenarios;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.Shared.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.OutProc.Tests.Formatters
{
    [TestClass]
    public class JsonEventTextFormatterFixture
    {
        [TestMethod]
        public void WhenUsingJsonFormatterIndented()
        {
            string fileName = @".\FlatFileJsonFormatterIndentedOutProc.log";
            File.Delete(fileName);
            var logger = MockEventSourceOutProc.Logger;

            IEnumerable<TestEventEntry> entries = null;
            var svcConfiguration = TraceEventServiceConfiguration.Load("Configurations\\WithFormatter\\FlatFileJsonFormatterIndentedOutProc.xml");
            TestScenario.WithConfiguration(
                svcConfiguration,
                () =>
                {
                    logger.LogSomeMessage("logging using Json Formatter indented");
                    entries = FlatFileHelper.PollUntilJsonEventsAreWritten<TestEventEntry>(fileName, 1);
                });

            Assert.AreEqual(1, entries.Count());
            var entry = entries.First();
            Assert.AreEqual(MockEventSourceOutProc.Logger.Guid, entry.ProviderId);
            Assert.AreEqual(8, entry.EventId);
            Assert.AreEqual(EventLevel.Informational, entry.Level);
            Assert.AreEqual(EventKeywords.None, entry.EventKeywords);
            Assert.AreEqual(EventOpcode.Info, entry.Opcode);
            Assert.AreEqual(System.Diagnostics.Process.GetCurrentProcess().Id, entry.ProcessId);
            Assert.AreEqual(ThreadHelper.GetCurrentUnManagedThreadId(), entry.ThreadId);
            Assert.AreEqual(EventOpcode.Info, entry.Opcode);
            Assert.AreEqual(0, entry.Version);
            Assert.IsNull(entry.Message);
            Assert.AreEqual(1, entry.Payload.Count);
            Assert.AreEqual("logging using Json Formatter indented", (string)entry.Payload["message"]);
        }

        [TestMethod]
        public void WhenUsingJsonFormatterNotIndented()
        {
            string fileName = "FlatFileJsonFormatterOutProc.log";
            File.Delete(fileName);
            var logger = MockEventSourceOutProc.Logger;

            IEnumerable<TestEventEntry> entries = null;
            var svcConfiguration = TraceEventServiceConfiguration.Load("Configurations\\WithFormatter\\FlatFileJsonFormatterOutProc.xml");
            TestScenario.WithConfiguration(
                svcConfiguration,
                () =>
                {
                    logger.LogSomeMessage("logging using Json Formatter not indented");
                    entries = FlatFileHelper.PollUntilJsonEventsAreWritten<TestEventEntry>(fileName, 1);
                });

            Assert.AreEqual(1, entries.Count());
            var entry = entries.First();
            Assert.AreEqual(MockEventSourceOutProc.Logger.Guid, entry.ProviderId);
            Assert.AreEqual(8, entry.EventId);
            Assert.AreEqual(EventLevel.Informational, entry.Level);
            Assert.AreEqual(EventKeywords.None, entry.EventKeywords);
            Assert.AreEqual(EventOpcode.Info, entry.Opcode);
            Assert.AreEqual(System.Diagnostics.Process.GetCurrentProcess().Id, entry.ProcessId);
            Assert.AreEqual(ThreadHelper.GetCurrentUnManagedThreadId(), entry.ThreadId);
            Assert.AreEqual(0, entry.Version);
            Assert.AreEqual(1, entry.Payload.Count);
            Assert.AreEqual("logging using Json Formatter not indented", (string)entry.Payload["message"]);
        }

        [TestMethod]
        public void WhenUsingJsonFormatterWithCustomDateTimeFormat()
        {
            string fileName = "FlatFileJsonFormatterDateTimeFormat.log";
            File.Delete(fileName);
            var logger = MockEventSourceOutProc.Logger;

            IEnumerable<TestEventEntryCustomTimeStamp> entries = null;
            var svcConfiguration = TraceEventServiceConfiguration.Load("Configurations\\WithFormatter\\FlatFileJsonFormatterDateTimeFormat.xml");
            TestScenario.WithConfiguration(
                svcConfiguration,
                () =>
                {
                    logger.LogSomeMessage("logging using custom DateTime format");
                    entries = FlatFileHelper.PollUntilJsonEventsAreWritten<TestEventEntryCustomTimeStamp>(fileName, 1);
                });

            var dt = DateTime.UtcNow;
            string expectedTimestamp = dt.Day.ToString() + dt.Month.ToString() + dt.Year.ToString();
            Assert.AreEqual(expectedTimestamp, entries.First().Timestamp);
        }

        [TestMethod]
        public void WhenEnumsInPayloadInJson()
        {
            string fileName = @".\Logs\FlatFileJsonAndEnums.log";
            FlatFileHelper.DeleteDirectory(@".\Logs");
            var logger = MockEventSourceOutProcEnum.Logger;

            IEnumerable<TestEventEntryCustomTimeStamp> entries = null;
            var svcConfiguration = TraceEventServiceConfiguration.Load("Configurations\\WithFormatter\\FlatFileJsonAndEnums.xml");
            TestScenario.WithConfiguration(
                svcConfiguration,
                () =>
                {
                    logger.SendEnumsEvent15(MockEventSourceOutProcEnum.MyColor.Blue, MockEventSourceOutProcEnum.MyFlags.Flag2);
                    entries = FlatFileHelper.PollUntilJsonEventsAreWritten<TestEventEntryCustomTimeStamp>(fileName, 1);
                });

            Assert.AreEqual(1, entries.Count());
            var entry = entries.First();
            Assert.AreEqual(2, entry.Payload.Count);
            Assert.AreEqual((long)MockEventSourceOutProcEnum.MyColor.Blue, (long)entry.Payload["a"]);
            Assert.AreEqual((long)MockEventSourceOutProcEnum.MyFlags.Flag2, (long)entry.Payload["b"]);
        }
    }
}
