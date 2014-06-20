// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.OutProc.Tests.TestObjects;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.Shared.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.ServiceProcess;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.OutProc.Tests.End2End
{
    [TestClass]
    public class IntegrationFixture : End2EndFixtureBase
    {
        [TestInitialize]
        public override void Initialize()
        {
            base.Initialize();
        }

        [TestCleanup]
        public override void TestCleanup()
        {
            // TODO: Can remove File.Copy if probePath OOP Service issue is fixed. 
            File.Copy("slabsvcTest.xml.bak", "slabsvcTest.xml", true);

            base.TestCleanup();
        }

        [TestMethod]
        public void WhenUsingBasicConfig()
        {
            string configFile = "Configurations\\WinService\\VeryBasicConfig.xml";

            this.ExecuteServiceTest(configFile, () =>
            {
                var proc = Process.GetProcessesByName("SemanticLogging-svc").FirstOrDefault();
                Assert.IsNotNull(proc);
            });
        }

        [TestMethod]
        public void WhenUsingFlatFile()
        {
            string fileName = "FlatFileOutProcCfgWS.log";

            File.Delete(fileName);
            string configFile = "Configurations\\WinService\\FlatFileWinService.xml";

            IEnumerable<string> entries = null;
            var logger = MockEventSourceOutProc.Logger;

            this.ExecuteServiceTest(configFile, () =>
            {
                logger.LogSomeMessage("logging to the windows service");
                logger.LogSomeMessage("logging to the windows service 2");

                entries = FlatFileHelper.PollUntilTextEventsAreWritten(fileName, 2, "----------");
            });

            Assert.AreEqual(2, entries.Count());
            StringAssert.Contains(entries.First(), "Payload : [message : logging to the windows service]");
            StringAssert.Contains(entries.Last(), "Payload : [message : logging to the windows service 2]");
        }

        [TestMethod]
        public void WhenUsingDatabase()
        {
            var validConnectionString = System.Configuration.ConfigurationManager.ConnectionStrings["valid"].ConnectionString;
            DatabaseHelper.CleanLoggingDB(validConnectionString);
            string configFile = CopyConfigFileToWhereServiceExeFileIsLocatedAndReturnNewConfigFilePath("Configurations\\WinService", "sqlDB.xml");

            System.Data.DataTable logsTable = null;
            var logger = MockEventSourceOutProc.Logger;

            this.ExecuteServiceTest(configFile, () =>
                {
                    logger.LogSomeMessage("logging to the windows service");
                    logger.LogSomeMessage("logging to the windows service 2");

                    logsTable = DatabaseHelper.PollUntilEventsAreWritten(validConnectionString, 2);
                });

            Assert.IsNotNull(logsTable, "No data logged");
            Assert.AreEqual(2, logsTable.Rows.Count);
            var dr = logsTable.Rows[0];
            StringAssert.Contains((string)dr["Payload"], "logging to the windows service");
            dr = logsTable.Rows[1];
            StringAssert.Contains((string)dr["Payload"], "logging to the windows service 2");
        }

        [TestMethod]
        public void WhenUsingAzureTable()
        {
            var connectionString = System.Configuration.ConfigurationManager.AppSettings["StorageConnectionString"];
            AzureTableHelper.DeleteTable(connectionString, End2EndFixtureBase.AzureTableName);
            string configFile = CopyConfigFileToWhereServiceExeFileIsLocatedAndReturnNewConfigFilePath("Configurations\\WinService", "AzureTablesWinService.xml");

            IEnumerable<WindowsAzureTableEventEntry> events = null;
            var logger = MockEventSourceOutProc.Logger;

            this.ExecuteServiceTest(configFile, () =>
                {
                    logger.LogSomeMessage("logging using windows service to azure tables");
                    logger.LogSomeMessage("logging using windows service to azure tables 2");

                    events = AzureTableHelper.PollForEvents(connectionString, AzureTableName, 2);
                });

            Assert.AreEqual(2, events.Count());
            var event1 = events.SingleOrDefault(e => e.Payload.Contains(@"""message"": ""logging using windows service to azure tables"""));
            Assert.IsNotNull(event1);
            var event2 = events.SingleOrDefault(e => e.Payload.Contains(@"""message"": ""logging using windows service to azure tables 2"""));
            Assert.IsNotNull(event2);
        }

        [TestMethod]
        public void WhenUsingElasticsearch()
        {
            var elasticsearchUri = ConfigurationManager.AppSettings["ElasticsearchUri"];
            try
            {
                ElasticsearchHelper.DeleteIndex(elasticsearchUri);
            }
            catch (Exception exp)
            {
                Assert.Inconclusive(String.Format("Error occured connecting to ES: Message{0}, StackTrace: {1}", exp.Message, exp.StackTrace));
            }

            var index = string.Format(CultureInfo.InvariantCulture, "{0}-{1:yyyy.MM.dd}", ElasticsearchIndexPrefix, DateTime.UtcNow);
            var type = "testtype";
            string configFile = CopyConfigFileToWhereServiceExeFileIsLocatedAndReturnNewConfigFilePath("Configurations\\WinService", "ElasticsearchWinService.xml");

            QueryResult result = null;
            var logger = MockEventSourceOutProc.Logger;

            this.ExecuteServiceTest(configFile, () =>
            {
                logger.LogSomeMessage("logging using windows service to elastic search");
                logger.LogSomeMessage("logging using windows service to elastic search 2");

                result = ElasticsearchHelper.PollUntilEvents(elasticsearchUri, index, type, 2);
            });

            Assert.AreEqual(2, result.Hits.Total);
            Assert.IsNotNull(result.Hits.Hits.SingleOrDefault(h => (string)h.Source["Payload_message"] == "logging using windows service to elastic search"));
            Assert.IsNotNull(result.Hits.Hits.SingleOrDefault(h => (string)h.Source["Payload_message"] == "logging using windows service to elastic search 2"));
        }

        private void ExecuteServiceTest(string configFile, Action runTest)
        {
            using (var semanticLoggingServiceProcess = this.StartServiceAsConsoleWithConfig(configFile))
            {
                StringAssert.Contains(semanticLoggingServiceProcess.ProcessName, "SemanticLogging-svc");

                try
                {
                    runTest();
                }
                finally
                {
                    if (semanticLoggingServiceProcess != null)
                    {
                        semanticLoggingServiceProcess.Kill();
                        semanticLoggingServiceProcess.WaitForExit((int)TimeSpan.FromMinutes(1).TotalMilliseconds);
                    }
                }
            }
        }
    }
}
