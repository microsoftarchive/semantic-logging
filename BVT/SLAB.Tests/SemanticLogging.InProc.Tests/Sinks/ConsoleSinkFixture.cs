// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Formatters;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.InProc.Tests.TestObjects;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.Shared.TestObjects;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.InProc.Tests.Sinks
{
    [TestClass]
    public class ConsoleSinkFixture
    {
        [TestMethod]
        public void DefaultColorMappingForInformationalToConsole()
        {
            var consoleOutputInterceptor = new MockConsoleOutputInterceptor();
            var formatter = new EventTextFormatter(EventTextFormatter.DashSeparator);

            using (var eventListener = new ObservableEventListener()) 
            { 
                eventListener.LogToConsole();
                eventListener.EnableEvents(MockConsoleListenerEventSource.Logger, EventLevel.LogAlways);
                MockConsoleListenerEventSource.Logger.Informational("This is to log information in Console");
            }

            var entry = Regex.Split(consoleOutputInterceptor.Ouput, formatter.Header).Where(c => !string.IsNullOrWhiteSpace(c)).SingleOrDefault();
            Assert.IsNotNull(entry);
            StringAssert.Contains(entry, "100");
            Assert.AreEqual(DefaultConsoleColorMapper.Informational, consoleOutputInterceptor.OutputForegroundColor);
        }

        [TestMethod]
        public void DefaultColorMappingForErrorToConsole()
        {
            var consoleOutputInterceptor = new MockConsoleOutputInterceptor();
            var formatter = new EventTextFormatter(EventTextFormatter.DashSeparator);

            using (var eventListener = new ObservableEventListener())
            {
                eventListener.LogToConsole();
                eventListener.EnableEvents(MockConsoleListenerEventSource.Logger, EventLevel.LogAlways);
                MockConsoleListenerEventSource.Logger.Error("This is to log error in Console");
            }

            var entry = Regex.Split(consoleOutputInterceptor.Ouput, formatter.Header).Where(c => !string.IsNullOrWhiteSpace(c)).SingleOrDefault();
            Assert.IsNotNull(entry);
            StringAssert.Contains(entry, "300");
            Assert.AreEqual(DefaultConsoleColorMapper.Error, consoleOutputInterceptor.OutputForegroundColor);
        }

        [TestMethod]
        public void DefaultColorMappingForCriticalToConsole()
        {
            var consoleOutputInterceptor = new MockConsoleOutputInterceptor();
            var formatter = new EventTextFormatter(EventTextFormatter.DashSeparator);

            using (var eventListener = new ObservableEventListener())
            {
                eventListener.LogToConsole();
                eventListener.EnableEvents(MockConsoleListenerEventSource.Logger, EventLevel.LogAlways);
                MockConsoleListenerEventSource.Logger.Critical("This is to log critical in Console");
            }

            var entry = Regex.Split(consoleOutputInterceptor.Ouput, formatter.Header).Where(c => !string.IsNullOrWhiteSpace(c)).SingleOrDefault();
            Assert.IsNotNull(entry);
            StringAssert.Contains(entry, "200");
            Assert.AreEqual(DefaultConsoleColorMapper.Critical, consoleOutputInterceptor.OutputForegroundColor);
        }

        [TestMethod]
        public void DefaultColorMappingForVerboseToConsole()
        {
            var consoleOutputInterceptor = new MockConsoleOutputInterceptor();
            var formatter = new EventTextFormatter(EventTextFormatter.DashSeparator);

            using (var eventListener = new ObservableEventListener())
            {
                eventListener.LogToConsole();
                eventListener.EnableEvents(MockConsoleListenerEventSource.Logger, EventLevel.LogAlways);
                MockConsoleListenerEventSource.Logger.Verbose("This is to log verbose in Console");
            }

            var entry = Regex.Split(consoleOutputInterceptor.Ouput, formatter.Header).Where(c => !string.IsNullOrWhiteSpace(c)).SingleOrDefault();
            Assert.IsNotNull(entry);
            StringAssert.Contains(entry, "400");
            Assert.AreEqual(DefaultConsoleColorMapper.Verbose, consoleOutputInterceptor.OutputForegroundColor);
        }

        [TestMethod]
        public void OneSourceTwoListenersToConsole()
        {
            var consoleOutputInterceptor = new MockConsoleOutputInterceptor();
            var formatter = new EventTextFormatter(EventTextFormatter.DashSeparator);
            
            using (var eventListener = new ObservableEventListener())
            using (var eventListener2 = new ObservableEventListener())
            {
                eventListener.LogToConsole(new EventTextFormatter(), null);
                eventListener2.LogToConsole();
                string errorMessage = string.Concat("Error ", Guid.NewGuid());
                string infoMessage = string.Concat("Message", Guid.NewGuid());
                eventListener2.EnableEvents(MockConsoleListenerEventSource.Logger, EventLevel.Error);
                eventListener.EnableEvents(MockConsoleListenerEventSource.Logger, EventLevel.Informational);
                MockConsoleListenerEventSource.Logger.Informational(infoMessage);
                MockConsoleListenerEventSource.Logger.Error(errorMessage);

                var entry = Regex.Split(consoleOutputInterceptor.Ouput, formatter.Header).Where(c => !string.IsNullOrWhiteSpace(c)).SingleOrDefault();
                Assert.IsNotNull(entry);
                StringAssert.Contains(entry, "100");
                StringAssert.Contains(entry, "300");
            }
        }

        [TestMethod]
        public void OneListenerTwoSourcesToConsole()
        {
            var consoleOutputInterceptor = new MockConsoleOutputInterceptor();
            var formatter = new EventTextFormatter(EventTextFormatter.DashSeparator);
            var logger = MockConsoleListenerEventSource.Logger;
            var logger2 = MockConsoleListenerEventSource2.Logger;

            using (var eventListener = new ObservableEventListener())
            {
                eventListener.LogToConsole();
                string message = string.Concat("Message ", Guid.NewGuid());
                string errorMessage = string.Concat("Error ", Guid.NewGuid());
                eventListener.EnableEvents(logger, EventLevel.LogAlways);
                eventListener.EnableEvents(logger2, EventLevel.LogAlways);
                logger.Informational(message);
                logger2.Informational(message);
            }

            var entry = Regex.Split(consoleOutputInterceptor.Ouput, formatter.Header).Where(c => !string.IsNullOrWhiteSpace(c)).SingleOrDefault();
            Assert.IsNotNull(entry);
            StringAssert.Contains(entry, "100");
        }

        [TestMethod]
        public void MultipleEventsToConsole()
        {
            var consoleOutputInterceptor = new MockConsoleOutputInterceptor();
            var formatter = new EventTextFormatter(EventTextFormatter.DashSeparator);
            var logger = MockConsoleListenerEventSource.Logger;

            using (var eventListener = new ObservableEventListener())
            {   
                eventListener.EnableEvents(logger, EventLevel.LogAlways);
                eventListener.LogToConsole();
                for (int n = 0; n < 300; n++)
                {
                    logger.Informational("Some message to console " + n);
                }
            }

            var output = Regex.Split(consoleOutputInterceptor.Ouput, formatter.Header).Where(c => !string.IsNullOrWhiteSpace(c));
            Assert.IsNotNull(output);
            StringAssert.Contains(output.First(), "Some message to console 0");
            StringAssert.Contains(output.First(), "Some message to console 299");
        }

        [TestMethod]
        public void OneSourceTwoListenersConcurrentlyToConsole()
        {
            var consoleOutputInterceptor = new MockConsoleOutputInterceptor();
            var formatter = new EventTextFormatter(EventTextFormatter.DashSeparator);
            var logger = MockConsoleListenerEventSource.Logger;
            
            using (var eventListener = new ObservableEventListener())
            using (var eventListener2 = new ObservableEventListener())
            {
                eventListener.LogToConsole();
                eventListener2.LogToConsole();
                int maxLoggedEntries = 9;
                string criticalMessage = string.Concat("CriticalMessage");
                string infoMessage = string.Concat("InfoMessage");
                eventListener.EnableEvents(logger, EventLevel.Critical);
                eventListener2.EnableEvents(logger, EventLevel.Critical);
                Parallel.Invoke(Enumerable.Range(0, maxLoggedEntries).Select(i =>
                new Action(() =>
                {
                    logger.Critical(i + criticalMessage);
                })).ToArray());
            }

            var entry = Regex.Split(consoleOutputInterceptor.Ouput, formatter.Header).Where(c => !string.IsNullOrWhiteSpace(c)).SingleOrDefault();
            Assert.IsNotNull(entry);
        }

        [TestMethod]
        public void PaylodIsParsedWithDifferentTypesToConsole()
        {
            var consoleOutputInterceptor = new MockConsoleOutputInterceptor();
            var formatter = new EventTextFormatter(EventTextFormatter.DashSeparator);
            var logger = MockEventSourceNoTask.Logger;

            using (var eventListener = new ObservableEventListener())
            {
                eventListener.LogToConsole();
                eventListener.EnableEvents(logger, EventLevel.LogAlways);
                logger.DifferentTypes("testString", 500000);
            }

            var entry = Regex.Split(consoleOutputInterceptor.Ouput, formatter.Header).Where(c => !string.IsNullOrWhiteSpace(c)).SingleOrDefault();
            Assert.IsNotNull(entry);
            StringAssert.Contains(entry, @"strArg : testString");
            StringAssert.Contains(entry, @"longArg : 500000");
        }

        [TestMethod]
        public void PaylodIsParsedWithDifferentTypesAndNullToConsole()
        {
            var consoleOutputInterceptor = new MockConsoleOutputInterceptor();
            var formatter = new EventTextFormatter(EventTextFormatter.DashSeparator);
            var logger = MockEventSourceNoTask.Logger;

            using (var eventListener = new ObservableEventListener())
            {
                eventListener.LogToConsole();
                eventListener.EnableEvents(logger, EventLevel.LogAlways);
                logger.DifferentTypes(null, 500000);
            }

            var entry = Regex.Split(consoleOutputInterceptor.Ouput, formatter.Header).Where(c => !string.IsNullOrWhiteSpace(c)).SingleOrDefault();
            Assert.IsNotNull(entry);
            Assert.IsTrue(entry.Count() > 0);
            StringAssert.Contains(entry, @"strArg : ");
            StringAssert.Contains(entry, @"longArg : 500000");
        }

        [TestMethod]
        public void RawMessageAndFormattedMessageToConsole()
        {
            var consoleOutputInterceptor = new MockConsoleOutputInterceptor();
            var formatter = new EventTextFormatter(EventTextFormatter.DashSeparator);
            var logger = MockEventSourceNoTask.Logger;

            using (var eventListener = new ObservableEventListener())
            {
                eventListener.LogToConsole();
                eventListener.EnableEvents(logger, EventLevel.LogAlways);
                logger.Informational("testing");
            }

            var entry = Regex.Split(consoleOutputInterceptor.Ouput, formatter.Header).Where(c => !string.IsNullOrWhiteSpace(c)).SingleOrDefault();
            Assert.IsNotNull(entry);
            Assert.IsTrue(entry.Count() > 0);
            StringAssert.Contains(entry, "message param");
        }

        [TestMethod]
        public void HighEventIdsToConsole()
        {
            var consoleOutputInterceptor = new MockConsoleOutputInterceptor();
            var formatter = new EventTextFormatter(EventTextFormatter.DashSeparator);
            var logger = MockHighEventIdEventSource.HigheventIdLogger;

            using (var eventListener = new ObservableEventListener())
            {
                eventListener.LogToConsole();
                eventListener.EnableEvents(logger, EventLevel.Warning);
                logger.Warning();
            }

            var entry = Regex.Split(consoleOutputInterceptor.Ouput, formatter.Header).Where(c => !string.IsNullOrWhiteSpace(c)).SingleOrDefault();
            Assert.IsNotNull(entry);
        }

        [TestMethod]
        public void LowEventIdsToConsole()
        {
            var consoleOutputInterceptor = new MockConsoleOutputInterceptor();
            var formatter = new EventTextFormatter(EventTextFormatter.DashSeparator);
            var logger = MockNegativeEventIdEventSource.LoweventIdLogger;

            try
            {
                using (var eventListener = new ObservableEventListener())
                {
                    eventListener.EnableEvents(logger, EventLevel.Warning);
                    Assert.IsFalse(true, "Should throw when calling EnableEvents.");
                }
            }
            catch (ArgumentException ex)
            {
                Assert.AreEqual("Event IDs must be positive integers.", ex.Message);
            }
        }

        [TestMethod]
        public void ErrorEventRaisedWhenErrorInLoggingToConsole()
        {
            InMemoryEventListener collectErrorsListener;
            var mockConsole = new MockConsoleOutputInterceptor();

            using (var eventListener = new ObservableEventListener())
            {
                eventListener.LogToConsole(new MockFormatter(true));
                eventListener.EnableEvents(TestEventSource.Logger, EventLevel.LogAlways);
                collectErrorsListener = new InMemoryEventListener(true);
                collectErrorsListener.EnableEvents(SemanticLoggingEventSource.Log, EventLevel.Error, SemanticLoggingEventSource.Keywords.Sink);
                TestEventSource.Logger.EventWithPayload("payload1", 100);
            }

            StringAssert.Contains(collectErrorsListener.ToString(), "System.InvalidOperationException: Operation is not valid due to the current state of the object.");
            Assert.AreEqual(string.Empty, mockConsole.Ouput);
        }

        [TestMethod]
        public void SingleLineTextFormatterToConsole()
        {
            var consoleOutputInterceptor = new MockConsoleOutputInterceptor();
            var formatter = new EventTextFormatter(EventTextFormatter.DashSeparator);

            using (var eventListener = new ObservableEventListener())
            {
                eventListener.LogToConsole(formatter);
                eventListener.EnableEvents(MockConsoleListenerEventSource.Logger, EventLevel.Error);
                MockConsoleListenerEventSource.Logger.Critical("This is to log critical in Console");
                MockConsoleListenerEventSource.Logger.Verbose("This is should not be logged in Console");
            }

            var entry = Regex.Split(consoleOutputInterceptor.Ouput, formatter.Header).Where(c => !string.IsNullOrWhiteSpace(c)).SingleOrDefault();
            Assert.IsNotNull(entry);
            Assert.IsFalse(entry.Contains("This is should not be logged in Console"));
            Assert.IsTrue(entry.Contains("This is to log critical in Console"));
            Assert.IsTrue(entry.Contains("\r\nProviderId : "));
            Assert.IsTrue(entry.Contains("\r\nEventId : 200\r\nKeywords : None\r\nLevel : Critical\r\nMessage : Functional Test\r\nOpcode : Info\r\nTask : 65334\r\nVersion : 0\r\nPayload : [message : This is to log critical in Console] \r\nEventName : CriticalInfo\r\nTimestamp :"));
        }

        [TestMethod]
        public void EnablingAllKeywordsToConsole()
        {
            var consoleOutputInterceptor = new MockConsoleOutputInterceptor();
            var formatter = new EventTextFormatter("----", "====", EventLevel.LogAlways);

            using (var eventListener = new ObservableEventListener())
            {
                eventListener.EnableEvents(MockConsoleListenerEventSource.Logger, EventLevel.LogAlways, Keywords.All);
                eventListener.LogToConsole(formatter);
                MockConsoleListenerEventSource.Logger.InfoWithKeywordDiagnostic("Info with keyword Diagnostic");
            }

            var entry = Regex.Split(consoleOutputInterceptor.Ouput, formatter.Header).Where(c => !string.IsNullOrWhiteSpace(c)).SingleOrDefault();
            Assert.IsNotNull(entry);
            Assert.IsTrue(entry.Contains("ProviderId : "));
            Assert.IsTrue(entry.Contains("\r\nEventId : 1020\r\nKeywords : 4\r\nLevel : Informational\r\nMessage : \r\nOpcode : Info\r\nTask : 1\r\nVersion : 0\r\nPayload : [message : Info with keyword Diagnostic] \r\nEventName : PageInfo\r\nTimestamp : "));
        }

        [TestMethod]
        public void SpecificKeywordToConsole()
        {
            var consoleOutputInterceptor = new MockConsoleOutputInterceptor();
            var formatter = new EventTextFormatter(EventTextFormatter.DashSeparator);

            using (var eventListener = new ObservableEventListener())
            {
                eventListener.LogToConsole();
                eventListener.EnableEvents(MockConsoleListenerEventSource.Logger, EventLevel.LogAlways);
                MockConsoleListenerEventSource.Logger.InfoWithKeywordDiagnostic("Info with keyword Diagnostic");
            }

            var entry = Regex.Split(consoleOutputInterceptor.Ouput, formatter.Header).Where(c => !string.IsNullOrWhiteSpace(c)).SingleOrDefault();
            Assert.IsNull(entry);
        }

        [TestMethod]
        public void CriticalVerbosityForFormatterToConsole()
        {
            var consoleOutputInterceptor = new MockConsoleOutputInterceptor();
            var formatter = new EventTextFormatter(EventTextFormatter.DashSeparator);

            using (var eventListener = new ObservableEventListener())
            {
                eventListener.LogToConsole(formatter);
                formatter.VerbosityThreshold = EventLevel.Critical;
                eventListener.EnableEvents(MockConsoleListenerEventSource.Logger, EventLevel.Critical);
                MockConsoleListenerEventSource.Logger.Critical("This is to log critical in Console");
            }

            var entry = Regex.Split(consoleOutputInterceptor.Ouput, formatter.Header).Where(c => !string.IsNullOrWhiteSpace(c)).SingleOrDefault();
            Assert.IsNotNull(entry);
            StringAssert.Contains(entry, "200");
            StringAssert.Contains(entry, "Keywords : None");
            Assert.AreEqual(DefaultConsoleColorMapper.Critical, consoleOutputInterceptor.OutputForegroundColor);
        }

        [TestMethod]
        public void DefaultFormatterToConsole()
        {
            var consoleOutputInterceptor = new MockConsoleOutputInterceptor();
            var formatter = new EventTextFormatter(EventTextFormatter.DashSeparator);

            using (var eventListener = new ObservableEventListener())
            {
                eventListener.LogToConsole();
                eventListener.EnableEvents(MockConsoleListenerEventSource.Logger, EventLevel.Critical);
                MockConsoleListenerEventSource.Logger.Critical("This is to log critical in Console");
            }

            var entry = Regex.Split(consoleOutputInterceptor.Ouput, formatter.Header).Where(c => !string.IsNullOrWhiteSpace(c)).SingleOrDefault();
            Assert.IsNotNull(entry);
            StringAssert.Contains(entry, "200");
            StringAssert.Contains(entry, "Keywords : None");
            Assert.AreEqual(DefaultConsoleColorMapper.Critical, consoleOutputInterceptor.OutputForegroundColor);
        }

        [TestMethod]
        public void EventWithTaskNameToConsole()
        {
            var consoleOutputInterceptor = new MockConsoleOutputInterceptor();
            var formatter = new EventTextFormatter(EventTextFormatter.DashSeparator);

            using (var eventListener = new ObservableEventListener())
            {
                eventListener.LogToConsole();
                eventListener.EnableEvents(MockConsoleListenerEventSource.Logger, EventLevel.LogAlways, Keywords.All);
                MockConsoleListenerEventSource.Logger.CriticalWithTaskName("Critical with taskname Page");
            }

            var entry = Regex.Split(consoleOutputInterceptor.Ouput, formatter.Header).Where(c => !string.IsNullOrWhiteSpace(c)).SingleOrDefault();
            Assert.IsNotNull(entry);
            Assert.IsTrue(entry.Contains("ProviderId : "));
            Assert.IsTrue(entry.Contains("\r\nEventId : 1500\r\nKeywords : 1\r\nLevel : Critical\r\nMessage : \r\nOpcode : Info\r\nTask : 1\r\nVersion : 0\r\nPayload : [message : Critical with taskname Page] \r\nEventName : PageInfo\r\nTimestamp : "));
        }

        [TestMethod]
        public void EventWithTaskNoneToConsole()
        {
            var consoleOutputInterceptor = new MockConsoleOutputInterceptor();
            var formatter = new EventTextFormatter(EventTextFormatter.DashSeparator);

            using (var eventListener = new ObservableEventListener())
            {
                eventListener.LogToConsole();
                eventListener.EnableEvents(MockConsoleListenerEventSource.Logger, EventLevel.LogAlways);
                MockConsoleListenerEventSource.Logger.Critical("Critical with taskname or eventname as Critical");
            }

            var entry = Regex.Split(consoleOutputInterceptor.Ouput, formatter.Header).Where(c => !string.IsNullOrWhiteSpace(c)).SingleOrDefault();
            Assert.IsNotNull(entry);
            Assert.IsTrue(entry.Contains("ProviderId : "));
            Assert.IsTrue(entry.Contains("\r\nEventId : 200\r\nKeywords : None\r\nLevel : Critical\r\nMessage : Functional Test\r\nOpcode : Info\r\nTask : 65334\r\nVersion : 0\r\nPayload : [message : Critical with taskname or eventname as Critical] \r\nEventName : CriticalInfo\r\nTimestamp : "));
        }

        [TestMethod]
        public void EventWithMessageInAttributeToConsole()
        {
            var consoleOutputInterceptor = new MockConsoleOutputInterceptor();
            var formatter = new EventTextFormatter(EventTextFormatter.DashSeparator);
            var eventformatter = new EventTextFormatter("----", "-----", EventLevel.Informational);
            eventformatter.DateTimeFormat = "dd/MM/yyyy";
            var logger = TestEventSourceNoAttributes.Logger;

            using (var eventListener = new ObservableEventListener())
            {
                eventListener.LogToConsole(eventformatter);
                eventListener.EnableEvents(logger, EventLevel.LogAlways);
                logger.ObjectArrayEvent4(1000, "stringstringarg10", 2000, "stringstringarg20", 3000);
            }

            var entry = Regex.Split(consoleOutputInterceptor.Ouput, formatter.Header).Where(c => !string.IsNullOrWhiteSpace(c)).SingleOrDefault();
            Assert.IsNotNull(entry);
            StringAssert.Contains(entry, "[arg0 : 1000] [arg1 : stringstringarg10] [arg2 : 2000] [arg3 : stringstringarg20] [arg4 : 3000]");
            StringAssert.Contains(entry, "Message : Check if it is logged");
        }

        [TestMethod]
        public void EventWithMessageInAttributeUsingJsonToConsole()
        {
            var consoleOutputInterceptor = new MockConsoleOutputInterceptor();
            var formatter = new EventTextFormatter(EventTextFormatter.DashSeparator);
            var jsonFormatter = new JsonEventTextFormatter();
            jsonFormatter.DateTimeFormat = "dd/MM/yyyy";
            var logger = TestEventSourceNoAttributes.Logger;

            using (var eventListener = new ObservableEventListener())
            {
                eventListener.LogToConsole(jsonFormatter);
                eventListener.EnableEvents(logger, EventLevel.LogAlways);
                logger.ObjectArrayEvent4(1000, "stringstringarg10", 2000, "stringstringarg20", 3000);
            }

            var entry = Regex.Split(consoleOutputInterceptor.Ouput, formatter.Header).Where(c => !string.IsNullOrWhiteSpace(c)).SingleOrDefault();
            Assert.IsNotNull(entry);
            StringAssert.Contains(entry, "{\"arg0\":1000,\"arg1\":\"stringstringarg10\",\"arg2\":2000,\"arg3\":\"stringstringarg20\",\"arg4\":3000}");
            StringAssert.Contains(entry, "Check if it is logged");
        }

        [TestMethod]
        public void EventWithMessageInAttributeUsingXmlToConsole()
        {
            var consoleOutputInterceptor = new MockConsoleOutputInterceptor();
            var formatter = new EventTextFormatter(EventTextFormatter.DashSeparator);
            var xmlFormatter = new XmlEventTextFormatter();
            xmlFormatter.DateTimeFormat = "dd/MM/yyyy";
            var logger = TestEventSourceNoAttributes.Logger;

            using (var eventListener = new ObservableEventListener())
            {
                eventListener.LogToConsole(xmlFormatter);
                eventListener.EnableEvents(logger, EventLevel.LogAlways);
                logger.ObjectArrayEvent4(1000, "stringstringarg10", 2000, "stringstringarg20", 3000);
            }

            var entry = Regex.Split(consoleOutputInterceptor.Ouput, formatter.Header).Where(c => !string.IsNullOrWhiteSpace(c)).SingleOrDefault();
            Assert.IsNotNull(entry);
            StringAssert.Contains(entry, "<Data Name=\"arg0\">1000</Data><Data Name=\"arg1\">stringstringarg10</Data><Data Name=\"arg2\">2000</Data><Data Name=\"arg3\">stringstringarg20</Data><Data Name=\"arg4\">3000</Data>");
            StringAssert.Contains(entry, "<Message>Check if it is logged</Message>");
        }
    }
}
