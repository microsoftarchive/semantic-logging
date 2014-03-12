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
using System.Diagnostics.Tracing;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.OutProc.Tests.Sinks
{
    [TestClass]
    public class WindowsAzureTableSinkFixture
    {
        private string tableName;

        [ClassInitialize]
        public static void Setup(TestContext testContext)
        {
            AssemblyLoaderHelper.EnsureAllAssembliesAreLoadedForSinkTest();
        }

        [TestInitialize]
        public void Initialize()
        {
            this.tableName = string.Empty;
        }

        [TestCleanup]
        public void Teardown()
        {
            if (!string.IsNullOrWhiteSpace(this.tableName))
            {
                AzureTableHelper.DeleteTable(System.Configuration.ConfigurationManager.AppSettings["StorageConnectionString"], this.tableName);
            }
        }

        [TestMethod]
        public void WhenConnectionTakesTooLong()
        {
            var connectionString = System.Configuration.ConfigurationManager.AppSettings["StorageConnectionString"];
            var subject = new EventEntrySubject();
            subject.LogToWindowsAzureTable("AzureInstance", connectionString);

            Assert.IsTrue(Task.Run(() => subject.OnCompleted()).Wait(TimeSpan.FromSeconds(2)));
        }

        [TestMethod]
        public void WhenConfiguringProgrammatically()
        {
            this.tableName = "testoutofprocazuretables";
            var connectionString = System.Configuration.ConfigurationManager.AppSettings["StorageConnectionString"];
            AzureTableHelper.DeleteTable(connectionString, this.tableName);
            var logger = MockEventSourceOutProc.Logger;
            EventTextFormatter formatter = new EventTextFormatter();

            IEnumerable<WindowsAzureTableEventEntry> events = null;
            EventSourceSettings settings = new EventSourceSettings("MockEventSourceOutProc", null, EventLevel.LogAlways);
            var subject = new EventEntrySubject();
            subject.LogToWindowsAzureTable("AzureInstance", connectionString, tableName, TimeSpan.FromSeconds(1));
            SinkSettings sinkSettings = new SinkSettings("azureSink", subject, new List<EventSourceSettings>() { { settings } });
            List<SinkSettings> sinks = new List<SinkSettings>() { { sinkSettings } };
            TraceEventServiceConfiguration svcConfiguration = new TraceEventServiceConfiguration(sinks);
            using (TraceEventService collector = new TraceEventService(svcConfiguration))
            {
                try
                {
                    collector.Start();
                    for (int i = 0; i < 10; i++)
                    {
                        logger.Critical("Critical message");    
                    }

                    events = AzureTableHelper.PollForEvents(connectionString, this.tableName, 10);
                }
                finally
                {
                    collector.Stop();
                }
            }

            Assert.AreEqual<int>(10, events.Count());
            Assert.AreEqual<int>(2, events.First().EventId);
        }

        [TestMethod]
        public void WhenUsingExternalConfig()
        {
            this.tableName = "outProcazuretablesusingconfig";
            var connectionString = System.Configuration.ConfigurationManager.AppSettings["StorageConnectionString"];
            AzureTableHelper.DeleteTable(connectionString, this.tableName);
            var logger = MockEventSourceOutProc.Logger;

            IEnumerable<WindowsAzureTableEventEntry> events = null;
            var svcConfiguration = TraceEventServiceConfiguration.Load("Configurations\\AzureTables\\AzureTables.xml");
            using (TraceEventService collector = new TraceEventService(svcConfiguration))
            {
                try
                {
                    collector.Start();
                    for (int i = 0; i < 10; i++)
                    {
                        logger.Critical("Critical message");
                    }

                    events = AzureTableHelper.PollForEvents(connectionString, this.tableName, 10);
                }
                finally
                {
                    collector.Stop();
                }
            }

            Assert.AreEqual<int>(10, events.Count());
            Assert.AreEqual<int>(2, events.First().EventId);
        }
    }
}
