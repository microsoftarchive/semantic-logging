// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using Diagnostics.Tracing;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw.Configuration;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Formatters;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Observable;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.OutProc.Tests.TestObjects;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Sinks;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.Shared.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.OutProc.Tests.Sinks
{
    [TestClass]
    public class FlatFileSinkFixture
    {
        [TestMethod]
        public void WhenUsingFlatFileSinkProgramatic()
        {
            var logger = MockEventSourceOutProc.Logger;
            EventTextFormatter formatter = new EventTextFormatter(EventTextFormatter.DashSeparator);
            var fileName = "newflatfileSerial.log";
            File.Delete(fileName);
            EventSourceSettings settings = new EventSourceSettings("MockEventSourceOutProc", null, EventLevel.LogAlways);
            var subject = new EventEntrySubject();
            subject.LogToFlatFile(fileName, formatter);

            SinkSettings sinkSettings = new SinkSettings("flatFileSink", subject, new List<EventSourceSettings>() { { settings } });
            List<SinkSettings> sinks = new List<SinkSettings>() { { sinkSettings } };
            TraceEventServiceConfiguration svcConfiguration = new TraceEventServiceConfiguration(sinks);
            IEnumerable<string> entries = null;
            using (TraceEventService collector = new TraceEventService(svcConfiguration))
            {
                collector.Start();
                try
                {
                    for (int n = 0; n < 200; n++)
                    {
                        logger.LogSomeMessage("some message" + n.ToString());
                    }

                    entries = FlatFileHelper.PollUntilTextEventsAreWritten(fileName, 200, EventTextFormatter.DashSeparator);
                }
                finally
                {
                    collector.Stop();
                }
            }

            Assert.AreEqual(200, entries.Count());
            StringAssert.Contains(entries.First(), "some message0");
            StringAssert.Contains(entries.Last(), "some message199");
        }

        [TestMethod]
        public void WhenUsingRollingSinkProgramatic()
        {
            var logger = MockEventSourceOutProc.Logger;
            EventTextFormatter formatter = new EventTextFormatter(EventTextFormatter.DashSeparator);
            var fileName = "newRollingFlatfileSerial.log";
            File.Delete(fileName);
            EventSourceSettings settings = new EventSourceSettings("MockEventSourceOutProc", null, EventLevel.LogAlways);
            var subject = new EventEntrySubject();
            subject.LogToRollingFlatFile(fileName, 100, "d", RollFileExistsBehavior.Overwrite, RollInterval.Day, formatter);

            SinkSettings sinkSettings = new SinkSettings("rollingFlatFileSink", subject, new List<EventSourceSettings>() { { settings } });
            List<SinkSettings> sinks = new List<SinkSettings>() { { sinkSettings } };
            TraceEventServiceConfiguration svcConfiguration = new TraceEventServiceConfiguration(sinks);
            IEnumerable<string> entries = null;
            using (TraceEventService collector = new TraceEventService(svcConfiguration))
            {
                collector.Start();
                try
                {
                    for (int n = 0; n < 200; n++)
                    {
                        logger.LogSomeMessage("some message" + n.ToString());
                    }

                    entries = FlatFileHelper.PollUntilTextEventsAreWritten(fileName, 200, EventTextFormatter.DashSeparator);
                }
                finally
                {
                    collector.Stop();
                }
            }

            Assert.AreEqual(200, entries.Count());
            StringAssert.Contains(entries.First(), "some message0");
            StringAssert.Contains(entries.Last(), "some message199");
        }

        [TestMethod]
        public void WhenNoArgEventIsLogged()
        {
            var serviceConfigFile = "Configurations\\DataCorrectness\\FlatFile.xml";
            string fileName = @".\Logs\OutProcFlatFileData.log";

            FlatFileHelper.DeleteDirectory(@".\Logs");
            string xmlContent = File.ReadAllText(serviceConfigFile);
            IEnumerable<string> entries = null;
            var xmlContentRepl = xmlContent.Replace("\"replaceEventSource\"", "\"TestEventSourceNoAttributes\"");
            try
            {
                File.WriteAllText(serviceConfigFile, xmlContentRepl);

                using (var svcConfiguration = TraceEventServiceConfiguration.Load(serviceConfigFile))
                using (var eventCollectorService = new TraceEventService(svcConfiguration))
                {
                    eventCollectorService.Start();
                    try
                    {
                        var logger = TestEventSourceNoAttributes.Logger;
                        logger.NoArgEvent1();
                    }
                    finally
                    {
                        entries = FlatFileHelper.PollUntilTextEventsAreWritten(fileName, 1, EventTextFormatter.DashSeparator);
                        eventCollectorService.Stop();
                    }
                }
            }
            finally
            {
                File.WriteAllText(serviceConfigFile, xmlContent);
            }

            Assert.AreEqual(1, entries.Count());
            StringAssert.Contains(entries.First(), "EventId : 1");
            StringAssert.Contains(entries.First(), "Payload : ");
        }
        
        [TestMethod]
        public void WhenIntArgPayload()
        {
            var serviceConfigFile = "Configurations\\DataCorrectness\\FlatFile.xml";
            string fileName = @".\Logs\OutProcFlatFileData.log";
            FlatFileHelper.DeleteDirectory(@".\Logs");

            string xmlContent = File.ReadAllText(serviceConfigFile);
            IEnumerable<string> entries = null;
            var xmlContentRepl = xmlContent.Replace("replaceEventSource", "TestEventSourceNoAttributes");
            try
            {
                File.WriteAllText(serviceConfigFile, xmlContentRepl);

                using (var svcConfiguration = TraceEventServiceConfiguration.Load(serviceConfigFile))
                using (var eventCollectorService = new TraceEventService(svcConfiguration))
                {
                    eventCollectorService.Start();
                    try
                    {
                        var logger = TestEventSourceNoAttributes.Logger;
                        logger.IntArgEvent2(10);

                        entries = FlatFileHelper.PollUntilTextEventsAreWritten(fileName, 1, EventTextFormatter.DashSeparator);
                    }
                    finally
                    {
                        eventCollectorService.Stop();
                    }
                }
            }
            finally
            {
                File.WriteAllText(serviceConfigFile, xmlContent);
            }

            Assert.AreEqual(1, entries.Count());
            StringAssert.Contains(entries.First(), "EventId : 2");
            StringAssert.Contains(entries.First(), "Payload : [arg : 10]");
        }

        [TestMethod]
        public void WhenLongArgPayload()
        {
            var serviceConfigFile = "Configurations\\DataCorrectness\\FlatFile.xml";
            string fileName = @".\Logs\OutProcFlatFileData.log";
            FlatFileHelper.DeleteDirectory(@".\Logs");

            string xmlContent = File.ReadAllText(serviceConfigFile);
            IEnumerable<string> entries = null;
            var xmlContentRepl = xmlContent.Replace("replaceEventSource", "TestEventSourceNoAttributes");
            try
            {
                File.WriteAllText(serviceConfigFile, xmlContentRepl);

                using (var svcConfiguration = TraceEventServiceConfiguration.Load(serviceConfigFile))
                using (var eventCollectorService = new TraceEventService(svcConfiguration))
                {
                    eventCollectorService.Start();
                    try
                    {
                        var logger = TestEventSourceNoAttributes.Logger;
                        logger.LongArgEvent3((long)10);

                        entries = FlatFileHelper.PollUntilTextEventsAreWritten(fileName, 1, EventTextFormatter.DashSeparator);
                    }
                    finally
                    {
                        eventCollectorService.Stop();
                    }
                }
            }
            finally
            {
                File.WriteAllText(serviceConfigFile, xmlContent);
            }

            Assert.AreEqual(1, entries.Count());
            StringAssert.Contains(entries.First(), "EventId : 3");
            StringAssert.Contains(entries.First(), "Payload : [arg : 10]");
        }

        [TestMethod]
        public void WhenObjectArgPayload()
        {
            var serviceConfigFile = "Configurations\\DataCorrectness\\FlatFile.xml";
            string fileName = @".\Logs\OutProcFlatFileData.log";
            FlatFileHelper.DeleteDirectory(@".\Logs");

            string xmlContent = File.ReadAllText(serviceConfigFile);
            IEnumerable<string> entries = null;
            var xmlContentRepl = xmlContent.Replace("replaceEventSource", "TestEventSourceNoAttributes");
            try
            {
                File.WriteAllText(serviceConfigFile, xmlContentRepl);

                using (var svcConfiguration = TraceEventServiceConfiguration.Load(serviceConfigFile))
                using (var eventCollectorService = new TraceEventService(svcConfiguration))
                {
                    eventCollectorService.Start();
                    try
                    {
                        var logger = TestEventSourceNoAttributes.Logger;
                        logger.ObjectArrayEvent4(10, "stringarg1", 20, "stringarg3", 30);

                        entries = FlatFileHelper.PollUntilTextEventsAreWritten(fileName, 1, EventTextFormatter.DashSeparator);
                    }
                    finally
                    {
                        eventCollectorService.Stop();
                    }
                }
            }
            finally
            {
                File.WriteAllText(serviceConfigFile, xmlContent);
            }

            Assert.AreEqual(1, entries.Count());
            StringAssert.Contains(entries.First(), "EventId : 4");
            StringAssert.Contains(entries.First(), "Payload : [arg0 : 10] [arg1 : stringarg1] [arg2 : 20] [arg3 : stringarg3] [arg4 : 30]");
        }

        [TestMethod]
        public void WhenTwoArgPayload()
        {
            var serviceConfigFile = "Configurations\\DataCorrectness\\FlatFile.xml";
            string fileName = @".\Logs\OutProcFlatFileData.log";
            FlatFileHelper.DeleteDirectory(@".\Logs");
            string xmlContent = File.ReadAllText(serviceConfigFile);
            IEnumerable<string> entries = null;
            var xmlContentRepl = xmlContent.Replace("replaceEventSource", "TestEventSourceNoAttributes");
            try
            {
                File.WriteAllText(serviceConfigFile, xmlContentRepl);

                using (var svcConfiguration = TraceEventServiceConfiguration.Load(serviceConfigFile))
                using (var eventCollectorService = new TraceEventService(svcConfiguration))
                {
                    eventCollectorService.Start();
                    try
                    {
                        var logger = TestEventSourceNoAttributes.Logger;
                        logger.TwoIntArgEvent6(10, 30);

                        entries = FlatFileHelper.PollUntilTextEventsAreWritten(fileName, 1, EventTextFormatter.DashSeparator);
                    }
                    finally
                    {
                        eventCollectorService.Stop();
                    }
                }
            }
            finally
            {
                File.WriteAllText(serviceConfigFile, xmlContent);
            }

            Assert.AreEqual(1, entries.Count());
            StringAssert.Contains(entries.First(), "EventId : 6");
            StringAssert.Contains(entries.First(), "Payload : [arg1 : 10] [arg2 : 30]");
        }

        [TestMethod]
        public void When3ArgStringPayload()
        {
            var serviceConfigFile = "Configurations\\DataCorrectness\\FlatFile.xml";
            string fileName = @".\Logs\OutProcFlatFileData.log";
            FlatFileHelper.DeleteDirectory(@".\Logs");

            string xmlContent = File.ReadAllText(serviceConfigFile);
            IEnumerable<string> entries = null;
            var xmlContentRepl = xmlContent.Replace("replaceEventSource", "TestEventSourceNoAttributes");
            try
            {
                File.WriteAllText(serviceConfigFile, xmlContentRepl);

                using (var svcConfiguration = TraceEventServiceConfiguration.Load(serviceConfigFile))
                using (var eventCollectorService = new TraceEventService(svcConfiguration))
                {
                    eventCollectorService.Start();
                    try
                    {
                        var logger = TestEventSourceNoAttributes.Logger;
                        logger.ThreeStringArgEvent14("message1", "message2", "message3");

                        entries = FlatFileHelper.PollUntilTextEventsAreWritten(fileName, 1, EventTextFormatter.DashSeparator);
                    }
                    finally
                    {
                        eventCollectorService.Stop();
                    }
                }
            }
            finally
            {
                File.WriteAllText(serviceConfigFile, xmlContent);
            }

            Assert.AreEqual(1, entries.Count());
            StringAssert.Contains(entries.First(), "EventId : 14");
            StringAssert.Contains(entries.First(), "Payload : [arg1 : message1] [arg2 : message2] [arg3 : message3]");
        }

        [TestMethod]
        public void WhenStringAndLongArgPayload()
        {
            var serviceConfigFile = "Configurations\\DataCorrectness\\FlatFile.xml";
            string fileName = @".\Logs\OutProcFlatFileData.log";
            FlatFileHelper.DeleteDirectory(@".\Logs");

            string xmlContent = File.ReadAllText(serviceConfigFile);
            IEnumerable<string> entries = null;
            var xmlContentRepl = xmlContent.Replace("replaceEventSource", "TestEventSourceNoAttributes");
            try
            {
                File.WriteAllText(serviceConfigFile, xmlContentRepl);

                using (var svcConfiguration = TraceEventServiceConfiguration.Load(serviceConfigFile))
                using (var eventCollectorService = new TraceEventService(svcConfiguration))
                {
                    eventCollectorService.Start();

                    try
                    {
                        var logger = TestEventSourceNoAttributes.Logger;
                        logger.StringAndLongArgEvent9("message1", 20);

                        entries = FlatFileHelper.PollUntilTextEventsAreWritten(fileName, 1, EventTextFormatter.DashSeparator);
                    }
                    finally
                    {
                        eventCollectorService.Stop();
                    }
                }
            }
            finally
            {
                File.WriteAllText(serviceConfigFile, xmlContent);
            }

            Assert.AreEqual(1, entries.Count());
            StringAssert.Contains(entries.First(), "EventId : 9");
            StringAssert.Contains(entries.First(), "Payload : [arg1 : message1] [arg2 : 20]");
        }

        [TestMethod]
        public void WhenEnumAndFlagPayload()
        {
            var serviceConfigFile = "Configurations\\DataCorrectness\\FlatFile.xml";
            string fileName = @".\Logs\OutProcFlatFileData.log";
            FlatFileHelper.DeleteDirectory(@".\Logs");

            string xmlContent = File.ReadAllText(serviceConfigFile);
            IEnumerable<string> entries = null;
            var xmlContentRepl = xmlContent.Replace("replaceEventSource", "TestEventSourceNoAttributes");
            try
            {
                File.WriteAllText(serviceConfigFile, xmlContentRepl);

                using (var svcConfiguration = TraceEventServiceConfiguration.Load(serviceConfigFile))
                using (var eventCollectorService = new TraceEventService(svcConfiguration))
                {
                    eventCollectorService.Start();
                    try
                    {
                        var logger = TestEventSourceNoAttributes.Logger;
                        logger.SendEnumsEvent15(MyColor.Green, MyFlags.Flag1 | MyFlags.Flag3);

                        entries = FlatFileHelper.PollUntilTextEventsAreWritten(fileName, 1, EventTextFormatter.DashSeparator);
                    }
                    finally
                    {
                        eventCollectorService.Stop();
                    }
                }
            }
            finally
            {
                File.WriteAllText(serviceConfigFile, xmlContent);
            }

            Assert.AreEqual(1, entries.Count());
            StringAssert.Contains(entries.First(), "EventId : 15");
            StringAssert.Contains(entries.First(), "Payload : [color : 2] [flags : 5]");
        }

        [TestMethod]
        public void WhenEventAttributeHasNoTask()
        {
            var serviceConfigFile = "Configurations\\DataCorrectness\\FlatFile.xml";
            string fileName = @".\Logs\OutProcFlatFileData.log";
            FlatFileHelper.DeleteDirectory(@".\Logs");

            string xmlContent = File.ReadAllText(serviceConfigFile);
            IEnumerable<string> entries = null;
            var xmlContentRepl = xmlContent.Replace(@"name=""replaceEventSource""", @"id=""B4F8149D-6DD2-4EE2-A46A-45584A942D1C""");
            try
            {
                File.WriteAllText(serviceConfigFile, xmlContentRepl);

                using (var svcConfiguration = TraceEventServiceConfiguration.Load(serviceConfigFile))
                using (var eventCollectorService = new TraceEventService(svcConfiguration))
                {
                    eventCollectorService.Start();
                    try
                    {
                        var logger = TestAttributesEventSource.Logger;
                        logger.NoTaskSpecfied2(1, 3, 5);

                        entries = FlatFileHelper.PollUntilTextEventsAreWritten(fileName, 1, EventTextFormatter.DashSeparator);
                    }
                    finally
                    {
                        eventCollectorService.Stop();
                    }
                }
            }
            finally
            {
                File.WriteAllText(serviceConfigFile, xmlContent);
            }

            Assert.AreEqual(1, entries.Count());
            StringAssert.Contains(entries.First(), "EventId : 105");
        }
    }
}
