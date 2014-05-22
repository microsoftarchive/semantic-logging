// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Formatters;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.InProc.Tests.TestObjects;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.InProc.Tests.TestScenarios;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.Shared.TestSupport;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.InProc.Tests.Sinks
{
    [TestClass]
    public class ConsoleSinkFixture
    {
        [TestMethod]
        public void WhenDefaultColorMappingForInformational()
        {
            var consoleOutputInterceptor = new MockConsoleOutputInterceptor();
            var formatter = new EventTextFormatter(EventTextFormatter.DashSeparator);
            var logger = MockConsoleListenerEventSource.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToConsole();
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    logger.Informational("This is to log information in Console");
                });

            var entry = Regex.Split(consoleOutputInterceptor.Ouput, formatter.Header).Where(c => !string.IsNullOrWhiteSpace(c)).SingleOrDefault();
            Assert.IsNotNull(entry);
            StringAssert.Contains(entry, "100");
            Assert.AreEqual(DefaultConsoleColorMapper.Informational, consoleOutputInterceptor.OutputForegroundColor);
        }

        [TestMethod]
        public void WhenDefaultColorMappingForError()
        {
            var consoleOutputInterceptor = new MockConsoleOutputInterceptor();
            var formatter = new EventTextFormatter(EventTextFormatter.DashSeparator);
            var logger = MockConsoleListenerEventSource.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToConsole();
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    logger.Error("This is to log error in Console");
                });

            var entry = Regex.Split(consoleOutputInterceptor.Ouput, formatter.Header).Where(c => !string.IsNullOrWhiteSpace(c)).SingleOrDefault();
            Assert.IsNotNull(entry);
            StringAssert.Contains(entry, "300");
            Assert.AreEqual(DefaultConsoleColorMapper.Error, consoleOutputInterceptor.OutputForegroundColor);
        }

        [TestMethod]
        public void WhenDefaultColorMappingForCritical()
        {
            var consoleOutputInterceptor = new MockConsoleOutputInterceptor();
            var formatter = new EventTextFormatter(EventTextFormatter.DashSeparator);
            var logger = MockConsoleListenerEventSource.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToConsole();
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    logger.Critical("This is to log critical in Console");
                });

            var entry = Regex.Split(consoleOutputInterceptor.Ouput, formatter.Header).Where(c => !string.IsNullOrWhiteSpace(c)).SingleOrDefault();
            Assert.IsNotNull(entry);
            StringAssert.Contains(entry, "200");
            Assert.AreEqual(DefaultConsoleColorMapper.Critical, consoleOutputInterceptor.OutputForegroundColor);
        }

        [TestMethod]
        public void WhenDefaultColorMappingForVerbose()
        {
            var consoleOutputInterceptor = new MockConsoleOutputInterceptor();
            var formatter = new EventTextFormatter(EventTextFormatter.DashSeparator);
            var logger = MockConsoleListenerEventSource.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToConsole();
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    logger.Verbose("This is to log verbose in Console");
                });

            var entry = Regex.Split(consoleOutputInterceptor.Ouput, formatter.Header).Where(c => !string.IsNullOrWhiteSpace(c)).SingleOrDefault();
            Assert.IsNotNull(entry);
            StringAssert.Contains(entry, "400");
            Assert.AreEqual(DefaultConsoleColorMapper.Verbose, consoleOutputInterceptor.OutputForegroundColor);
        }

        [TestMethod]
        public void WhenOneSourceTwoListeners()
        {
            var consoleOutputInterceptor = new MockConsoleOutputInterceptor();
            var formatter = new EventTextFormatter(EventTextFormatter.DashSeparator);
            var logger = MockConsoleListenerEventSource.Logger;

            string errorMessage = string.Concat("Error ", Guid.NewGuid());
            string infoMessage = string.Concat("Message", Guid.NewGuid());
            TestScenario.With2Listeners(
                logger,
                (listener1, listener2) =>
                {
                    listener1.LogToConsole(new EventTextFormatter(), null);
                    listener2.LogToConsole();
                    listener1.EnableEvents(logger, EventLevel.Informational);
                    listener2.EnableEvents(logger, EventLevel.Error);
                    logger.Informational(infoMessage);
                    logger.Error(errorMessage);
                });

            var entry = Regex.Split(consoleOutputInterceptor.Ouput, formatter.Header).Where(c => !string.IsNullOrWhiteSpace(c)).SingleOrDefault();
            Assert.IsNotNull(entry);
            StringAssert.Contains(entry, "100");
            StringAssert.Contains(entry, "300");
        }

        [TestMethod]
        public void WhenOneListenerTwoSources()
        {
            var consoleOutputInterceptor = new MockConsoleOutputInterceptor();
            var formatter = new EventTextFormatter(EventTextFormatter.DashSeparator);
            var logger = MockConsoleListenerEventSource.Logger;
            var logger2 = MockConsoleListenerEventSource2.Logger;

            string message = string.Concat("Message ", Guid.NewGuid());
            string errorMessage = string.Concat("Error ", Guid.NewGuid());
            TestScenario.With1Listener(
                new EventSource[] { logger, logger2 },
                listener =>
                {
                    listener.LogToConsole();
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    listener.EnableEvents(logger2, EventLevel.LogAlways);
                    logger.Informational(message);
                    logger2.Informational(message);
                });

            var entry = Regex.Split(consoleOutputInterceptor.Ouput, formatter.Header).Where(c => !string.IsNullOrWhiteSpace(c)).SingleOrDefault();
            Assert.IsNotNull(entry);
            StringAssert.Contains(entry, "100");
        }

        [TestMethod]
        public void WhenMultipleEvents()
        {
            var consoleOutputInterceptor = new MockConsoleOutputInterceptor();
            var formatter = new EventTextFormatter(EventTextFormatter.DashSeparator);
            var logger = MockConsoleListenerEventSource.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    listener.LogToConsole();
                    for (int n = 0; n < 300; n++)
                    {
                        logger.Informational("Some message to console " + n);
                    }
                });

            var output = Regex.Split(consoleOutputInterceptor.Ouput, formatter.Header).Where(c => !string.IsNullOrWhiteSpace(c));
            Assert.IsNotNull(output);
            StringAssert.Contains(output.First(), "Some message to console 0");
            StringAssert.Contains(output.First(), "Some message to console 299");
        }

        [TestMethod]
        public void WhenOneSourceTwoListenersConcurrently()
        {
            var consoleOutputInterceptor = new MockConsoleOutputInterceptor();
            var formatter = new EventTextFormatter(EventTextFormatter.DashSeparator);
            var logger = MockConsoleListenerEventSource.Logger;

            TestScenario.With2Listeners(
                logger,
                (listener1, listener2) =>
                {
                    listener1.LogToConsole();
                    listener2.LogToConsole();
                    int maxLoggedEntries = 9;
                    string criticalMessage = string.Concat("CriticalMessage");
                    string infoMessage = string.Concat("InfoMessage");
                    listener1.EnableEvents(logger, EventLevel.Critical);
                    listener2.EnableEvents(logger, EventLevel.Critical);
                    Parallel.Invoke(Enumerable.Range(0, maxLoggedEntries).Select(i =>
                    new Action(() =>
                    {
                        logger.Critical(i + criticalMessage);
                    })).ToArray());
                });

            var entry = Regex.Split(consoleOutputInterceptor.Ouput, formatter.Header).Where(c => !string.IsNullOrWhiteSpace(c)).SingleOrDefault();
            Assert.IsNotNull(entry);
        }

        [TestMethod]
        public void WhenPayloadHasDifferentTypes()
        {
            var consoleOutputInterceptor = new MockConsoleOutputInterceptor();
            var formatter = new EventTextFormatter(EventTextFormatter.DashSeparator);
            var logger = MockEventSourceNoTask.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToConsole();
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    logger.DifferentTypes("testString", 500000);
                });

            var entry = Regex.Split(consoleOutputInterceptor.Ouput, formatter.Header).Where(c => !string.IsNullOrWhiteSpace(c)).SingleOrDefault();
            Assert.IsNotNull(entry);
            StringAssert.Contains(entry, @"strArg : testString");
            StringAssert.Contains(entry, @"longArg : 500000");
        }

        [TestMethod]
        public void WhenPayloadHasDifferentTypesAndNull()
        {
            var consoleOutputInterceptor = new MockConsoleOutputInterceptor();
            var formatter = new EventTextFormatter(EventTextFormatter.DashSeparator);
            var logger = MockEventSourceNoTask.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToConsole();
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    logger.DifferentTypes(null, 500000);
                });

            var entry = Regex.Split(consoleOutputInterceptor.Ouput, formatter.Header).Where(c => !string.IsNullOrWhiteSpace(c)).SingleOrDefault();
            Assert.IsNotNull(entry);
            Assert.IsTrue(entry.Count() > 0);
            StringAssert.Contains(entry, @"strArg : ");
            StringAssert.Contains(entry, @"longArg : 500000");
        }

        [TestMethod]
        public void WhenEventHasRawMessageAndFormattedMessage()
        {
            var consoleOutputInterceptor = new MockConsoleOutputInterceptor();
            var formatter = new EventTextFormatter(EventTextFormatter.DashSeparator);
            var logger = MockEventSourceNoTask.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToConsole();
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    logger.Informational("testing");
                });

            var entry = Regex.Split(consoleOutputInterceptor.Ouput, formatter.Header).Where(c => !string.IsNullOrWhiteSpace(c)).SingleOrDefault();
            Assert.IsNotNull(entry);
            Assert.IsTrue(entry.Count() > 0);
            StringAssert.Contains(entry, "message param");
        }

        [TestMethod]
        public void WhenHighEventIds()
        {
            var consoleOutputInterceptor = new MockConsoleOutputInterceptor();
            var formatter = new EventTextFormatter(EventTextFormatter.DashSeparator);
            var logger = MockHighEventIdEventSource.HigheventIdLogger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToConsole();
                    listener.EnableEvents(logger, EventLevel.Warning);
                    logger.Warning();
                });

            var entry = Regex.Split(consoleOutputInterceptor.Ouput, formatter.Header).Where(c => !string.IsNullOrWhiteSpace(c)).SingleOrDefault();
            Assert.IsNotNull(entry);
        }

        [TestMethod]
        public void WhenLowEventIds()
        {
            var consoleOutputInterceptor = new MockConsoleOutputInterceptor();
            var formatter = new EventTextFormatter(EventTextFormatter.DashSeparator);
            var logger = MockNegativeEventIdEventSource.LoweventIdLogger;

            try
            {
                TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.EnableEvents(logger, EventLevel.Warning);
                    Assert.IsFalse(true, "Should throw when calling EnableEvents.");
                });
            }
            catch (ArgumentException ex)
            {
                Assert.AreEqual("Event IDs must be positive integers.", ex.Message);
            }
        }

        [TestMethod]
        public void WhenLoggingErrorOCcurs()
        {
            var mockConsole = new MockConsoleOutputInterceptor();
            var logger = TestEventSource.Logger;

            TestScenario.With1Listener(
                logger,
                (listener, errorsListener) =>
                {
                    listener.LogToConsole(new MockFormatter(true));
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    logger.EventWithPayload("payload1", 100);

                    StringAssert.Contains(errorsListener.ToString(), "System.InvalidOperationException: Operation is not valid due to the current state of the object.");
                    Assert.AreEqual(string.Empty, mockConsole.Ouput);
                });
        }

        [TestMethod]
        public void WhenSingleLineTextFormatter()
        {
            var consoleOutputInterceptor = new MockConsoleOutputInterceptor();
            var formatter = new EventTextFormatter(EventTextFormatter.DashSeparator);
            var logger = MockConsoleListenerEventSource.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToConsole(formatter);
                    listener.EnableEvents(logger, EventLevel.Error);
                    logger.Critical("This is to log critical in Console");
                    logger.Verbose("This is should not be logged in Console");
                });

            var entry = Regex.Split(consoleOutputInterceptor.Ouput, formatter.Header).Where(c => !string.IsNullOrWhiteSpace(c)).SingleOrDefault();
            Assert.IsNotNull(entry);
            Assert.IsFalse(entry.Contains("This is should not be logged in Console"));
            Assert.IsTrue(entry.Contains("This is to log critical in Console"));
            Assert.IsTrue(entry.Contains("\r\nProviderId : "));
            Assert.IsTrue(entry.Contains("\r\nEventId : 200\r\nKeywords : None\r\nLevel : Critical\r\nMessage : Functional Test\r\nOpcode : Info\r\nTask : 65334\r\nVersion : 0\r\nPayload : [message : This is to log critical in Console] \r\nEventName : CriticalInfo\r\nTimestamp :"));
        }

        [TestMethod]
        public void WhenEnablingAllKeywords()
        {
            var consoleOutputInterceptor = new MockConsoleOutputInterceptor();
            var formatter = new EventTextFormatter("----", "====", EventLevel.LogAlways);
            var logger = MockConsoleListenerEventSource.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.EnableEvents(logger, EventLevel.LogAlways, Keywords.All);
                    listener.LogToConsole(formatter);
                    logger.InfoWithKeywordDiagnostic("Info with keyword Diagnostic");
                });

            var entry = Regex.Split(consoleOutputInterceptor.Ouput, formatter.Header).Where(c => !string.IsNullOrWhiteSpace(c)).SingleOrDefault();
            Assert.IsNotNull(entry);
            Assert.IsTrue(entry.Contains("ProviderId : "));
            Assert.IsTrue(entry.Contains("\r\nEventId : 1020\r\nKeywords : 4\r\nLevel : Informational\r\nMessage : \r\nOpcode : Info\r\nTask : 1\r\nVersion : 0\r\nPayload : [message : Info with keyword Diagnostic] \r\nEventName : PageInfo\r\nTimestamp : "));
        }

        [TestMethod]
        public void WhenSourceEnabledAndNotSpecifyingKeyword()
        {
            var consoleOutputInterceptor = new MockConsoleOutputInterceptor();
            var formatter = new EventTextFormatter(EventTextFormatter.DashSeparator);
            var logger = MockConsoleListenerEventSource.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToConsole();
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    logger.InfoWithKeywordDiagnostic("Info with keyword Diagnostic");
                });

            var entry = Regex.Split(consoleOutputInterceptor.Ouput, formatter.Header).Where(c => !string.IsNullOrWhiteSpace(c)).SingleOrDefault();
            Assert.IsNull(entry);
        }

        [TestMethod]
        public void WhenCriticalVerbosityForFormatter()
        {
            var consoleOutputInterceptor = new MockConsoleOutputInterceptor();
            var formatter = new EventTextFormatter(EventTextFormatter.DashSeparator);
            var logger = MockConsoleListenerEventSource.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToConsole(formatter);
                    formatter.VerbosityThreshold = EventLevel.Critical;
                    listener.EnableEvents(logger, EventLevel.Critical);
                    logger.Critical("This is to log critical in Console");
                });

            var entry = Regex.Split(consoleOutputInterceptor.Ouput, formatter.Header).Where(c => !string.IsNullOrWhiteSpace(c)).SingleOrDefault();
            Assert.IsNotNull(entry);
            StringAssert.Contains(entry, "200");
            StringAssert.Contains(entry, "Keywords : None");
            Assert.AreEqual(DefaultConsoleColorMapper.Critical, consoleOutputInterceptor.OutputForegroundColor);
        }

        [TestMethod]
        public void WhenDefaultFormatter()
        {
            var consoleOutputInterceptor = new MockConsoleOutputInterceptor();
            var formatter = new EventTextFormatter(EventTextFormatter.DashSeparator);
            var logger = MockConsoleListenerEventSource.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToConsole();
                    listener.EnableEvents(logger, EventLevel.Critical);
                    logger.Critical("This is to log critical in Console");
                });

            var entry = Regex.Split(consoleOutputInterceptor.Ouput, formatter.Header).Where(c => !string.IsNullOrWhiteSpace(c)).SingleOrDefault();
            Assert.IsNotNull(entry);
            StringAssert.Contains(entry, "200");
            StringAssert.Contains(entry, "Keywords : None");
            Assert.AreEqual(DefaultConsoleColorMapper.Critical, consoleOutputInterceptor.OutputForegroundColor);
        }

        [TestMethod]
        public void WhenActivityId()
        {
            var consoleOutputInterceptor = new MockConsoleOutputInterceptor();
            var formatter = new EventTextFormatter(EventTextFormatter.DashSeparator);
            var logger = MockConsoleListenerEventSource.Logger;

            var activityId = Guid.NewGuid();
            var previousActivityId = Guid.Empty;
            EventSource.SetCurrentThreadActivityId(activityId, out previousActivityId);
            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToConsole();
                    listener.EnableEvents(logger, EventLevel.Critical);
                    logger.Critical("This is to log critical in Console");
                });

            EventSource.SetCurrentThreadActivityId(previousActivityId);
                
            var entry = Regex.Split(consoleOutputInterceptor.Ouput, formatter.Header).Where(c => !string.IsNullOrWhiteSpace(c)).SingleOrDefault();
            Assert.IsNotNull(entry);
            StringAssert.Contains(entry, "200");
            StringAssert.Contains(entry, "Keywords : None");
            Assert.AreEqual(DefaultConsoleColorMapper.Critical, consoleOutputInterceptor.OutputForegroundColor);
            StringAssert.Contains(entry, "ActivityId : " + activityId.ToString());
            StringAssert.DoesNotMatch(entry, new Regex("RelatedActivityId"));
        }

        [TestMethod]
        public void WhenActivityIdAndRelatedActivityId()
        {
            var consoleOutputInterceptor = new MockConsoleOutputInterceptor();
            var formatter = new EventTextFormatter(EventTextFormatter.DashSeparator);
            var logger = MockConsoleListenerEventSource.Logger;

            var activityId = Guid.NewGuid();
            var relatedActivityId = Guid.NewGuid();
            var previousActivityId = Guid.Empty;
            EventSource.SetCurrentThreadActivityId(activityId, out previousActivityId);
            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToConsole();
                    listener.EnableEvents(logger, EventLevel.Critical);
                    logger.CriticalWithRelatedActivityId("This is to log critical in Console", relatedActivityId);
                });
            
            EventSource.SetCurrentThreadActivityId(previousActivityId);

            var entry = Regex.Split(consoleOutputInterceptor.Ouput, formatter.Header).Where(c => !string.IsNullOrWhiteSpace(c)).SingleOrDefault();
            Assert.IsNotNull(entry);
            StringAssert.Contains(entry, "800");
            StringAssert.Contains(entry, "Keywords : None");
            Assert.AreEqual(DefaultConsoleColorMapper.Critical, consoleOutputInterceptor.OutputForegroundColor);
            StringAssert.Contains(entry, "ActivityId : " + activityId.ToString());
            StringAssert.Contains(entry, "RelatedActivityId : " + relatedActivityId.ToString());
        }

        [TestMethod]
        public void WhenEventWithTaskName()
        {
            var consoleOutputInterceptor = new MockConsoleOutputInterceptor();
            var formatter = new EventTextFormatter(EventTextFormatter.DashSeparator);
            var logger = MockConsoleListenerEventSource.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToConsole();
                    listener.EnableEvents(logger, EventLevel.LogAlways, Keywords.All);
                    logger.CriticalWithTaskName("Critical with taskname Page");
                });

            var entry = Regex.Split(consoleOutputInterceptor.Ouput, formatter.Header).Where(c => !string.IsNullOrWhiteSpace(c)).SingleOrDefault();
            Assert.IsNotNull(entry);
            Assert.IsTrue(entry.Contains("ProviderId : "));
            Assert.IsTrue(entry.Contains("\r\nEventId : 1500\r\nKeywords : 1\r\nLevel : Critical\r\nMessage : \r\nOpcode : Info\r\nTask : 1\r\nVersion : 0\r\nPayload : [message : Critical with taskname Page] \r\nEventName : PageInfo\r\nTimestamp : "));
        }

        [TestMethod]
        public void WhenEventWithTaskNone()
        {
            var consoleOutputInterceptor = new MockConsoleOutputInterceptor();
            var formatter = new EventTextFormatter(EventTextFormatter.DashSeparator);
            var logger = MockConsoleListenerEventSource.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToConsole();
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    logger.Critical("Critical with taskname or eventname as Critical");
                });

            var entry = Regex.Split(consoleOutputInterceptor.Ouput, formatter.Header).Where(c => !string.IsNullOrWhiteSpace(c)).SingleOrDefault();
            Assert.IsNotNull(entry);
            Assert.IsTrue(entry.Contains("ProviderId : "));
            Assert.IsTrue(entry.Contains("\r\nEventId : 200\r\nKeywords : None\r\nLevel : Critical\r\nMessage : Functional Test\r\nOpcode : Info\r\nTask : 65334\r\nVersion : 0\r\nPayload : [message : Critical with taskname or eventname as Critical] \r\nEventName : CriticalInfo\r\nTimestamp : "));
        }

        [TestMethod]
        public void WhenEventWithMessageInAttribute()
        {
            var consoleOutputInterceptor = new MockConsoleOutputInterceptor();
            var formatter = new EventTextFormatter(EventTextFormatter.DashSeparator);
            var eventformatter = new EventTextFormatter("----", "-----", EventLevel.Informational);
            eventformatter.DateTimeFormat = "dd/MM/yyyy";
            var logger = TestEventSourceNoAttributes.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToConsole(eventformatter);
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    logger.ObjectArrayEvent4(1000, "stringstringarg10", 2000, "stringstringarg20", 3000);
                });

            var entry = Regex.Split(consoleOutputInterceptor.Ouput, formatter.Header).Where(c => !string.IsNullOrWhiteSpace(c)).SingleOrDefault();
            Assert.IsNotNull(entry);
            StringAssert.Contains(entry, "[arg0 : 1000] [arg1 : stringstringarg10] [arg2 : 2000] [arg3 : stringstringarg20] [arg4 : 3000]");
            StringAssert.Contains(entry, "Message : Check if it is logged");
        }

        [TestMethod]
        public void WhenEventWithMessageInAttributeUsingJson()
        {
            var consoleOutputInterceptor = new MockConsoleOutputInterceptor();
            var formatter = new EventTextFormatter(EventTextFormatter.DashSeparator);
            var jsonFormatter = new JsonEventTextFormatter();
            jsonFormatter.DateTimeFormat = "dd/MM/yyyy";
            var logger = TestEventSourceNoAttributes.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToConsole(jsonFormatter);
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    logger.ObjectArrayEvent4(1000, "stringstringarg10", 2000, "stringstringarg20", 3000);
                });

            var entry = Regex.Split(consoleOutputInterceptor.Ouput, formatter.Header).Where(c => !string.IsNullOrWhiteSpace(c)).SingleOrDefault();
            Assert.IsNotNull(entry);
            StringAssert.Contains(entry, "{\"arg0\":1000,\"arg1\":\"stringstringarg10\",\"arg2\":2000,\"arg3\":\"stringstringarg20\",\"arg4\":3000}");
            StringAssert.Contains(entry, "Check if it is logged");
        }

        [TestMethod]
        public void WhenEventWithMessageInAttributeUsingXml()
        {
            var consoleOutputInterceptor = new MockConsoleOutputInterceptor();
            var formatter = new EventTextFormatter(EventTextFormatter.DashSeparator);
            var xmlFormatter = new XmlEventTextFormatter();
            xmlFormatter.DateTimeFormat = "dd/MM/yyyy";
            var logger = TestEventSourceNoAttributes.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToConsole(xmlFormatter);
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    logger.ObjectArrayEvent4(1000, "stringstringarg10", 2000, "stringstringarg20", 3000);
                });

            var entry = Regex.Split(consoleOutputInterceptor.Ouput, formatter.Header).Where(c => !string.IsNullOrWhiteSpace(c)).SingleOrDefault();
            Assert.IsNotNull(entry);
            StringAssert.Contains(entry, "<Data Name=\"arg0\">1000</Data><Data Name=\"arg1\">stringstringarg10</Data><Data Name=\"arg2\">2000</Data><Data Name=\"arg3\">stringstringarg20</Data><Data Name=\"arg4\">3000</Data>");
            StringAssert.Contains(entry, "<Message>Check if it is logged</Message>");
        }

        [TestMethod]
        public void WhenProcessId()
        {
            var consoleOutputInterceptor = new MockConsoleOutputInterceptor();
            var formatter = new EventTextFormatter(EventTextFormatter.DashSeparator);
            var logger = MockConsoleListenerEventSource.Logger;

            int processId = System.Diagnostics.Process.GetCurrentProcess().Id;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToConsole();
                    listener.EnableEvents(logger, EventLevel.Critical);
                    logger.Critical("This is to log critical in Console");
                });

            var entry = Regex.Split(consoleOutputInterceptor.Ouput, formatter.Header).Where(c => !string.IsNullOrWhiteSpace(c)).SingleOrDefault();
            Assert.IsNotNull(entry);
            StringAssert.Contains(entry, "ProcessId : " + processId);
        }

        [TestMethod]
        public void WhenThreadId()
        {
            var consoleOutputInterceptor = new MockConsoleOutputInterceptor();
            var formatter = new EventTextFormatter(EventTextFormatter.DashSeparator);
            var logger = MockConsoleListenerEventSource.Logger;

            int threadId = ThreadHelper.GetCurrentUnManagedThreadId();

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToConsole();
                    listener.EnableEvents(logger, EventLevel.Critical);
                    logger.Critical("This is to log critical in Console");
                });

            var entry = Regex.Split(consoleOutputInterceptor.Ouput, formatter.Header).Where(c => !string.IsNullOrWhiteSpace(c)).SingleOrDefault();
            Assert.IsNotNull(entry);
            StringAssert.Contains(entry, "ThreadId : " + threadId);
        }
    }
}
