using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.Shared.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.OutProc.Tests.End2End
{
    public class End2EndFixtureBase
    {
        protected static readonly string SemanticLoggingServiceExecutableFilePath = Path.Combine(Environment.CurrentDirectory, "SemanticLogging-svc.exe");
        protected const string AzureTableName = "azuretablese2eusingwindowsservice";
        protected const string ElasticsearchIndexPrefix = "elasticsearch2eusingwindowsservice";

        public virtual void TestCleanup()
        {
            AzureTableHelper.DeleteTable(System.Configuration.ConfigurationManager.AppSettings["StorageConnectionString"], AzureTableName);

            StopAllSemanticSvcInstances();
        }

        public virtual void Initialize()
        {
            StopAllSemanticSvcInstances();
        }

        protected void StopAllSemanticSvcInstances()
        {
            try
            {
                uint serviceId = GetSemanticLoggingServiceId();
                foreach (var proc in Process.GetProcessesByName("SemanticLogging-svc").Where(p => p.Id != serviceId))
                {
                    if (proc != null)
                    {
                        proc.Kill();
                        proc.WaitForExit((int)TimeSpan.FromMinutes(1).TotalMilliseconds);
                        proc.Dispose();
                    }
                }
            }
            catch
            { }
        }

        protected void StartServiceWithConfig(string configFileName)
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

        protected static void InstallService()
        {
            var semanticLoggingService = GetSemanticLoggingService();
            if (semanticLoggingService == null)
            {
                RunSemanticLoggingServiceExecutable("-i");
            }
        }

        protected static void UninstallService()
        {
            RunSemanticLoggingServiceExecutable("-u");
        }

        protected static void StartWindowsService()
        {
            //            InstallService();
            RunSemanticLoggingServiceExecutable("-s");

            var semanticLoggingService = GetSemanticLoggingService();
            Assert.IsNotNull(semanticLoggingService, "Service was not installed started. Make sure Visual Studio is ran as Administrator.");
        }

        protected static void RunSemanticLoggingServiceExecutable(string argument)
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

        protected static bool StopService()
        {
            var svc = GetSemanticLoggingService();
            try
            {
                svc.Stop();
                svc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
            }
            catch (System.ServiceProcess.TimeoutException)
            {
            }

            return svc.Status == ServiceControllerStatus.Stopped;
        }

        protected static void ValidateAndInitSemanticLoggingService()
        {
            string query = string.Format("SELECT ProcessId, PathName FROM Win32_Service WHERE Name='{0}'",
                Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw.Constants.ServiceName);

            var searcher = new ManagementObjectSearcher(query);
            ManagementBaseObject service = searcher.Get().Cast<ManagementBaseObject>().FirstOrDefault();

            if (service == null)
            {
                Assert.Fail("The " + Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw.Constants.ServiceName +
                    " is not installed.  This test requires manually installing and starting located at " + SemanticLoggingServiceExecutableFilePath);

                //wasServiceInstalled = true;
                //InstallService();
            }
            else
            {
                object path = service["PathName"];
                string fullServicePath = service["PathName"].ToString().Substring(1).TrimEnd('"', '\\');

                if (String.Compare(SemanticLoggingServiceExecutableFilePath, fullServicePath, StringComparison.OrdinalIgnoreCase) != 0)
                {
                    Assert.Fail("The " + Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw.Constants.ServiceName +
                        " is not installed from the correct location to run the code built for the BVTs.  Install and start the service located at " + SemanticLoggingServiceExecutableFilePath);
                }
                object pid = service["ProcessId"];

                if (pid == null || Convert.ToUInt32(pid) == 0)
                {
                    //Assert.Fail("The " + Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw.Constants.ServiceName +
                    //    " is not started.  This test requires the service to be manually started.");

                    StartWindowsService();
                }
            }
        }

        protected static uint GetSemanticLoggingServiceId()
        {
            string query = string.Format("SELECT ProcessId FROM Win32_Service WHERE Name='{0}'",
                Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw.Constants.ServiceName);

            var searcher = new ManagementObjectSearcher(query);
            ManagementBaseObject service = searcher.Get().Cast<ManagementBaseObject>().FirstOrDefault();

            if (service != null)
            {
                object pid = service["ProcessId"];

                if (pid != null)
                {
                    return Convert.ToUInt32(pid);
                }
            }

            return 0;
        }

        protected static ServiceController GetSemanticLoggingService()
        {
            return
                ServiceController
                    .GetServices()
                    .FirstOrDefault(s =>
                        s.ServiceName.Equals(
                            Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw.Constants.ServiceName,
                            StringComparison.OrdinalIgnoreCase));
        }

        protected Process StartServiceAsConsoleWithConfig(string configFileName)
        {
            // TODO: can uncomment below and remove File.Copy's if probePath OOP Service issue is fixed. 

            //string path = Path.Combine(Environment.CurrentDirectory, "SemanticLogging-svc.exe.config");
            //string appConfigContent = File.ReadAllText(path);
            var semanticLoggingServiceProcess = new Process();
            try
            {
                File.Copy("slabsvcTest.xml", "slabsvcTest.xml.bak", true);               
                File.Copy(configFileName, "slabsvcTest.xml", true);
//                string appConfigContentReplace = appConfigContent.Replace("slabsvcTest.xml", configFileName);
//                File.WriteAllText(path, appConfigContentReplace);
                semanticLoggingServiceProcess.StartInfo.FileName = SemanticLoggingServiceExecutableFilePath;
                semanticLoggingServiceProcess.StartInfo.Arguments = "-c";
                semanticLoggingServiceProcess.StartInfo.UseShellExecute = false;
                semanticLoggingServiceProcess.Start();

                // Wait for the configuration to be loaded
                System.Threading.Tasks.Task.Delay(TimeSpan.FromSeconds(5)).Wait();
            }
            catch
            {
                if (semanticLoggingServiceProcess != null)
                {
                    semanticLoggingServiceProcess.Kill();
                    semanticLoggingServiceProcess.WaitForExit((int)TimeSpan.FromMinutes(1).TotalMilliseconds);
                    semanticLoggingServiceProcess.Dispose();
                }

                throw;
            }
            finally
            {
                //                File.WriteAllText(path, appConfigContent);
            }

            return semanticLoggingServiceProcess;
        }

        protected static string CopyConfigFileToWhereServiceExeFileIsLocatedAndReturnNewConfigFilePath(string configFileDirectory, string configFileName)
        {
            var sourceConfigFile = Path.Combine(configFileDirectory, configFileName);
            var configFile = Path.Combine(Environment.CurrentDirectory, configFileName);
            File.Copy(sourceConfigFile, configFile, true);

            return configFile;
        }
    }
}
