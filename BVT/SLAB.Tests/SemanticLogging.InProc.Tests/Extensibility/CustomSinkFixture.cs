// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Formatters;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.InProc.Tests.TestObjects;
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
            using (var eventListener = new ObservableEventListener())
            {
                try
                {
                    eventListener.LogToCustomSqlDatabase("TestInstanceName", validConnectionString);
                    eventListener.EnableEvents(logger, System.Diagnostics.Tracing.EventLevel.LogAlways, Keywords.All);
                    logger.LogSomeMessage(message);  
                }
                finally
                {
                    eventListener.DisableEvents(logger);
                }
            }

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
            using (var eventListener = new ObservableEventListener())
            {
                try
                {
                    eventListener.LogToMockFlatFile(fileName, "==-==");
                    eventListener.EnableEvents(logger, System.Diagnostics.Tracing.EventLevel.LogAlways, Keywords.All);
                    logger.LogSomeMessage("some message");
                    logger.LogSomeMessage("some message2");
                    logger.LogSomeMessage("some message3");

                    entries = FlatFileHelper.PollUntilTextEventsAreWritten(fileName, 3, "==-==");
                }
                finally
                {
                    eventListener.DisableEvents(logger);
                }
            }

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
            using (var eventListener = new ObservableEventListener())
            {
                try
                {
                    eventListener.LogToMockFlatFile(fileName1, "==-==");
                    eventListener.LogToFlatFile(fileName2, new EventTextFormatter("--==--"));
                    eventListener.LogToSqlDatabase("testInstance", validConnectionString, "Traces", TimeSpan.Zero, 1);
                    eventListener.LogToCustomSqlDatabase("testCustom", validConnectionString);
                    eventListener.EnableEvents(logger, System.Diagnostics.Tracing.EventLevel.LogAlways, Keywords.All);
                    logger.LogSomeMessage(message);
                    logger.LogSomeMessage(message2);

                    entries = FlatFileHelper.PollUntilTextEventsAreWritten(fileName1, 2, "==-==");
                    entries2 = FlatFileHelper.PollUntilTextEventsAreWritten(fileName2, 2, "--==--");
                }
                finally
                {
                    eventListener.DisableEvents(logger);
                }
            }

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

            var dr = dt.Rows[0];
            Assert.AreEqual(4, (int)dr["Level"]);
            Assert.AreEqual(8, (int)dr["EventID"]);
            Assert.AreEqual("testInstance", dr["InstanceName"].ToString());
            StringAssert.Contains((string)dr["Payload"], message);

            dr = dt.Rows[1];
            Assert.AreEqual(4, (int)dr["Level"]);
            Assert.AreEqual(8, (int)dr["EventID"]);
            Assert.AreEqual("testInstance", dr["InstanceName"].ToString());
            StringAssert.Contains((string)dr["Payload"], message2);

            dr = dt.Rows[2];
            Assert.AreEqual(4, (int)dr["Level"]);
            Assert.AreEqual(8, (int)dr["EventID"]);
            Assert.AreEqual("testCustom", dr["InstanceName"].ToString());
            StringAssert.Contains((string)dr["Payload"], message);

            dr = dt.Rows[3];
            Assert.AreEqual(4, (int)dr["Level"]);
            Assert.AreEqual(8, (int)dr["EventID"]);
            Assert.AreEqual("testCustom", dr["InstanceName"].ToString());
            StringAssert.Contains((string)dr["Payload"], message2);
        }

        [TestMethod]
        public void WhenExceptinOCcursInCustomFormater1()
        {
            string filename = "customFormatterException.log";
            File.Delete(filename);
            var logger = MockEventSource.Logger;
            var formatter = new CustomFormatter(true);

            using (var listener = new ObservableEventListener())
            using (var collectErrorsListener = new InMemoryEventListener(true))
            {
                try
                {
                    collectErrorsListener.EnableEvents(SemanticLoggingEventSource.Log, System.Diagnostics.Tracing.EventLevel.LogAlways, Keywords.All);
                    listener.LogToFlatFile(filename, formatter);
                    listener.EnableEvents(logger, System.Diagnostics.Tracing.EventLevel.LogAlways, Keywords.All);
                    logger.LogSomeMessage("testing");

                    collectErrorsListener.WaitEvents.Wait(3000);
                    StringAssert.Contains(collectErrorsListener.ToString(), "unhandled exception from formatter");
                }
                finally
                {
                    listener.DisableEvents(logger);
                    collectErrorsListener.DisableEvents(SemanticLoggingEventSource.Log);
                }
            }
        }

        [TestMethod]
        public void WhenExceptionOccursInCustomFormatter()
        {
            string fileName = "FlatFileInProcCustomFormatterHandleException.log";
            File.Delete(fileName);
            var logger = TestEventSourceNonTransient.Logger;

            using (var listener = new ObservableEventListener())
            using (var collectErrorsListener = new InMemoryEventListener(true))
            {
                try
                {
                    collectErrorsListener.EnableEvents(SemanticLoggingEventSource.Log, System.Diagnostics.Tracing.EventLevel.Error, Keywords.All);
                    listener.LogToFlatFile(fileName, new MockFormatter(true));
                    listener.EnableEvents(logger, System.Diagnostics.Tracing.EventLevel.LogAlways);
                    logger.EventWithPayload("payload1", 100);

                    StringAssert.Contains(collectErrorsListener.ToString(), "Payload : [message : System.InvalidOperationException: Operation is not valid due to the current state of the object.");
                }
                finally
                {
                    listener.DisableEvents(logger);
                    collectErrorsListener.DisableEvents(SemanticLoggingEventSource.Log);
                }
            }
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
            using (var listener = new ObservableEventListener())
            {
                try
                {
                    listener.LogToFlatFile(fileName, formatter);
                    listener.EnableEvents(logger, System.Diagnostics.Tracing.EventLevel.LogAlways);

                    logger.EventWithPayload("payload1", 100);

                    entries = FlatFileHelper.PollUntilTextEventsAreWritten(fileName, 1, header);
                }
                finally
                {
                    listener.DisableEvents(logger);
                }
            }

            StringAssert.Contains(entries.First(), "Mock SourceId");
            StringAssert.Contains(entries.First(), "Mock EventId");
            StringAssert.Contains(entries.First(), "Payload : [payload1 : payload1] [payload2 : 100]");
        }
    }
}
