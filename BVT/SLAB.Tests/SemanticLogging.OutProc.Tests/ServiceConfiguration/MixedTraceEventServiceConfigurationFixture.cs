// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw.Configuration;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Formatters;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Observable;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.OutProc.Tests.TestObjects;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.Shared.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.OutProc.Tests.ServiceConfiguration
{
    [TestClass]
    public class MixedTraceEventServiceConfigurationFixture
    {
        [TestMethod]
        public void TwoCollectorsSameEventSource()
        {
            var validConnectionString = System.Configuration.ConfigurationManager.ConnectionStrings["valid"].ConnectionString;
            DatabaseHelper.CleanLoggingDB(validConnectionString);
            var logger = MockEventSourceOutProc.Logger;
            EventTextFormatter formatter = new EventTextFormatter();
            EventSourceSettings settings = new EventSourceSettings("MockEventSourceOutProc", null, EventLevel.LogAlways);

            var subject = new EventEntrySubject();
            subject.LogToSqlDatabase("testInstance", validConnectionString, "Traces", TimeSpan.FromSeconds(1), 1);
            SinkSettings sinkSettings = new SinkSettings("dbSink", subject, new List<EventSourceSettings>() { { settings } });

            var subject2 = new EventEntrySubject();
            subject2.LogToSqlDatabase("testInstance", validConnectionString, "Traces", TimeSpan.FromSeconds(1), 1);
            SinkSettings sinkSettings2 = new SinkSettings("dbSink2", subject2, new List<EventSourceSettings>() { { settings } });

            System.Data.DataTable eventsDataTable = null;
            List<SinkSettings> sinks = new List<SinkSettings>() { sinkSettings, sinkSettings2 };
            TraceEventServiceConfiguration svcConfiguration = new TraceEventServiceConfiguration(sinks);
            using (TraceEventService collector = new TraceEventService(svcConfiguration))
            using (TraceEventService collector2 = new TraceEventService(svcConfiguration))
            {
                collector.Start();
                collector2.Start();
                try
                {
                    for (int n = 0; n < 10; n++)
                    {
                        logger.LogSomeMessage("some message" + n.ToString());
                    }

                    eventsDataTable = DatabaseHelper.PollUntilEventsAreWritten(validConnectionString, 20);
                }
                finally
                {
                    collector.Stop();
                    collector2.Stop();
                }
            }

            Assert.AreEqual(20, eventsDataTable.Rows.Count);
            StringAssert.Contains(eventsDataTable.Rows[0]["payload"].ToString(), "some message");
        }

        [TestMethod]
        public void TwoCollectorsSameEventSourceDifferentSinkTypes()
        {
            var validConnectionString = System.Configuration.ConfigurationManager.ConnectionStrings["valid"].ConnectionString;
            DatabaseHelper.CleanLoggingDB(validConnectionString);
            string fileName = "TwoCollectorsSameEventSourceDifferentSinkTypes.log";
            File.Delete(fileName);
            string header = "===========";
            var logger = MockEventSourceOutProc.Logger;
            EventTextFormatter formatter = new EventTextFormatter(header);
            EventSourceSettings settings = new EventSourceSettings("MockEventSourceOutProc", null, EventLevel.LogAlways);

            var subject = new EventEntrySubject();
            subject.LogToSqlDatabase("testInstance", validConnectionString, "Traces", TimeSpan.FromSeconds(1), 1);
            SinkSettings sinkSettings = new SinkSettings("dbSink", subject, new List<EventSourceSettings>() { { settings } });

            var subject2 = new EventEntrySubject();
            subject2.LogToFlatFile(fileName, formatter);
            SinkSettings sinkSettings2 = new SinkSettings("ffSink", subject2, new List<EventSourceSettings>() { { settings } });

            System.Data.DataTable eventsDataTable = null;
            List<SinkSettings> sinks = new List<SinkSettings>() { sinkSettings, sinkSettings2 };
            TraceEventServiceConfiguration svcConfiguration = new TraceEventServiceConfiguration(sinks);
            IEnumerable<string> entries = null;
            using (TraceEventService collector = new TraceEventService(svcConfiguration))
            using (TraceEventService collector2 = new TraceEventService(svcConfiguration))
            {
                collector.Start();
                collector2.Start();
                try
                {
                    for (int n = 0; n < 10; n++)
                    {
                        logger.LogSomeMessage("some message" + n.ToString());
                    }

                    eventsDataTable = DatabaseHelper.PollUntilEventsAreWritten(validConnectionString, 10);

                    entries = FlatFileHelper.PollUntilTextEventsAreWritten(fileName, 10, header);
                }
                finally
                {
                    collector.Stop();
                    collector2.Stop();
                }
            }

            Assert.AreEqual(10, eventsDataTable.Rows.Count);
            StringAssert.Contains(eventsDataTable.Rows[0]["payload"].ToString(), "some message");

            Assert.AreEqual(10, entries.Count());
            StringAssert.Contains(entries.First(), "some message0");
            StringAssert.Contains(entries.Last(), "some message9");
        }

        [TestMethod]
        public void TwoEventSourcesOneCollector()
        {
            var validConnectionString = System.Configuration.ConfigurationManager.ConnectionStrings["valid"].ConnectionString;
            DatabaseHelper.CleanLoggingDB(validConnectionString);
            var logger = MockEventSourceOutProc.Logger;
            var logger2 = MockEventSourceOutProc2.Logger;
            EventTextFormatter formatter = new EventTextFormatter();
            EventSourceSettings settings = new EventSourceSettings("MockEventSourceOutProc", null, EventLevel.LogAlways);
            EventSourceSettings settings2 = new EventSourceSettings("MockEventSourceOutProc2", null, EventLevel.LogAlways);

            var subject = new EventEntrySubject();
            subject.LogToSqlDatabase("testInstance", validConnectionString, "Traces", TimeSpan.FromSeconds(1), 1);
            SinkSettings sinkSettings = new SinkSettings("dbSink", subject, new List<EventSourceSettings>() { settings, settings2 });
            List<SinkSettings> sinks = new List<SinkSettings>() { sinkSettings };
            TraceEventServiceConfiguration svcConfiguration = new TraceEventServiceConfiguration(sinks);
            using (TraceEventService collector = new TraceEventService(svcConfiguration))
            {
                collector.Start();
                try
                {
                    for (int n = 0; n < 200; n++)
                    {
                        logger.LogSomeMessage("some message" + n.ToString());
                    }

                    var eventsDataTable = DatabaseHelper.PollUntilEventsAreWritten(validConnectionString, 200);
                    Assert.AreEqual(200, eventsDataTable.Rows.Count);
                    StringAssert.Contains(eventsDataTable.Rows[0]["payload"].ToString(), "some message");

                    DatabaseHelper.CleanLoggingDB(validConnectionString);

                    for (int n = 0; n < 200; n++)
                    {
                        logger2.LogSomeMessage("some message" + n.ToString());
                    }

                    var eventsDataTable2 = DatabaseHelper.PollUntilEventsAreWritten(validConnectionString, 200);
                    Assert.AreEqual(200, eventsDataTable2.Rows.Count);
                    StringAssert.Contains(eventsDataTable2.Rows[0]["payload"].ToString(), "some message");
                }
                finally
                {
                    collector.Stop();
                }
            }
        }

        [TestMethod]
        public void FlatFileAllFiltered()
        {
            var logger = MockEventSourceOutProc.Logger;
            EventTextFormatter formatter = new EventTextFormatter(EventTextFormatter.DashSeparator);
            var fileName = "FlatFileAllFiltered.log";
            File.Delete(fileName);

            EventSourceSettings settings = new EventSourceSettings("MockEventSourceOutProc", null, EventLevel.Critical);
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

            Assert.AreEqual(0, entries.Count());
        }

        [TestMethod]
        public void FlatFileSomeFilteredSomeNot()
        {
            var logger = MockEventSourceOutProc.Logger;
            EventTextFormatter formatter = new EventTextFormatter(EventTextFormatter.DashSeparator);
            var fileName = "FlatFileAllFiltered.log";
            File.Delete(fileName);

            EventSourceSettings settings = new EventSourceSettings("MockEventSourceOutProc", null, EventLevel.Error);
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
                        logger.LogSomeMessage("some message " + n.ToString());
                        logger.Critical("some error " + n.ToString());
                    }
                    entries = FlatFileHelper.PollUntilTextEventsAreWritten(fileName, 200, EventTextFormatter.DashSeparator);
                }
                finally
                {
                    collector.Stop();
                }
            }

            Assert.AreEqual(200, entries.Count());
            StringAssert.Contains(entries.First(), "some error 0");
            StringAssert.Contains(entries.Last(), "some error 199");
        }

        [TestMethod]
        public void OutProcFiltering()
        {
            var fileName = "levelFiltering.log";
            File.Delete(fileName);
            var fileName2 = "levelFiltering2.log";
            File.Delete(fileName2);
            var fileName3 = "levelFiltering3.log";
            File.Delete(fileName3);
            var logger = MockEventSourceOutProcFiltering.Logger;

            IEnumerable<string> entries = null;
            IEnumerable<string> entries2 = null;
            IEnumerable<string> entries3 = null;
            using (TraceEventServiceConfiguration svcConfiguration = TraceEventServiceConfiguration.Load("Configurations\\LevelFiltering\\LevelFiltering.xml"))
            using (TraceEventService collector = new TraceEventService(svcConfiguration))
            {
                collector.Start();
                try
                {
                    logger.Informational("some informational message");
                    logger.Verbose("some verbose");
                    logger.Critical("some critical");
                    logger.Error("some error");
                    logger.Warning("some warning");

                    entries = FlatFileHelper.PollUntilTextEventsAreWritten(fileName, 1, "======");
                    entries2 = FlatFileHelper.PollUntilTextEventsAreWritten(fileName2, 2, "======");
                    entries3 = FlatFileHelper.PollUntilTextEventsAreWritten(fileName3, 3, "======");
                }
                finally
                {
                    collector.Stop();
                }
            }

            StringAssert.Contains(entries.First().ToString(), "some critical");
            StringAssert.Contains(entries2.First().ToString(), "some critical");
            StringAssert.Contains(entries2.Last().ToString(), "some error");
            Assert.AreEqual(1, entries3.Where(e => e.Contains("some error")).Count());
            Assert.AreEqual(1, entries3.Where(e => e.Contains("some critical")).Count());
            Assert.AreEqual(1, entries3.Where(e => e.Contains("some warning")).Count());
        }

        [TestMethod]
        public void OutProcKeywordsFiltering()
        {
            var fileName = "keywordFiltering.log";
            File.Delete(fileName);
            var logger = MockEventSourceOutProcKeywords.Logger;

            IEnumerable<string> entries = null;
            using (TraceEventServiceConfiguration svcConfiguration = TraceEventServiceConfiguration.Load("Configurations\\KeywordFiltering\\keywordFiltering.xml"))
            using (TraceEventService collector = new TraceEventService(svcConfiguration))
            {
                collector.Start();
                try
                {
                    logger.InformationalPage("some informational message filtered by Page keyword");
                    logger.InformationalDatabase("some informational message filtered by Database keyword");
                    logger.InformationalDiagnostic("some informational message filtered by Diagnostic keyword");
                    entries = FlatFileHelper.PollUntilTextEventsAreWritten(fileName, 2, "======");
                }
                finally
                {
                    collector.Stop();
                }
            }

            Assert.AreEqual(2, entries.Count());
            StringAssert.Contains(entries.First().ToString(), "some informational message filtered by Page keyword");
            StringAssert.Contains(entries.First().ToString(), "Keywords : 1");
            StringAssert.Contains(entries.Last().ToString(), "some informational message filtered by Database keyword");
            StringAssert.Contains(entries.Last().ToString(), "Keywords : 2");
        }
    }
}
