// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Formatters;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.InProc.Tests.TestObjects;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.InProc.Tests.TestScenarios;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.Shared.TestObjects;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.Shared.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.InProc.Tests.Extensibility
{
    [TestClass]
    public class CustomSinkFixture
    {
        [TestMethod]
        public void WhenUsingCustomSink()
        {
            var validConnectionString = System.Configuration.ConfigurationManager.ConnectionStrings["valid"].ConnectionString;
            DatabaseHelper.CleanLoggingDB(validConnectionString);
            var logger = MockEventSource.Logger;

            string message = string.Concat("Message ", Guid.NewGuid());
            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToCustomSqlDatabase("TestInstanceName", validConnectionString);
                    listener.EnableEvents(logger, System.Diagnostics.Tracing.EventLevel.LogAlways, Keywords.All);
                    logger.LogSomeMessage(message);
                });

            var dt = DatabaseHelper.GetLoggedTable(validConnectionString);
            Assert.AreEqual(1, dt.Rows.Count);
            var dr = dt.Rows[0];
            Assert.AreEqual(4, (int)dr["Level"]);
            Assert.AreEqual(8, (int)dr["EventID"]);
            Assert.AreEqual("TestInstanceName", dr["InstanceName"].ToString());
            StringAssert.Contains((string)dr["Payload"], message);
        }

        [TestMethod]
        public void WhenUsingCustomSinkAndMultipleEvents()
        {
            string fileName = "ProvidedCustomSink.log";
            File.Delete(fileName);
            var logger = MockEventSource.Logger;

            IEnumerable<string> entries = null;
            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToMockFlatFile(fileName, "==-==");
                    listener.EnableEvents(logger, System.Diagnostics.Tracing.EventLevel.LogAlways, Keywords.All);
                    logger.LogSomeMessage("some message");
                    logger.LogSomeMessage("some message2");
                    logger.LogSomeMessage("some message3");

                    entries = FlatFileHelper.PollUntilTextEventsAreWritten(fileName, 3, "==-==");
                });

            Assert.IsTrue(File.Exists(fileName));
            Assert.AreEqual<int>(3, entries.Count());
            Assert.IsNotNull(entries.SingleOrDefault(e => e.Contains("Payload : [message : some message]")));
            Assert.IsNotNull(entries.SingleOrDefault(e => e.Contains("Payload : [message : some message2]")));
            Assert.IsNotNull(entries.SingleOrDefault(e => e.Contains("Payload : [message : some message3]")));
        }

        [TestMethod]
        public void WhenMultipleCustomSinksSubscribing()
        {
            string fileName1 = "mockFlatFileMutiple.log";
            File.Delete(fileName1);
            string fileName2 = "flatFileMultiple.log";
            File.Delete(fileName2);
            var validConnectionString = System.Configuration.ConfigurationManager.ConnectionStrings["valid"].ConnectionString;
            DatabaseHelper.CleanLoggingDB(validConnectionString);
            var logger = MockEventSource.Logger;

            string message = string.Concat("Message ", Guid.NewGuid());
            string message2 = string.Concat("Message2 ", Guid.NewGuid());
            IEnumerable<string> entries = null;
            IEnumerable<string> entries2 = null;
            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToMockFlatFile(fileName1, "==-==");
                    listener.LogToFlatFile(fileName2, new EventTextFormatter("--==--"));
                    listener.LogToSqlDatabase("testInstance", validConnectionString, "Traces", TimeSpan.Zero, 1);
                    listener.LogToCustomSqlDatabase("testCustom", validConnectionString);
                    listener.EnableEvents(logger, System.Diagnostics.Tracing.EventLevel.LogAlways, Keywords.All);
                    logger.LogSomeMessage(message);
                    logger.LogSomeMessage(message2);

                    entries = FlatFileHelper.PollUntilTextEventsAreWritten(fileName1, 2, "==-==");
                    entries2 = FlatFileHelper.PollUntilTextEventsAreWritten(fileName2, 2, "--==--");
                });

            Assert.IsTrue(File.Exists(fileName1));
            Assert.AreEqual<int>(2, entries.Count());
            StringAssert.Contains(entries.First().ToString(), message);
            StringAssert.Contains(entries.Last().ToString(), message2);

            Assert.IsTrue(File.Exists(fileName2));
            Assert.AreEqual<int>(2, entries.Count());
            StringAssert.Contains(entries.First().ToString(), message);
            StringAssert.Contains(entries.Last().ToString(), message2);

            var dt = DatabaseHelper.GetLoggedTable(validConnectionString);
            Assert.AreEqual(4, dt.Rows.Count);
        }

        [TestMethod]
        public void WhenExceptinOccursInCustomFormater1()
        {
            string filename = "customFormatterException.log";
            File.Delete(filename);
            var logger = MockEventSource.Logger;
            var formatter = new CustomFormatter(true);

            TestScenario.With1Listener(
                logger,
                (listener, errorsListener) =>
                {
                    listener.LogToFlatFile(filename, formatter);
                    listener.EnableEvents(logger, System.Diagnostics.Tracing.EventLevel.LogAlways, Keywords.All);
                    logger.LogSomeMessage("testing");

                    errorsListener.WaitEvents.Wait(3000);
                    StringAssert.Contains(errorsListener.ToString(), "unhandled exception from formatter");
                });
        }

        [TestMethod]
        public void WhenExceptionOccursInCustomFormatter()
        {
            string fileName = "FlatFileInProcCustomFormatterHandleException.log";
            File.Delete(fileName);
            var logger = TestEventSourceNonTransient.Logger;

            TestScenario.With1Listener(
                logger,
                (listener, errorsListener) =>
                {
                    listener.LogToFlatFile(fileName, new MockFormatter(true));
                    listener.EnableEvents(logger, System.Diagnostics.Tracing.EventLevel.LogAlways);
                    logger.EventWithPayload("payload1", 100);

                    StringAssert.Contains(errorsListener.ToString(), "Payload : [message : System.InvalidOperationException: Operation is not valid due to the current state of the object.");
                });
        }

        [TestMethod]
        public void WhenUsingCustomFormatter()
        {
            string fileName = "FlatFileInProcCustomFormatter.log";
            File.Delete(fileName);
            string header = "----------";
            var logger = TestEventSourceNonTransient.Logger;
            var formatter = new CustomFormatterWithWait(header);
            formatter.Detailed = System.Diagnostics.Tracing.EventLevel.LogAlways;

            IEnumerable<string> entries = null;
            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToFlatFile(fileName, formatter);
                    listener.EnableEvents(logger, System.Diagnostics.Tracing.EventLevel.LogAlways);

                    logger.EventWithPayload("payload1", 100);

                    entries = FlatFileHelper.PollUntilTextEventsAreWritten(fileName, 1, header);
                });

            StringAssert.Contains(entries.First(), "Mock SourceId");
            StringAssert.Contains(entries.First(), "Mock EventId");
            StringAssert.Contains(entries.First(), "Payload : [payload1 : payload1] [payload2 : 100]");
        }
    }
}
