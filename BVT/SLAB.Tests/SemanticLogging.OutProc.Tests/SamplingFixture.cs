using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw.Configuration;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Formatters;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.OutProc.Tests.TestObjects;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.OutProc.Tests.TestScenarios;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Sinks;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.Shared.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.OutProc.Tests
{
    [TestClass]
    public class SamplingFixture
    {
        private ManualResetEvent waitObject = new ManualResetEvent(false);

        [TestMethod]
        public void WhenEnablingSamplingForAProcessAndProcessIsRunning()
        {
            var serviceConfigFile = "Configurations\\Sampling\\WhenEnablingSamplingForAPRocessAndPRocessIsRunning.xml";
            string fileName = "WhenEnablingSamplingForAPRocessAndPRocessIsRunning.log";

            IEnumerable<TestEventEntry> entries = null;
            Guid activityId;
            Guid oldActivityId;
            TraceEventServiceConfiguration svcConfiguration;

            InitializeTest(serviceConfigFile, fileName, out svcConfiguration, out activityId, out oldActivityId);
            try
            {
                TestScenario.WithConfiguration(svcConfiguration, () =>
                {
                    var triggerLogger = TriggerEventSource.Logger;
                    triggerLogger.TriggerEvent("triggermessage");
                    SamplingEventSource.Logger.EventToSample("mainmessage");

                    entries = FlatFileHelper.PollUntilJsonEventsAreWritten<TestEventEntry>(fileName, 2);
                });
            }
            finally
            {
                EventSource.SetCurrentThreadActivityId(oldActivityId);
            }

            Assert.AreEqual(2, entries.Count());
        }

        [TestMethod]
        public void WhenEnablingSamplingForAProcessAndProcessIsNotRunning()
        {
            var serviceConfigFile = "Configurations\\Sampling\\WhenEnablingSamplingForAProcessAndProcessIsNotRunning.xml";
            string fileName = "WhenEnablingSamplingForAProcessAndProcessIsNotRunning.log";

            IEnumerable<TestEventEntry> entries = null;
            Guid activityId;
            Guid oldActivityId;
            TraceEventServiceConfiguration svcConfiguration;

            InitializeTest(serviceConfigFile, fileName, out svcConfiguration, out activityId, out oldActivityId);
            try
            {
                TestScenario.WithConfiguration(svcConfiguration, () =>
                {
                    var triggerLogger = TriggerEventSource.Logger;
                    triggerLogger.TriggerEvent("triggermessage");
                    SamplingEventSource.Logger.EventToSample("mainmessage");

                    entries = FlatFileHelper.PollUntilJsonEventsAreWritten<TestEventEntry>(fileName, 2);
                });
            }
            finally
            {
                EventSource.SetCurrentThreadActivityId(oldActivityId);
            }

            Assert.AreEqual(0, entries.Count());
        }

        [TestMethod]
        public void WhenEnablingSamplingAndTPLEvents()
        {
            var serviceConfigFile = "Configurations\\Sampling\\WhenEnablingSamplingForAPRocessAndTPLEvents.xml";
            string fileName = "WhenEnablingSamplingForAPRocessAndTPLEvents.log";

            IEnumerable<TestEventEntry> entries = null;
            Guid activityId;
            Guid oldActivityId;
            TraceEventServiceConfiguration svcConfiguration;

            InitializeTest(serviceConfigFile, fileName, out svcConfiguration, out activityId, out oldActivityId);
            try
            {
                TestScenario.WithConfiguration(svcConfiguration, () =>
                {
                    var triggerLogger = TriggerEventSource.Logger;
                    triggerLogger.TriggerEvent("triggermessage");
                    SamplingEventSource.Logger.BeforeEventToSample("mainmessage");
                    Task.Run(async () => { await LogEventsAsync(); });
                    SamplingEventSource.Logger.AfterEventToSample("mainmessage");

                    waitObject.WaitOne();
                    entries = FlatFileHelper.PollUntilJsonEventsAreWritten<TestEventEntry>(fileName, 50);
                });
            }
            finally
            {
                EventSource.SetCurrentThreadActivityId(oldActivityId);
            }

            Assert.IsTrue(entries.Count() > 40 && entries.Count() < 50);
        }

        [TestMethod]
        public void WhenEnablingSamplingAndTPLEventsAndMultipleSources()
        {
            var serviceConfigFile = "Configurations\\Sampling\\WhenEnablingSamplingAndTPLEventsAndMultipleSources.xml";
            string fileName = "WhenEnablingSamplingAndTPLEventsAndMultipleSources.log";
            File.Delete("errorsandWarnings.log");

            IEnumerable<TestEventEntry> entries = null;
            Guid activityId;
            Guid oldActivityId;
            TraceEventServiceConfiguration svcConfiguration;

            InitializeTest(serviceConfigFile, fileName, out svcConfiguration, out activityId, out oldActivityId);

            try
            {
                TestScenario.WithConfiguration(svcConfiguration, () =>
                {
                    var triggerLogger = TriggerEventSource.Logger;
                    triggerLogger.TriggerEvent("triggermessage");
                    SamplingEventSource.Logger.BeforeEventToSample("mainmessage");
                    SamplingEventSource1.Logger.EventToSampleFromOtherSource("mainmessage");
                    Task.Run(async () => await LogEventsAsync());
                    SamplingEventSource.Logger.AfterEventToSample("mainmessage");

                    waitObject.WaitOne();
                    entries = FlatFileHelper.PollUntilJsonEventsAreWritten<TestEventEntry>(fileName, 200);
                });
            }
            finally
            {
                EventSource.SetCurrentThreadActivityId(oldActivityId);
            }

            // Should be 2 trigger events
            Assert.AreEqual(2, entries.Count(entry => entry.Payload.ContainsKey("message") && entry.Payload["message"].ToString().ToLower().StartsWith("trigger")));

            // Should be 5 Sampling events
            Assert.AreEqual(5, entries.Count(entry => entry.Payload.ContainsKey("message")
                && (entry.Payload["message"].ToString().ToLower() == "mainmessage" || entry.Payload["message"].ToString().ToLower().EndsWith("from process"))));

            // Should be TPL events
            Guid guid = new Guid("2e5dba47-a3d2-4d16-8ee0-6671ffdcd7b5");
            Assert.IsTrue(entries.Count(entry => entry.ProviderId == guid) > 0);
        }

        [TestMethod]
        public void WhenSamplingEnabledAndTriggerEventIsNotRaised()
        {
            var serviceConfigFile = "Configurations\\Sampling\\WhenEnablingSamplingForAPRocessAndTPLEvents.xml";
            string fileName = "WhenEnablingSamplingForAPRocessAndTPLEvents.log";

            IEnumerable<TestEventEntry> entries = null;
            Guid activityId;
            Guid oldActivityId;
            TraceEventServiceConfiguration svcConfiguration;

            InitializeTest(serviceConfigFile, fileName, out svcConfiguration, out activityId, out oldActivityId);
            try
            {
                TestScenario.WithConfiguration(svcConfiguration, () =>
                {
                    SamplingEventSource.Logger.BeforeEventToSample("mainmessage");
                    Task.Run(() => { SamplingEventSource.Logger.EventInATask("test"); waitObject.Set(); });
                    SamplingEventSource.Logger.AfterEventToSample("mainmessage");

                    waitObject.WaitOne();
                    entries = FlatFileHelper.PollUntilJsonEventsAreWritten<TestEventEntry>(fileName, 50);
                });
            }
            finally
            {
                EventSource.SetCurrentThreadActivityId(oldActivityId);
            }

            Assert.AreEqual(0, entries.Count());
        }

        [TestMethod]
        public void WhenServiceStartedBeforeProcessStarted()
        {
            var serviceConfigFile = "Configurations\\Sampling\\WhenServiceStartedBeforeProcessStarted.xml";
            string fileName = "WhenServiceStartedBeforeProcessStarted.log";

            IEnumerable<TestEventEntry> entries = null;
            Guid activityId;
            Guid oldActivityId;
            TraceEventServiceConfiguration svcConfiguration;

            InitializeTest(serviceConfigFile, fileName, out svcConfiguration, out activityId, out oldActivityId);

            using (var process = new Process())
            {
                try
                {
                    TestScenario.WithConfiguration(svcConfiguration, () =>
                    {
                        process.StartInfo = new ProcessStartInfo() { UseShellExecute = false, RedirectStandardInput = true, FileName = "ProcessToSample.exe" };
                        process.Start();

                        process.StandardInput.Write("x");
                        process.WaitForExit(1000);

                        entries = FlatFileHelper.PollUntilJsonEventsAreWritten<TestEventEntry>(fileName, 1000);
                    });
                }
                catch (Exception exp)
                {
                    Assert.Fail(exp.Message);
                }
            }

            Assert.AreEqual(6, entries.Count());
        }

        [TestMethod]
        public void WhenServiceStartedAfterProcessStarted()
        {
            var serviceConfigFile = "Configurations\\Sampling\\WhenServiceStartedAfterProcessStarted.xml";
            string fileName = "WhenServiceStartedAfterProcessStarted.log";

            IEnumerable<TestEventEntry> entries = null;
            Guid activityId;
            Guid oldActivityId;
            TraceEventServiceConfiguration svcConfiguration;

            using (var process = new Process())
            {
                process.StartInfo = new ProcessStartInfo() { UseShellExecute = false, RedirectStandardInput = true, FileName = "ProcessToSample.exe" };
                process.Start();

                InitializeTest(serviceConfigFile, fileName, out svcConfiguration, out activityId, out oldActivityId);
                try
                {
                    TestScenario.WithConfiguration(svcConfiguration, () =>
                    {
                        process.StandardInput.Write("x");
                        process.WaitForExit(1000);
                        entries = FlatFileHelper.PollUntilJsonEventsAreWritten<TestEventEntry>(fileName, 1000);
                    });
                }
                catch (Exception exp)
                {
                    Assert.Fail(exp.Message);
                }
            }

            Assert.AreEqual(6, entries.Count());
        }

        [TestMethod]
        public void WhenInvalidKeys()
        {
            var serviceConfigFile = "Configurations\\Sampling\\WhenInvalidKeysInConfiguration.xml";
            string fileName = "WhenInvalidKeysInConfiguration.log";

            IEnumerable<TestEventEntry> entries = null;
            Guid activityId;
            Guid oldActivityId;
            TraceEventServiceConfiguration svcConfiguration;

            InitializeTest(serviceConfigFile, fileName, out svcConfiguration, out activityId, out oldActivityId);
            try
            {
                TestScenario.WithConfiguration(svcConfiguration, () =>
                {
                    TriggerEventSource.Logger.TriggerEvent("triggermessage1");
                    SamplingEventSource.Logger.EventToSample("mainmessage1");

                    WriteMessagesWithNewActivityId();

                    entries = FlatFileHelper.PollUntilJsonEventsAreWritten<TestEventEntry>(fileName, 4);
                });
            }
            finally
            {
                EventSource.SetCurrentThreadActivityId(oldActivityId);
            }

            Assert.AreEqual(4, entries.Count());
        }

        [TestMethod]
        public void WhenInvalidKeyValue()
        {
            var serviceConfigFile = "Configurations\\Sampling\\WhenInvalidKeyValueInConfiguration.xml";
            string fileName = "WhenInvalidKeyValueInConfiguration.log";
            string errorFileName = "Errors" + fileName;
            File.Delete(errorFileName);

            IEnumerable<TestEventEntry> entries = null;
            Guid activityId;
            Guid oldActivityId;

            TraceEventServiceConfiguration svcConfiguration;

            InitializeTest(serviceConfigFile, fileName, out svcConfiguration, out activityId, out oldActivityId);

            try
            {
                TestScenario.WithConfiguration(svcConfiguration, () =>
                {
                    // Create a new TriggerEventSource instead of the normal approach of using singleton 
                    // This is to force the EventSource constructor to fire which is where
                    // the validation happens.
                    new TriggerEventSource().TriggerEvent("triggermessage1");
                    SamplingEventSource.Logger.EventToSample("mainmessage1");

                    Task.Delay(TimeSpan.FromSeconds(10)).Wait();
                });
            }
            finally
            {
                EventSource.SetCurrentThreadActivityId(oldActivityId);
            }

            entries = FlatFileHelper.PollUntilJsonEventsAreWritten<TestEventEntry>(errorFileName, 1);


            // Some events will be lost because of buffer overruns or schema synchronization delays in trace session
            // is the message reported by SLAB even though the real error is misconfiguration
            Assert.AreEqual(entries.First().EventId, 811);

            entries = FlatFileHelper.PollUntilJsonEventsAreWritten<TestEventEntry>(fileName, 1);
            Assert.AreEqual(0, entries.Count());
        }

        [TestMethod]
        public void WhenSamplingIsEnabledAndDisabledAtRunTime()
        {
            var serviceConfigFile = "Configurations\\Sampling\\WhenSamplingIsEnabledAndDisabledAtRunTime.xml";
            string fileName = "WhenSamplingIsEnabledAndDisabledAtRunTime.log";

            IEnumerable<TestEventEntry> entries = null;
            Guid activityId;
            Guid oldActivityId;
            TraceEventServiceConfiguration svcConfiguration;

            InitializeTest(serviceConfigFile, fileName, out svcConfiguration, out activityId, out oldActivityId, true);
            try
            {
                TestScenario.WithConfiguration(svcConfiguration, () =>
                {
                    TriggerEventSource.Logger.TriggerEvent("triggermessage1");
                    SamplingEventSource.Logger.EventToSample("mainmessage1");

                    WriteMessagesWithNewActivityId();

                    entries = FlatFileHelper.PollUntilJsonEventsAreWritten<TestEventEntry>(fileName, 4);
                    Assert.AreEqual(2, entries.Count());

                    string configuration = File.ReadAllText(serviceConfigFile);
                    configuration = configuration.Replace("true", "false");
                    File.WriteAllText(serviceConfigFile, configuration);

                    Task.Delay(TimeSpan.FromSeconds(1)).Wait();

                    WriteMessagesWithNewActivityId();
                    WriteMessagesWithNewActivityId();

                    entries = FlatFileHelper.PollUntilJsonEventsAreWritten<TestEventEntry>(fileName, 4);
                    Assert.AreEqual(5, entries.Count());
                });
            }
            finally
            {
                EventSource.SetCurrentThreadActivityId(oldActivityId);
            }
        }

        [TestMethod]
        public void WhenMultipleProcessesShareSameEventSource()
        {
            var serviceConfigFile = "Configurations\\Sampling\\WhenMultipleProcessesShareSameEventSource.xml";
            string fileName1 = "WhenMultipleProcessesShareSameEventSource1.log";
            string fileName2 = "WhenMultipleProcessesShareSameEventSource2.log";
            File.Delete(fileName2);

            IEnumerable<TestEventEntry> entries1 = null;
            IEnumerable<TestEventEntry> entries2 = null;
            Guid activityId;
            Guid oldActivityId;
            TraceEventServiceConfiguration svcConfiguration;

            InitializeTest(serviceConfigFile, fileName1, out svcConfiguration, out activityId, out oldActivityId);

            using (var process = new Process())
            {
                try
                {
                    TestScenario.WithConfiguration(svcConfiguration, () =>
                    {
                        process.StartInfo = new ProcessStartInfo() { UseShellExecute = false, RedirectStandardInput = true, FileName = "ProcessToSample.exe" };
                        process.Start();

                        TriggerEventSource.Logger.TriggerEvent("triggermessage1");
                        SamplingEventSource.Logger.EventToSample("mainmessage1");

                        process.StandardInput.Write("x");
                        process.WaitForExit(1000);
                        entries1 = FlatFileHelper.PollUntilJsonEventsAreWritten<TestEventEntry>(fileName1, 1000);
                        entries2 = FlatFileHelper.PollUntilJsonEventsAreWritten<TestEventEntry>(fileName2, 1000);
                    });
                }
                catch (Exception exp)
                {
                    Assert.Fail(exp.Message);
                }
            }

            Assert.AreEqual(2, entries1.Count());
            Assert.AreEqual(1, entries2.Count());
        }

        [TestMethod]
        public void WhenFilteringMultipleProcessesSameEventSource()
        {
            var serviceConfigFile = "Configurations\\Sampling\\WhenFilteringMultipleProcessesSameEventSource.xml";
            string fileName1 = "WhenFilteringMultipleProcessesSameEventSource.log";
            string fileName2 = "WhenFilteringMultipleProcessesSameEventSource2.log";
            File.Delete(fileName2);

            IEnumerable<TestEventEntry> entries1 = null;
            IEnumerable<TestEventEntry> entries2 = null;
            Guid activityId;
            Guid oldActivityId;
            TraceEventServiceConfiguration svcConfiguration;

            InitializeTest(serviceConfigFile, fileName1, out svcConfiguration, out activityId, out oldActivityId);

            using (var process = new Process())
            {
                try
                {
                    TestScenario.WithConfiguration(svcConfiguration, () =>
                    {
                        process.StartInfo = new ProcessStartInfo() { UseShellExecute = false, RedirectStandardInput = true, FileName = "ProcessToSample.exe" };
                        process.Start();

                        TriggerEventSource.Logger.TriggerEvent("triggermessage1");
                        SamplingEventSource.Logger.EventToSample("mainmessage1");

                        process.StandardInput.Write("x");
                        process.WaitForExit(1000);
                        entries1 = FlatFileHelper.PollUntilJsonEventsAreWritten<TestEventEntry>(fileName1, 3);
                        entries2 = FlatFileHelper.PollUntilJsonEventsAreWritten<TestEventEntry>(fileName2, 1);
                    });
                }
                catch (Exception exp)
                {
                    Assert.Fail(exp.Message);
                }
            }

            Assert.AreEqual(3, entries1.Count());
            Assert.AreEqual(1, entries2.Count());
        }

        private async Task LogEventsAsync()
        {
            TriggerEventSource.Logger.TriggerEvent("trigger event from process triggered");
            SamplingEventSource.Logger.BeforeEventToSample("message 1 from process");

            await Task.Delay(10);

            SamplingEventSource.Logger.AfterEventToSample("message 2 from process");
            waitObject.Set();
        }

        private void InitializeTest(string serviceConfigFile, string fileName, out TraceEventServiceConfiguration svcConfiguration, out Guid activityId, out Guid oldActivityId, bool shouldMonitorChanges = false)
        {
            File.Delete(fileName);

            svcConfiguration = TraceEventServiceConfiguration.Load(serviceConfigFile, shouldMonitorChanges);

            activityId = new Guid("368D6088-9F44-4735-B37F-3E76242583A1");
            oldActivityId = EventSource.CurrentThreadActivityId;
            EventSource.SetCurrentThreadActivityId(activityId);
        }

        private void WriteMessagesWithNewActivityId()
        {
            EventSource.SetCurrentThreadActivityId(Guid.NewGuid());

            TriggerEventSource.Logger.TriggerEvent("triggermessage2");
            SamplingEventSource.Logger.EventToSample("mainmessage2");
        }
    }
}
