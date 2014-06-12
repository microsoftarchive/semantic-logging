// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Session;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw.Configuration;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.OutProc.Tests.TestObjects;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.OutProc.Tests.TestScenarios;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.Shared.TestObjects;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.Shared.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.OutProc.Tests.ServiceConfiguration
{
    [TestClass]
    public class ReconfigFixture
    {
        [TestInitialize]
        public void TestInit()
        {
            foreach (var sessionName in TraceEventSession.GetActiveSessionNames())
            {
                if (sessionName.ToString().StartsWith("ServiceReconfig"))
                {
                    new TraceEventSession(sessionName) { StopOnDispose = true }.Dispose();
                }
            }
        }

        [TestMethod]
        public void WhenSinkIsAdded()
        {
            var fileName = "flatFileReconfig.log";
            File.Delete(fileName);
            var logger = MockEventSourceOutProc.Logger;
            var configFile = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\Configurations\\Reconfiguration\\temp\\configFile.xml";
            UpdateServiceConfigurationFile("Configurations\\Reconfiguration\\NoListener.xml", configFile);

            IEnumerable<string> entries = null;
            TraceEventServiceConfiguration svcConfiguration = TraceEventServiceConfiguration.Load(configFile, true);
            TestScenario.WithConfiguration(
                svcConfiguration,
                () =>
                {
                    TraceSessionHelper.WaitAndAssertCountOfSessions("ServiceReconfig", 1);
                    UpdateServiceConfigurationFile("Configurations\\Reconfiguration\\FlatFileListener.xml", configFile);
                    TraceSessionHelper.WaitAndAssertCountOfSessions("ServiceReconfig-flatFileListener", 1);

                    MockEventSourceOutProc.Logger.LogSomeMessage("some message to new added flat file");
                    entries = FlatFileHelper.PollUntilTextEventsAreWritten(fileName, 1, "======");
                });

            File.Delete(configFile);

            Assert.AreEqual(1, entries.Count());
            StringAssert.Contains(entries.First(), "some message to new added flat file");
        }

        [TestMethod]
        public void WhenSinkIsRemoved()
        {
            var fileName = "flatFileReconfig.log";
            File.Delete(fileName);
            var logger = MockEventSourceOutProc.Logger;
            var configFile = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\Configurations\\Reconfiguration\\temp\\configFile.xml";
            UpdateServiceConfigurationFile("Configurations\\Reconfiguration\\FlatFileListener.xml", configFile);

            TraceEventServiceConfiguration svcConfiguration = TraceEventServiceConfiguration.Load(configFile, true);
            TestScenario.WithConfiguration(
                svcConfiguration,
                () =>
                {
                    TraceSessionHelper.WaitAndAssertCountOfSessions("ServiceReconfig-flatFileListener", 1);
                    MockEventSourceOutProc.Logger.LogSomeMessage("some message to new added flat file");
                    var entries = FlatFileHelper.PollUntilTextEventsAreWritten(fileName, 1, "======");
                    Assert.AreEqual(1, entries.Count());
                    StringAssert.Contains(entries.First(), "some message to new added flat file");

                    UpdateServiceConfigurationFile("Configurations\\Reconfiguration\\NoListener.xml", configFile);
                    TraceSessionHelper.WaitAndAssertCountOfSessions("ServiceReconfig-dummyListener", 1);
                    TraceSessionHelper.WaitAndAssertCountOfSessions("ServiceReconfig-flatFileListener", 0);

                    MockEventSourceOutProc.Logger.LogSomeMessage("this message should not be logged");
                    var entries2 = FlatFileHelper.PollUntilTextEventsAreWritten(fileName, 1, "======");
                    Assert.AreEqual(1, entries2.Count());
                });

            File.Delete(configFile);
        }

        [TestMethod]
        public void WhenEventLevelIsUpdated()
        {
            var fileName = "flatFileReconfig.log";
            File.Delete(fileName);
            var logger = MockEventSourceOutProc.Logger;
            var configFile = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\Configurations\\Reconfiguration\\temp\\configFile.xml";
            UpdateServiceConfigurationFile("Configurations\\Reconfiguration\\FlatFileListener.xml", configFile);

            TraceEventServiceConfiguration svcConfiguration = TraceEventServiceConfiguration.Load(configFile, true);
            TestScenario.WithConfiguration(
                svcConfiguration,
                () =>
                {
                    TraceSessionHelper.WaitAndAssertCountOfSessions("ServiceReconfig-flatFileListener", 1);
                    MockEventSourceOutProc.Logger.LogSomeMessage("some message to new added flat file");
                    var entries = FlatFileHelper.PollUntilTextEventsAreWritten(fileName, 1, "======");
                    Assert.AreEqual(1, entries.Count());
                    StringAssert.Contains(entries.First(), "some message to new added flat file");

                    UpdateServiceConfigurationFile("Configurations\\Reconfiguration\\FlatFileListenerLevelFiltered.xml", configFile);
                    TraceSessionHelper.WaitAndAssertCountOfSessions("ServiceReconfig-flatFileListener", 1);

                    MockEventSourceOutProcLevelFiltered.Logger.LogSomeMessage("this message should not be logged");
                    var entries2 = FlatFileHelper.PollUntilTextEventsAreWritten(fileName, 1, "======");
                    Assert.AreEqual(1, entries2.Count());

                    MockEventSourceOutProcLevelFiltered.Logger.Critical("this critical message should pass the filter");
                    entries2 = FlatFileHelper.PollUntilTextEventsAreWritten(fileName, 2, "======");
                    Assert.AreEqual(2, entries2.Count());
                    StringAssert.Contains(entries2.First(), "some message to new added flat file");
                    StringAssert.Contains(entries2.Last(), "this critical message should pass the filter");
                });

            File.Delete(configFile);
        }

        [TestMethod]
        public void WhenKeywordIsUpdated()
        {
            var fileName = "flatFileReconfig.log";
            File.Delete(fileName);
            var logger = MockEventSourceOutProcKeywords.Logger;
            var configFile = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\Configurations\\Reconfiguration\\temp\\configFile.xml";
            UpdateServiceConfigurationFile("Configurations\\Reconfiguration\\FlatFileListenerNoKeywordFiltered.xml", configFile);

            TraceEventServiceConfiguration svcConfiguration = TraceEventServiceConfiguration.Load(configFile, true);
            TestScenario.WithConfiguration(
                svcConfiguration,
                () =>
                {
                    TraceSessionHelper.WaitAndAssertCountOfSessions("ServiceReconfig-flatFileListener", 1);

                    MockEventSourceOutProcKeywords.Logger.InformationalPage("No keyword filtering");
                    var entries = FlatFileHelper.PollUntilTextEventsAreWritten(fileName, 1, "======");
                    Assert.AreEqual(1, entries.Count());
                    StringAssert.Contains(entries.First(), "No keyword filtering");

                    UpdateServiceConfigurationFile("Configurations\\Reconfiguration\\FlatFileListenerKeywordFiltered.xml", configFile);
                    TraceSessionHelper.WaitAndAssertCountOfSessions("ServiceReconfig-flatFileListener", 1);

                    MockEventSourceOutProcKeywords.Logger.InformationalPage("InformationalPage ok");
                    MockEventSourceOutProcKeywords.Logger.InformationalDatabase("InformationalDatabase ok");
                    MockEventSourceOutProcKeywords.Logger.InformationalDiagnostic("Diagnostic not ok");
                    var entries2 = FlatFileHelper.PollUntilTextEventsAreWritten(fileName, 3, "======");
                    Assert.AreEqual(3, entries2.Count());
                    StringAssert.Contains(entries2.First(), "No keyword filtering");
                    StringAssert.Contains(entries2.ElementAt(1).ToString(), "InformationalPage ok");
                    StringAssert.Contains(entries2.Last(), "InformationalDatabase ok");
                });

            File.Delete(configFile);
        }

        [TestMethod]
        public void WhenSinkAddedWithErrorDoesNotRecycle()
        {
            var fileName = "flatfileListenerOk.log";
            File.Delete(fileName);
            var logger = MockEventSourceOutProc.Logger;
            var configFile = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\Configurations\\Reconfiguration\\temp\\configFile.xml";
            UpdateServiceConfigurationFile("Configurations\\Reconfiguration\\NoListener.xml", configFile);

            TraceEventServiceConfiguration svcConfiguration = TraceEventServiceConfiguration.Load(configFile, true);
            TestScenario.WithConfiguration(
                svcConfiguration,
                () =>
                {
                    using (var collectErrorsListener = new InMemoryEventListener())
                    {
                        collectErrorsListener.EnableEvents(SemanticLoggingEventSource.Log, EventLevel.LogAlways, Keywords.All);
                        try
                        {
                            TraceSessionHelper.WaitAndAssertCountOfSessions("ServiceReconfig", 1);
                            UpdateServiceConfigurationFile("Configurations\\Reconfiguration\\FlatFileListenerError.xml", configFile);
                            TraceSessionHelper.WaitAndAssertCountOfSessions("ServiceReconfig-flatFileListener", 0);
                            TraceSessionHelper.WaitAndAssertCountOfSessions("ServiceReconfig-2flatFileListener", 1);

                            MockEventSourceOutProc.Logger.LogSomeMessage("Some informational from a new listener.");
                            var entries2 = FlatFileHelper.PollUntilTextEventsAreWritten(fileName, 1, "======");
                            Assert.AreEqual(1, entries2.Count());
                            StringAssert.Contains(entries2.First(), "Some informational from a new listener.");

                            collectErrorsListener.WaitEvents.Wait(TimeSpan.FromSeconds(3));
                            StringAssert.Contains(collectErrorsListener.ToString(), "One or more errors occurred when loading the TraceEventService configuration file.");
                            StringAssert.Contains(collectErrorsListener.ToString(), "The given path's format is not supported.");
                            StringAssert.Contains(collectErrorsListener.ToString(), "The configuration was partially successfully loaded. Check logs for further error details.");
                        }
                        finally
                        {
                            File.Delete(configFile);
                            collectErrorsListener.DisableEvents(SemanticLoggingEventSource.Log);
                        }
                    }
                });
        }

        [TestMethod]
        public void WhenTwoSourcesSinkAddedThenRemoved()
        {
            var fileName = "flatFileReconfig.log";
            File.Delete(fileName);
            var logger = MockEventSourceOutProc.Logger;
            var configFile = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\Configurations\\Reconfiguration\\temp\\configFile.xml";
            UpdateServiceConfigurationFile("Configurations\\Reconfiguration\\FlatFileListenerTwoSources.xml", configFile);

            TraceEventServiceConfiguration svcConfiguration = TraceEventServiceConfiguration.Load(configFile, true);
            TestScenario.WithConfiguration(
                svcConfiguration,
                () =>
                {
                    TraceSessionHelper.WaitAndAssertCountOfSessions("ServiceReconfig-flatFileListener", 1);

                    MockEventSourceOutProc.Logger.LogSomeMessage("some message to new added flat file");
                    var entries = FlatFileHelper.PollUntilTextEventsAreWritten(fileName, 1, "======");
                    Assert.AreEqual(1, entries.Count());
                    StringAssert.Contains(entries.First(), "some message to new added flat file");

                    UpdateServiceConfigurationFile("Configurations\\Reconfiguration\\FlatFileListenerTwoSourcesSameListener.xml", configFile);
                    TraceSessionHelper.WaitAndAssertCountOfSessions("ServiceReconfig-flatFileListener", 1);

                    MockEventSourceOutProc.Logger.LogSomeMessage("some message to new added flat file2");
                    MockEventSourceOutProc2.Logger.LogSomeMessage("some message to new added flat file3");
                    var entries2 = FlatFileHelper.PollUntilTextEventsAreWritten(fileName, 3, "======");
                    Assert.AreEqual(3, entries2.Count());
                    StringAssert.Contains(entries2.First(), "some message to new added flat file");
                    StringAssert.Contains(entries2.ElementAt(1), "some message to new added flat file2");
                    StringAssert.Contains(entries2.Last(), "some message to new added flat file3");

                    UpdateServiceConfigurationFile("Configurations\\Reconfiguration\\FlatFileListenerTwoSources.xml", configFile);
                    TraceSessionHelper.WaitAndAssertCountOfSessions("ServiceReconfig-flatFileListener", 1);

                    MockEventSourceOutProc.Logger.LogSomeMessage("last message to new added flat file");
                    var entries3 = FlatFileHelper.PollUntilTextEventsAreWritten(fileName, 4, "======");
                    Assert.AreEqual(4, entries3.Count());
                    StringAssert.Contains(entries3.Last(), "last message to new added flat file");

                    UpdateServiceConfigurationFile("Configurations\\Reconfiguration\\FlatFileListenerTwoSourcesNoListener.xml", configFile);
                    TraceSessionHelper.WaitAndAssertCountOfSessions("ServiceReconfig-flatFileListener", 0);
                });

            File.Delete(configFile);
        }

        [TestMethod]
        public void WhenConfigUpdatedTwice()
        {
            var fileName = "flatFileReconfig.log";
            File.Delete(fileName);
            var logger = MockEventSourceOutProc.Logger;
            var configFile = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\Configurations\\Reconfiguration\\temp\\configFile.xml";
            UpdateServiceConfigurationFile("Configurations\\Reconfiguration\\NoListener.xml", configFile);

            TraceEventServiceConfiguration svcConfiguration = TraceEventServiceConfiguration.Load(configFile, true);
            TestScenario.WithConfiguration(
                svcConfiguration,
                () =>
                {
                    TraceSessionHelper.WaitAndAssertCountOfSessions("ServiceReconfig-flatFileListener", 0);
                    TraceSessionHelper.WaitAndAssertCountOfSessions("ServiceReconfig-dummyListener", 1);

                    TraceEventServiceConfiguration newConfig = TraceEventServiceConfiguration.Load("Configurations\\Reconfiguration\\NoListener.xml");
                    UpdateServiceConfigurationFile("Configurations\\Reconfiguration\\NoListener.xml", configFile);

                    TraceSessionHelper.WaitAndAssertCountOfSessions("ServiceReconfig-flatFileListener", 0);
                    TraceSessionHelper.WaitAndAssertCountOfSessions("ServiceReconfig-dummyListener", 1);
                });

            File.Delete(configFile);
        }

        [TestMethod]
        public void WhenConnfigUpdatedTwiceWithTwoListeners()
        {
            var fileName = "flatFileReconfig.log";
            File.Delete(fileName);
            var logger = MockEventSourceOutProc.Logger;
            var configFile = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\Configurations\\Reconfiguration\\temp\\configFile.xml";
            UpdateServiceConfigurationFile("Configurations\\Reconfiguration\\FlatFileListener.xml", configFile);

            TraceEventServiceConfiguration svcConfiguration = TraceEventServiceConfiguration.Load(configFile, true);
            TestScenario.WithConfiguration(
                svcConfiguration,
                () =>
                {
                    TraceSessionHelper.WaitAndAssertCountOfSessions("ServiceReconfig-flatFileListener", 1);
                    TraceSessionHelper.WaitAndAssertCountOfSessions("ServiceReconfig-dummyListener", 1);

                    UpdateServiceConfigurationFile("Configurations\\Reconfiguration\\FlatFileListener.xml", configFile);

                    TraceSessionHelper.WaitAndAssertCountOfSessions("ServiceReconfig-flatFileListener", 1);
                    TraceSessionHelper.WaitAndAssertCountOfSessions("ServiceReconfig-dummyListener", 1);
                });

            File.Delete(configFile);
        }

        [TestMethod]
        public void WhenConfigChangesSessionRecycles()
        {
            var fileName = "flatFileReconfig.log";
            File.Delete(fileName);
            var logger = MockEventSourceOutProc.Logger;
            var configFile = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\Configurations\\Reconfiguration\\temp\\configFile.xml";
            UpdateServiceConfigurationFile("Configurations\\Reconfiguration\\FlatFileListener.xml", configFile);

            TraceEventServiceConfiguration svcConfiguration = TraceEventServiceConfiguration.Load(configFile, true);
            TestScenario.WithConfiguration(
                svcConfiguration,
                () =>
                {
                    TraceSessionHelper.WaitAndAssertCountOfSessions("ServiceReconfig-flatFileListener", 1);
                    TraceSessionHelper.WaitAndAssertCountOfSessions("ServiceReconfig-dummyListener", 1);

                    UpdateServiceConfigurationFile("Configurations\\Reconfiguration\\FlatFileListenerDiffSession.xml", configFile);
                });

            File.Delete(configFile);
        }

        [TestMethod]
        public void WhenSourceAddedSessionRecycles()
        {
            var fileName = "flatFileReconfig.log";
            File.Delete(fileName);
            var logger = MockEventSourceOutProc.Logger;
            var configFile = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\Configurations\\Reconfiguration\\temp\\configFile.xml";
            UpdateServiceConfigurationFile("Configurations\\Reconfiguration\\FlatFileListener.xml", configFile);

            TraceEventServiceConfiguration svcConfiguration = TraceEventServiceConfiguration.Load(configFile, true);
            TestScenario.WithConfiguration(
                svcConfiguration,
                () =>
                {
                    TraceSessionHelper.WaitAndAssertCountOfSessions("ServiceReconfig-flatFileListener", 1);
                    TraceSessionHelper.WaitAndAssertCountOfSessions("ServiceReconfig-dummyListener", 1);

                    MockEventSourceOutProc.Logger.LogSomeMessage("some message to new added flat file");
                    var entries = FlatFileHelper.PollUntilTextEventsAreWritten(fileName, 1, "======");
                    Assert.AreEqual(1, entries.Count());
                    StringAssert.Contains(entries.First(), "some message to new added flat file");

                    UpdateServiceConfigurationFile("Configurations\\Reconfiguration\\FlatFileListenerTwoSourcesSameListener.xml", configFile);
                    TraceSessionHelper.WaitAndAssertCountOfSessions("ServiceReconfig-dummyListener2", 1);

                    MockEventSourceOutProc2.Logger.LogSomeMessage("another message to new added flat file");
                    var entries2 = FlatFileHelper.PollUntilTextEventsAreWritten(fileName, 2, "======");
                    Assert.AreEqual(2, entries2.Count());
                    StringAssert.Contains(entries2.First(), "some message to new added flat file");
                });

            File.Delete(configFile);
        }

        [TestMethod]
        public void WhenSourceManifestIsUpdated()
        {
            var fileName = "twoflatFileListeners.log";
            File.Delete(fileName);
            var fileName2 = "twoflatFileListeners2.log";
            File.Delete(fileName2);

            var svcConfiguration = TraceEventServiceConfiguration.Load("Configurations\\Reconfiguration\\TwoFlatFileListeners.xml");
            TestScenario.WithConfiguration(
                svcConfiguration,
                () =>
                {
                    var newDomain = AppDomain.CreateDomain("TestintDynamicManifest", AppDomain.CurrentDomain.Evidence, AppDomain.CurrentDomain.SetupInformation);
                    try
                    {
                        var instance = (IsolatedExecutingClass)newDomain.CreateInstanceAndUnwrap(Assembly.GetExecutingAssembly().FullName, typeof(IsolatedExecutingClass).FullName);
                        instance.TestWithSource1();
                    }
                    finally
                    {
                        AppDomain.Unload(newDomain);
                    }

                    newDomain = AppDomain.CreateDomain("TestintDynamicManifest", AppDomain.CurrentDomain.Evidence, AppDomain.CurrentDomain.SetupInformation);
                    try
                    {
                        var instance = (IsolatedExecutingClass)newDomain.CreateInstanceAndUnwrap(Assembly.GetExecutingAssembly().FullName, typeof(IsolatedExecutingClass).FullName);
                        instance.TestWithSource2();
                    }
                    finally
                    {
                        AppDomain.Unload(newDomain);
                    }

                    var entries = FlatFileHelper.PollUntilTextEventsAreWritten(fileName, 2, "======");
                    Assert.AreEqual(2, entries.Count());
                    entries = FlatFileHelper.PollUntilTextEventsAreWritten(fileName2, 2, "======");
                    Assert.AreEqual(2, entries.Count());
                });
        }

        private static void UpdateServiceConfigurationFile(string path, string tempFile)
        {
            File.WriteAllText(tempFile, File.ReadAllText(path));

            // Arbitrary time to wait until the config changes are applied
            Task.Delay(TimeSpan.FromSeconds(2)).Wait();
        }

        [EventSource(Name = "MyNewCompanyEventSource")]
        private sealed class MyNewCompanyEventSource : EventSource
        {
            public static class Tasks
            {
                public const EventTask Opcode = (EventTask)1;
            }

            [Event(1, Message = "Event1 ID={0}", Opcode = EventOpcode.Start, Task = Tasks.Opcode)]
            public void Event1(int id)
            {
                if (this.IsEnabled())
                {
                    this.WriteEvent(1, id);
                }
            }

            public static readonly MyNewCompanyEventSource Logger = new MyNewCompanyEventSource();
        }

        [EventSource(Name = "MyNewCompanyEventSource")]
        private sealed class MyNewCompanyEventSource2 : EventSource
        {
            public static class Tasks
            {
                public const EventTask Opcode = (EventTask)1;
            }

            [Event(1, Message = "Event1 ID={0}", Opcode = EventOpcode.Start, Task = Tasks.Opcode)]
            public void Event1(int id)
            {
                if (this.IsEnabled())
                {
                    this.WriteEvent(1, id);
                }
            }

            [Event(2, Message = "Event2 ID={0}", Opcode = EventOpcode.Start, Task = Tasks.Opcode)]
            public void Event2(int id)
            {
                if (this.IsEnabled())
                {
                    this.WriteEvent(2, id);
                }
            }

            public static readonly MyNewCompanyEventSource2 Logger = new MyNewCompanyEventSource2();
        }

        [Serializable]
        internal class IsolatedExecutingClass : MarshalByRefObject
        {
            public void TestWithSource1()
            {
                var logger = MyNewCompanyEventSource.Logger;
                logger.Event1(1);
            }

            public void TestWithSource2()
            {
                var logger = MyNewCompanyEventSource2.Logger;
                logger.Event2(2);
            }
        }
    }
}
