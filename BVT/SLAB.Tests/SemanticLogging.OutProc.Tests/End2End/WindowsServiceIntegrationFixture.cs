using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.OutProc.Tests.TestObjects;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.Shared.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.OutProc.Tests.End2End
{
    [TestClass]
    public class WindowsServiceIntegrationFixture : End2EndFixtureBase
    {
        private const string FlatFileName = "FlatFileOutProcSvc.log";

        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            CleanElasticsearch();
            CleanFlatFile();
            CleanAzure();

            End2EndFixtureBase.ValidateAndInitSemanticLoggingService();

            LogMessages();
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            End2EndFixtureBase.StopService();
        }

        // Ignore until permission issue for Windows service is sorted
        [Ignore]
        [TestMethod]
        public void WhenUsingFlatFile()
        {
            IEnumerable<string> entries = null;

            entries = FlatFileHelper.PollUntilTextEventsAreWritten(FlatFileName, 2, "----------");

            Assert.AreEqual(2, entries.Count());
            StringAssert.Contains(entries.First(), "Payload : [message : logging to the windows service]");
            StringAssert.Contains(entries.Last(), "Payload : [message : logging to the windows service 2]");
        }

        // Ignore until permission issue for Windows service is sorted
        [Ignore]
        [TestMethod]
        public void WhenUsingAzureTable()
        {
            var connectionString = System.Configuration.ConfigurationManager.AppSettings["StorageConnectionString"];

            IEnumerable<WindowsAzureTableEventEntry> events = null;

            events = AzureTableHelper.PollForEvents(connectionString, End2EndFixtureBase.AzureTableName, 2);

            Assert.AreEqual(2, events.Count());
            var event1 = events.SingleOrDefault(e => e.Payload.Contains(@"""message"": ""logging to the windows service"""));
            Assert.IsNotNull(event1);
            var event2 = events.SingleOrDefault(e => e.Payload.Contains(@"""message"": ""logging to the windows service 2"""));
            Assert.IsNotNull(event2);
        }

        // Ignore until permission issue for Windows service is sorted
        [Ignore]
        [TestMethod]
        public void WhenUsingElasticsearch()
        {
            var elasticsearchUri = ConfigurationManager.AppSettings["ElasticsearchUri"];

            var index = string.Format(CultureInfo.InvariantCulture, "{0}-{1:yyyy.MM.dd}", ElasticsearchIndexPrefix, DateTime.UtcNow);
            var type = "testtype";
            string configFile = CopyConfigFileToWhereServiceExeFileIsLocatedAndReturnNewConfigFilePath("Configurations\\WinService", "ElasticsearchWinService.xml");

            QueryResult result = null;
            result = ElasticsearchHelper.PollUntilEvents(elasticsearchUri, index, type, 2);

            Assert.AreEqual(2, result.Hits.Total);
            Assert.IsNotNull(result.Hits.Hits.SingleOrDefault(h => (string)h.Source["Payload_message"] == "logging to the windows service"));
            Assert.IsNotNull(result.Hits.Hits.SingleOrDefault(h => (string)h.Source["Payload_message"] == "logging to the windows service 2"));
        }

        private static void CleanAzure()
        {
            var connectionString = System.Configuration.ConfigurationManager.AppSettings["StorageConnectionString"];
            AzureTableHelper.DeleteTable(connectionString, End2EndFixtureBase.AzureTableName);
        }

        private static void CleanElasticsearch()
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
        }

        private static void CleanFlatFile()
        {
            File.Delete(FlatFileName);
        }

        private static void LogMessages()
        {
            var logger = MockEventSourceOutProcSvc.Logger;
            logger.LogSomeMessage("logging to the windows service");
            logger.LogSomeMessage("logging to the windows service 2");
        }
    }
}