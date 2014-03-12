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

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.OutProc.Tests.WindowsService
{
    [TestClass]
    public class IntegrationFixture
    {
        private static readonly string SemanticLoggingServiceExecutableFilePath = Path.Combine(Environment.CurrentDirectory, "SemanticLogging-svc.exe");
        private string tableName;

        [ClassCleanup]
        public static void ClassCleanup()
        {
            try
            {
                UninstallService();
            }
            catch 
            { }
        }

        [TestInitialize]
        public void Initialize()
        {
            this.tableName = string.Empty;
        }

        [TestCleanup]
        public void TestCleanup()
        {
            if (!string.IsNullOrWhiteSpace(this.tableName))
            {
                AzureTableHelper.DeleteTable(System.Configuration.ConfigurationManager.AppSettings["StorageConnectionString"], this.tableName);
            }
        }

        [TestMethod]
        public void WhenUsingBasicConfig()
        {
            string configFile = "Configurations\\WinService\\VeryBasicConfig.xml";

            try
            {
                StartServiceWithConfig(configFile);
            }
            finally
            {
                StopService();
            }

            var proc = Process.GetProcessesByName("SemanticLogging-svc").FirstOrDefault();
            Assert.IsNotNull(proc);
        }

        [TestMethod]
        public void WhenUsingFlatFile()
        {
            string fileName = "FlatFileOutProcCfgWS.log";
            File.Delete(fileName);
            string configFile = "Configurations\\WinService\\FlatFileWinService.xml";

            IEnumerable<string> entries = null;
            try
            {
                StartServiceWithConfig(configFile);
                var logger = MockEventSourceOutProc.Logger;
                logger.LogSomeMessage("logging to the windows service");
                logger.LogSomeMessage("logging to the windows service 2");

                entries = FlatFileHelper.PollUntilTextEventsAreWritten(fileName, 2, "----------");
            }
            finally
            {
                StopService();
            }

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

            try
            {
                var proc = Process.GetProcessesByName("SemanticLogging-svc").FirstOrDefault();
                if (proc != null)
                {
                    proc.Kill();
                }
            }
            catch 
            { }

            // Run the service as a console app.
            // Login to the localdb from the Windows Service (as SYSTEM user) is denied.
            System.Data.DataTable logsTable = null;
            using (var semanticLoggingServiceProcess = this.StartServiceAsConsoleWithConfig(configFile))
            {
                try
                {
                    var logger = MockEventSourceOutProc.Logger;
                    logger.LogSomeMessage("logging to the windows service");
                    logger.LogSomeMessage("logging to the windows service 2");

                    logsTable = DatabaseHelper.PollUntilEventsAreWritten(validConnectionString, 2);
                }
                finally
                {
                    if (semanticLoggingServiceProcess != null)
                    {
                        semanticLoggingServiceProcess.Kill();
                    }
                }
            }

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
            this.tableName = "azuretablese2eusingwindowsservice";
            var connectionString = System.Configuration.ConfigurationManager.AppSettings["StorageConnectionString"];
            AzureTableHelper.DeleteTable(connectionString, this.tableName);
            string configFile = CopyConfigFileToWhereServiceExeFileIsLocatedAndReturnNewConfigFilePath("Configurations\\WinService", "AzureTablesWinService.xml");

            IEnumerable<WindowsAzureTableEventEntry> events = null;
            try
            {
                StartServiceWithConfig(configFile);
                var logger = MockEventSourceOutProc.Logger;
                logger.LogSomeMessage("logging using windows service to azure tables");
                logger.LogSomeMessage("logging using windows service to azure tables 2");

                events = AzureTableHelper.PollForEvents(connectionString, this.tableName, 2);
            }
            finally
            {
                StopService();
            }

            Assert.AreEqual(2, events.Count());
            var event1 = events.SingleOrDefault(e => e.Payload.Contains(@"""message"": ""logging using windows service to azure tables"""));
            Assert.IsNotNull(event1);
            var event2 = events.SingleOrDefault(e => e.Payload.Contains(@"""message"": ""logging using windows service to azure tables 2"""));
            Assert.IsNotNull(event2);
        }

        [TestMethod]
        public void WhenUsingElasticSearch()
        {
            var elasticSearchUri = ConfigurationManager.AppSettings["ElasticSearchUri"];
            try
            {
                ElasticSearchHelper.DeleteIndex(elasticSearchUri);
            }
            catch (Exception exp)
            {
                Assert.Inconclusive(String.Format("Error occured connecting to ES: Message{0}, StackTrace: {1}", exp.Message, exp.StackTrace));
            }

            var indexPrefix = "elasticsearch2eusingwindowsservice";
            var index = string.Format(CultureInfo.InvariantCulture, "{0}-{1:yyyy.MM.dd}", indexPrefix, DateTime.UtcNow);
            var type = "testtype";
            string configFile = CopyConfigFileToWhereServiceExeFileIsLocatedAndReturnNewConfigFilePath("Configurations\\WinService", "ElasticSearchWinService.xml");

            QueryResult result = null;
            try
            {
                StartServiceWithConfig(configFile);
                var logger = MockEventSourceOutProc.Logger;
                logger.LogSomeMessage("logging using windows service to elastic search");
                logger.LogSomeMessage("logging using windows service to elastic search 2");

                result = ElasticSearchHelper.PollUntilEvents(elasticSearchUri, index, type, 2);
            }
            finally
            {
                StopService();
            }

            Assert.AreEqual(2, result.Hits.Total);
            Assert.IsNotNull(result.Hits.Hits.SingleOrDefault(h => (string)h.Source["Payload_message"] == "logging using windows service to elastic search"));
            Assert.IsNotNull(result.Hits.Hits.SingleOrDefault(h => (string)h.Source["Payload_message"] == "logging using windows service to elastic search 2"));
        }

        private void StartServiceWithConfig(string configFileName)
        {
            string path = Path.Combine(Environment.CurrentDirectory, "SemanticLogging-svc.exe.config");
            string appConfigContent = File.ReadAllText(path);
            try
            {
                string appConfigContentReplace = appConfigContent.Replace("slabsvcTest.xml", configFileName);
                File.WriteAllText(path, appConfigContentReplace);
                StartWindowsService();
            }
            finally
            {
                File.WriteAllText(path, appConfigContent);
            }
        }

        private static void InstallService()
        {
            var semanticLoggingService = GetSemanticLoggingService();
            if (semanticLoggingService == null)
            {
                RunSemanticLoggingServiceExecutable("-i");
            }
        }

        private static void UninstallService()
        {
            RunSemanticLoggingServiceExecutable("-u");
        }

        private static void StartWindowsService()
        {
            InstallService();
            RunSemanticLoggingServiceExecutable("-s");

            var semanticLoggingService = GetSemanticLoggingService();
            semanticLoggingService = GetSemanticLoggingService();
            Assert.IsNotNull(semanticLoggingService, "Service was not installed successfully. Make sure Visual Studio is ran as Administrator.");
        }

        private static void RunSemanticLoggingServiceExecutable(string argument)
        {
            using (var semanticLoggingServiceProcess = new Process())
            {
                semanticLoggingServiceProcess.StartInfo.FileName = SemanticLoggingServiceExecutableFilePath;
                semanticLoggingServiceProcess.StartInfo.Arguments = argument;
                semanticLoggingServiceProcess.StartInfo.UseShellExecute = false;
                semanticLoggingServiceProcess.Start();
                semanticLoggingServiceProcess.WaitForExit();
            }
        }

        private static bool StopService()
        {
            var svc = ServiceController.GetServices().
                FirstOrDefault(s => s.ServiceName.Equals(Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw.Constants.ServiceName, StringComparison.OrdinalIgnoreCase));
            svc.Stop();

            return svc.Status == ServiceControllerStatus.Stopped;
        }

        private static ServiceController GetSemanticLoggingService()
        {
            return 
                ServiceController
                    .GetServices()
                    .FirstOrDefault(s => 
                        s.ServiceName.Equals(
                            Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw.Constants.ServiceName, 
                            StringComparison.OrdinalIgnoreCase));
        }

        private Process StartServiceAsConsoleWithConfig(string configFileName)
        {
            string path = Path.Combine(Environment.CurrentDirectory, "SemanticLogging-svc.exe.config");
            string appConfigContent = File.ReadAllText(path);
            var semanticLoggingServiceProcess = new Process();
            try
            {
                string appConfigContentReplace = appConfigContent.Replace("slabsvcTest.xml", configFileName);
                File.WriteAllText(path, appConfigContentReplace);
                semanticLoggingServiceProcess.StartInfo.FileName = SemanticLoggingServiceExecutableFilePath;
                semanticLoggingServiceProcess.StartInfo.Arguments = "-c";
                semanticLoggingServiceProcess.StartInfo.UseShellExecute = false;
                semanticLoggingServiceProcess.Start();

                // Wait for the configuration to be loaded
                System.Threading.Tasks.Task.Delay(TimeSpan.FromSeconds(1)).Wait();
            }
            catch
            {
                if (semanticLoggingServiceProcess != null)
                {
                    semanticLoggingServiceProcess.Kill();
                    semanticLoggingServiceProcess.Dispose();
                }
            }
            finally
            {
                File.WriteAllText(path, appConfigContent);
            }

            return semanticLoggingServiceProcess;
        }

        private static string CopyConfigFileToWhereServiceExeFileIsLocatedAndReturnNewConfigFilePath(string configFileDirectory, string configFileName)
        {
            var sourceConfigFile = Path.Combine(configFileDirectory, configFileName);
            var configFile = Path.Combine(Environment.CurrentDirectory, configFileName);
            File.Copy(sourceConfigFile, configFile, true);

            return configFile;
        }
    }
}
