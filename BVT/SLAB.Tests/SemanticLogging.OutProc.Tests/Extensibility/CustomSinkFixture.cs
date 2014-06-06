// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw.Configuration;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Formatters;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Observable;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.OutProc.Tests.TestObjects;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.OutProc.Tests.TestScenarios;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.Shared.TestObjects;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.Shared.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.OutProc.Tests.Extensibility
{
    [TestClass]
    public class CustomSinkFixture
    {
        [ClassInitialize]
        public static void Setup(TestContext testContext)
        {
            AssemblyLoaderHelper.EnsureAllAssembliesAreLoadedForSinkTest();
        }

        [TestMethod]
        public void WhenUsingCustomSinkWithSchema()
        {
            var validConnectionString = System.Configuration.ConfigurationManager.ConnectionStrings["valid"].ConnectionString;
            DatabaseHelper.CleanLoggingDB(validConnectionString);
            var svcConfiguration = TraceEventServiceConfiguration.Load("Configurations\\CustomSink\\CustomSqlSink.xml");

            System.Data.DataTable eventsDataTable = null;
            TestScenario.WithConfiguration(
                svcConfiguration,
                () =>
                {
                    var logger = MockEventSourceOutProc.Logger;
                    for (int n = 0; n < 10; n++)
                    {
                        logger.LogSomeMessage("some message" + n.ToString());
                    }

                    eventsDataTable = DatabaseHelper.PollUntilEventsAreWritten(validConnectionString, 10);
                });

            Assert.AreEqual(10, eventsDataTable.Rows.Count);
            StringAssert.Contains((string)eventsDataTable.Rows[0]["payload"], @"""message"": ""some message0""");
        }

        [TestMethod]
        public void WhenUsingCustomSinkBuiltInSinksForSameSource()
        {
            string fileName1 = "multipleFlatFile.log";
            File.Delete(fileName1);
            string fileName2 = "multipleMockFlatFile.log";
            File.Delete(fileName2);
            var validConnectionString = System.Configuration.ConfigurationManager.ConnectionStrings["valid"].ConnectionString;
            DatabaseHelper.CleanLoggingDB(validConnectionString);

            string message = string.Concat("Message ", Guid.NewGuid());
            string message2 = string.Concat("Message2 ", Guid.NewGuid());
            IEnumerable<string> entries = null;
            IEnumerable<string> entries2 = null;
            var svcConfiguration = TraceEventServiceConfiguration.Load("Configurations\\CustomSink\\Multiple.xml");
            TestScenario.WithConfiguration(
                svcConfiguration,
                () =>
                {
                    var logger = MockEventSourceOutProc.Logger;
                    logger.LogSomeMessage(message);
                    logger.LogSomeMessage(message2);
                    
                    entries = FlatFileHelper.PollUntilTextEventsAreWritten(fileName1, 2, "--==--");
                    entries2 = FlatFileHelper.PollUntilTextEventsAreWritten(fileName2, 2, "==-==");
                    DatabaseHelper.PollUntilEventsAreWritten(validConnectionString, 4);
                });

            Assert.IsTrue(File.Exists(fileName1));
            Assert.AreEqual<int>(2, entries.Count());
            StringAssert.Contains(entries.First().ToString(), message);
            StringAssert.Contains(entries.Last().ToString(), message2);

            Assert.IsTrue(File.Exists(fileName2));
            Assert.AreEqual<int>(2, entries2.Count());
            StringAssert.Contains(entries2.First().ToString(), message);
            StringAssert.Contains(entries2.Last().ToString(), message2);

            var dt = DatabaseHelper.GetLoggedTable(validConnectionString);
            Assert.AreEqual(4, dt.Rows.Count);

            var rowsWithMessage1 = dt.Select(string.Format("Payload like '%{0}%'", message));
            Assert.AreEqual(2, rowsWithMessage1.Count());
            var dr = rowsWithMessage1[0];
            Assert.AreEqual(4, (int)dr["Level"]);
            Assert.AreEqual(8, (int)dr["EventID"]);
            Assert.AreEqual("testingInstance", dr["InstanceName"].ToString());
            StringAssert.Contains((string)dr["Payload"], message);
            dr = rowsWithMessage1[1];
            Assert.AreEqual(4, (int)dr["Level"]);
            Assert.AreEqual(8, (int)dr["EventID"]);
            Assert.AreEqual("testingInstance", dr["InstanceName"].ToString());
            StringAssert.Contains((string)dr["Payload"], message);

            var rowsWithMessage2 = dt.Select(string.Format("Payload like '%{0}%'", message2));
            Assert.AreEqual(2, rowsWithMessage2.Count());
            dr = rowsWithMessage2[0];
            Assert.AreEqual(4, (int)dr["Level"]);
            Assert.AreEqual(8, (int)dr["EventID"]);
            Assert.AreEqual("testingInstance", dr["InstanceName"].ToString());
            StringAssert.Contains((string)dr["Payload"], message2);
            dr = rowsWithMessage2[1];
            Assert.AreEqual(4, (int)dr["Level"]);
            Assert.AreEqual(8, (int)dr["EventID"]);
            Assert.AreEqual("testingInstance", dr["InstanceName"].ToString());
            StringAssert.Contains((string)dr["Payload"], message2);
        }

        [TestMethod]
        public void WhenUsingCustomSinkWithoutSchema()
        {
            string fileName = "ProvidedCustomSink.log";
            File.Delete(fileName);
            var logger = MockEventSourceOutProc.Logger;

            IEnumerable<string> entries = null;
            var svcConfiguration = TraceEventServiceConfiguration.Load("Configurations\\CustomSink\\MockFlatFileSink.xml");
            TestScenario.WithConfiguration(
                svcConfiguration,
                () =>
                {
                    logger.LogSomeMessage("some message");
                    logger.LogSomeMessage("some message2");
                    logger.LogSomeMessage("some message3");

                    entries = FlatFileHelper.PollUntilTextEventsAreWritten(fileName, 3, "==-==");
                });

            Assert.AreEqual<int>(3, entries.Count());
            Assert.IsNotNull(entries.SingleOrDefault(e => e.Contains("Payload : [message : some message]")));
            Assert.IsNotNull(entries.SingleOrDefault(e => e.Contains("Payload : [message : some message2]")));
            Assert.IsNotNull(entries.SingleOrDefault(e => e.Contains("Payload : [message : some message3]")));
        }

        [TestMethod]
        public void WhenUsingCustomSinkProgrammatically()
        {
            string fileName = "ProvidedCustomSink.log";
            File.Delete(fileName);
            var logger = MockEventSourceOutProc.Logger;
            var formatter = new EventTextFormatter();

            IEnumerable<string> entries = null;
            var subject = new EventEntrySubject();
            subject.LogToMockFlatFile(fileName, "==-==");
            EventSourceSettings settings = new EventSourceSettings("MockEventSourceOutProc", null, EventLevel.LogAlways);
            SinkSettings sinkSettings = new SinkSettings("MockFlatFileSink", subject, new List<EventSourceSettings>() { { settings } });
            List<SinkSettings> sinks = new List<SinkSettings>() { { sinkSettings } };
            TraceEventServiceConfiguration svcConfiguration = new TraceEventServiceConfiguration(sinks);
            TestScenario.WithConfiguration(
                svcConfiguration,
                () =>
                {
                    logger.LogSomeMessage("some message");
                    logger.LogSomeMessage("some message2");
                    logger.LogSomeMessage("some message3");

                    entries = FlatFileHelper.PollUntilTextEventsAreWritten(fileName, 3, "==-==");
                });

            Assert.AreEqual<int>(3, entries.Count());
            Assert.IsNotNull(entries.SingleOrDefault(e => e.Contains("Payload : [message : some message]")));
            Assert.IsNotNull(entries.SingleOrDefault(e => e.Contains("Payload : [message : some message2]")));
            Assert.IsNotNull(entries.SingleOrDefault(e => e.Contains("Payload : [message : some message3]")));
        }

        [TestMethod]
        public void WhenCustomSinkConstructionFails()
        {
            var exc = ExceptionAssertHelper.Throws<Exception>(() => TraceEventServiceConfiguration.Load("Configurations\\CustomSink\\CustomSqlDBNotAllParams.xml"));

            StringAssert.Contains(exc.ToString(), "Value cannot be null");
            StringAssert.Contains(exc.ToString(), "Parameter name: instanceName");
        }

        [TestMethod]
        public void WhenUsingCustomSinkWithSchemaAndNotAllParametersProvided()
        {
            var exc = ExceptionAssertHelper.Throws<Exception>(() => TraceEventServiceConfiguration.Load("Configurations\\CustomSink\\CustomSinkMissingParam.xml"));

            StringAssert.Contains(exc.ToString(), "The parameters specified in this element does not map to an existing type member. All paramters are required in the same order of the defined type member");
        }

        [TestMethod]
        public void WhenUsingCustomSinkAndParamsAreNotInOrder()
        {
            var exc = ExceptionAssertHelper.Throws<Exception>(() => TraceEventServiceConfiguration.Load("Configurations\\CustomSink\\CustomSinkDiffOrder.xml"));

            StringAssert.Contains(exc.ToString(), "The parameters specified in this element does not map to an existing type member. All paramters are required in the same order of the defined type member");
        }

        [TestMethod]
        public void WhenUsingCustomFormatter()
        {
            string fileName = "FlatFileOutProcConfigCF2.log";
            File.Delete(fileName);
            var logger = MockEventSourceOutProc.Logger;

            IEnumerable<string> entries = null;
            var svcConfiguration = TraceEventServiceConfiguration.Load("Configurations\\CustomSink\\FlatFileCustomFormatter2.xml");
            TestScenario.WithConfiguration(
                svcConfiguration,
                () =>
                {
                    logger.LogSomeMessage("some message using formatter");

                    entries = FlatFileHelper.PollUntilTextEventsAreWritten(fileName, 1, "----------");
                });

            StringAssert.Contains(entries.First(), "Mock SourceId");
            StringAssert.Contains(entries.First(), "Mock EventId");
            StringAssert.Contains(entries.First(), "Payload : [message : some message using formatter]");
        }

        [TestMethod]
        public void WhenUsingCustomFormatterProgramatically()
        {
            string fileName = "FlatFileCustomFormatterProgrammatic.log";
            File.Delete(fileName);
            var logger = MockEventSourceOutProc.Logger;
            CustomFormatterWithWait formatter = new CustomFormatterWithWait();
            formatter.Detailed = EventLevel.LogAlways;
            formatter.Header = "---------------";
            formatter.DateTimeFormat = "d";

            IEnumerable<string> entries = null;
            EventSourceSettings settings = new EventSourceSettings("MockEventSourceOutProc", null, EventLevel.Critical);
            var subject = new EventEntrySubject();
            subject.LogToFlatFile(fileName, formatter);
            SinkSettings sinkSettings = new SinkSettings("flatFileSink", subject, new List<EventSourceSettings>() { { settings } });
            List<SinkSettings> sinks = new List<SinkSettings>() { { sinkSettings } };
            TraceEventServiceConfiguration svcConfiguration = new TraceEventServiceConfiguration(sinks);
            TestScenario.WithConfiguration(
                svcConfiguration,
                () =>
                {
                    logger.Critical("some message using formatter");

                    entries = FlatFileHelper.PollUntilTextEventsAreWritten(fileName, 1, "---------------");
                });

            StringAssert.Contains(entries.First(), "Mock SourceId");
            StringAssert.Contains(entries.First(), "Mock EventId");
            StringAssert.Contains(entries.First(), "Payload : [message : some message using formatter]");
        }

        [TestMethod]
        public void WhenCustomFormatterThrowsAnExceptionAndUsedProgramatically()
        {
            string fileName = "FlatFileOutProcCustomFormatterHandleException.log";
            File.Delete(fileName);
            var logger = MockEventSourceOutProc.Logger;
            MockFormatter formatter = new MockFormatter(true); //this formatter throws

            EventSourceSettings settings = new EventSourceSettings("MockEventSourceOutProc", null, EventLevel.Informational);
            var subject = new EventEntrySubject();
            subject.LogToFlatFile(fileName, formatter);
            SinkSettings sinkSettings = new SinkSettings("flatFileSink", subject, new List<EventSourceSettings>() { { settings } });
            List<SinkSettings> sinks = new List<SinkSettings>() { { sinkSettings } };
            TraceEventServiceConfiguration svcConfiguration = new TraceEventServiceConfiguration(sinks);
            TestScenario.WithConfiguration(
                svcConfiguration,
                () =>
                {
                    using (var collectErrorsListener = new InMemoryEventListener())
                    {
                        try
                        {
                            collectErrorsListener.EnableEvents(SemanticLoggingEventSource.Log, EventLevel.LogAlways, Keywords.All);

                            logger.LogSomeMessage("some message using formatter that throws");
                            collectErrorsListener.WaitEvents.Wait(5000);

                            StringAssert.Contains(collectErrorsListener.ToString(), "Payload : [message : System.InvalidOperationException: Operation is not valid due to the current state of the object.");
                        }
                        finally
                        {
                            collectErrorsListener.DisableEvents(SemanticLoggingEventSource.Log);
                        }
                    }
                });
        }

        [TestMethod]
        public void WhenCustomFormatterThrowsAnExceptionAndUsedConfig()
        {
            string fileName = "FlatFileOutProcCustomFormatterHandleExceptionViaConfig.log";
            File.Delete(fileName);
            var logger = MockEventSourceOutProc.Logger;
            MockFormatter formatter = new MockFormatter(true); //this formatter throws

            TraceEventServiceConfiguration svcConfiguration = TraceEventServiceConfiguration.Load("Configurations\\CustomSink\\FlatFileCustomFormatter.xml");
            TestScenario.WithConfiguration(
                svcConfiguration,
                () =>
                {
                    using (InMemoryEventListener collectErrorsListener = new InMemoryEventListener())
                    {
                        try
                        {
                            collectErrorsListener.EnableEvents(SemanticLoggingEventSource.Log, EventLevel.Error, Keywords.All);
                            logger.LogSomeMessage("some message using formatter that throws");
                            collectErrorsListener.WaitEvents.Wait(5000);

                            StringAssert.Contains(collectErrorsListener.ToString(), "Payload : [message : System.InvalidOperationException: Operation is not valid due to the current state of the object.");
                        }
                        finally
                        {
                            collectErrorsListener.DisableEvents(SemanticLoggingEventSource.Log);
                        }
                    }
                });
        }
    }
}
