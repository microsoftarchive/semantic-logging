// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw.Configuration;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Formatters;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Observable;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.OutProc.Tests.TestObjects;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.OutProc.Tests.TestScenarios;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.Shared.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.OutProc.Tests.Sinks
{
    [TestClass]
    public class DatabaseSinkFixture
    {
        [ClassInitialize]
        public static void Setup(TestContext testContext)
        {
            AssemblyLoaderHelper.EnsureAllAssembliesAreLoadedForSinkTest();
        }

        [TestMethod]
        public void WhenUsingSinkProgramatically()
        {
            var validConnectionString = System.Configuration.ConfigurationManager.ConnectionStrings["valid"].ConnectionString;
            DatabaseHelper.CleanLoggingDB(validConnectionString);
            var logger = MockEventSourceOutProc.Logger;
            EventTextFormatter formatter = new EventTextFormatter();
            EventSourceSettings settings = new EventSourceSettings("MockEventSourceOutProc", null, EventLevel.LogAlways);
            var subject = new EventEntrySubject();
            subject.LogToSqlDatabase("testInstance", validConnectionString, "Traces", TimeSpan.FromSeconds(1), 1);

            System.Data.DataTable eventsDataTable = null;
            SinkSettings sinkSettings = new SinkSettings("sqlDBsink", subject, new List<EventSourceSettings>() { { settings } });
            List<SinkSettings> sinks = new List<SinkSettings>() { { sinkSettings } };
            TraceEventServiceConfiguration svcConfiguration = new TraceEventServiceConfiguration(sinks);
            TestScenario.WithConfiguration(
                svcConfiguration,
                () =>
                {
                    for (int n = 0; n < 10; n++)
                    {
                        logger.LogSomeMessage("some message" + n.ToString());
                    }

                    eventsDataTable = DatabaseHelper.PollUntilEventsAreWritten(validConnectionString, 10);
                });

            Assert.AreEqual(10, eventsDataTable.Rows.Count);
            StringAssert.Contains(eventsDataTable.Rows[0]["payload"].ToString(), "some message");
        }

        [TestMethod]
        public void WhenEnumsInPayload()
        {
            var validConnectionString = System.Configuration.ConfigurationManager.ConnectionStrings["valid"].ConnectionString;
            DatabaseHelper.CleanLoggingDB(validConnectionString);
            var logger = MockEventSourceOutProcEnum.Logger;
            EventTextFormatter formatter = new EventTextFormatter();

            System.Data.DataTable eventsDataTable = null;
            var subject = new EventEntrySubject();
            subject.LogToSqlDatabase("testInstance", validConnectionString, "Traces", bufferingCount: 1);
            EventSourceSettings settings = new EventSourceSettings("MockEventSourceOutProcEnum", null, EventLevel.LogAlways);
            SinkSettings sinkSettings = new SinkSettings("sqlDBsink", subject, new List<EventSourceSettings>() { { settings } });
            List<SinkSettings> sinks = new List<SinkSettings>() { { sinkSettings } };
            TraceEventServiceConfiguration svcConfiguration = new TraceEventServiceConfiguration(sinks);
            TestScenario.WithConfiguration(
                svcConfiguration,
                () =>
                {
                    logger.SendEnumsEvent16(MockEventSourceOutProcEnum.MyColor.Blue, MockEventSourceOutProcEnum.MyFlags.Flag3);

                    eventsDataTable = DatabaseHelper.PollUntilEventsAreWritten(validConnectionString, 1);
                });

            Assert.AreEqual(1, eventsDataTable.Rows.Count);
            StringAssert.Contains(eventsDataTable.Rows[0]["payload"].ToString(), @"""a"": 1");
            StringAssert.Contains(eventsDataTable.Rows[0]["payload"].ToString(), @"""b"": 4");
        }

        [TestMethod]
        public void WhenUsingSinkThroughConfig()
        {
            var validConnectionString = System.Configuration.ConfigurationManager.ConnectionStrings["valid"].ConnectionString;
            DatabaseHelper.CleanLoggingDB(validConnectionString);
            var logger = MockEventSourceOutProc.Logger;

            System.Data.DataTable eventsDataTable = null;
            var svcConfiguration = TraceEventServiceConfiguration.Load("Configurations\\SqlDatabase\\SqlDB.xml");
            TestScenario.WithConfiguration(
                svcConfiguration,
                () =>
                {
                    for (int n = 0; n < 10; n++)
                    {
                        logger.LogSomeMessage("some message" + n.ToString());
                    }

                    eventsDataTable = DatabaseHelper.PollUntilEventsAreWritten(validConnectionString, 10);
                });

            Assert.AreEqual(10, eventsDataTable.Rows.Count);
            StringAssert.Contains(eventsDataTable.Rows[0]["payload"].ToString(), "some message");
        }
    }
}
