// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.InProc.Tests.TestObjects;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.InProc.Tests.TestScenarios;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.Shared.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.InProc.Tests.Sinks
{
    [TestClass]
    public class WindowsAzureTableSinkFixture
    {
        private string tableName;

        [TestInitialize]
        public void Initialize()
        {
            this.tableName = string.Empty;
        }

        [TestCleanup]
        public void Teardown()
        {
            if (!string.IsNullOrWhiteSpace(this.tableName))
            {
                AzureTableHelper.DeleteTable(System.Configuration.ConfigurationManager.AppSettings["StorageConnectionString"], this.tableName);
            }
        }

        [TestMethod]
        public void WhenEventsWithDifferentLevels()
        {
            this.tableName = "WhenEventsWithDifferentLevels";
            var connectionString = System.Configuration.ConfigurationManager.AppSettings["StorageConnectionString"];
            AzureTableHelper.DeleteTable(connectionString, this.tableName);
            var logger = TestEventSource.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToWindowsAzureTable("mytestinstance", connectionString, this.tableName, bufferingInterval: TimeSpan.FromSeconds(1));
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    logger.Critical("This is a critical message");
                    logger.Error("This is an error message");
                    logger.Informational("This is informational");
                });

            var events = AzureTableHelper.PollForEvents(connectionString, this.tableName, 3);
            Assert.AreEqual(3, events.Count());
            Assert.AreEqual(TestEventSource.InformationalEventId, events.ElementAt(0).EventId);
            Assert.AreEqual(TestEventSource.ErrorEventId, events.ElementAt(1).EventId);
            Assert.AreEqual(TestEventSource.CriticalEventId, events.ElementAt(2).EventId);
        }

        [TestMethod]
        public void WhenLoggingMultipleMessages()
        {
            this.tableName = "WhenLoggingMultipleMessages";
            var connectionString = System.Configuration.ConfigurationManager.AppSettings["StorageConnectionString"];
            AzureTableHelper.DeleteTable(connectionString, this.tableName);
            var logger = TestEventSource.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToWindowsAzureTable("mytestinstance", connectionString, this.tableName);
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    for (int n = 0; n < 300; n++)
                    {
                        logger.Informational("logging multiple messages " + n.ToString());
                    }
                });

            var events = AzureTableHelper.PollForEvents(connectionString, this.tableName, 300);
            Assert.AreEqual(300, events.Count());
        }

        [TestMethod]
        public void WhenNoPayload()
        {
            this.tableName = "WhenNoPayload";
            var connectionString = System.Configuration.ConfigurationManager.AppSettings["StorageConnectionString"];
            AzureTableHelper.DeleteTable(connectionString, this.tableName);
            var logger = TestEventSource.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToWindowsAzureTable("mytestinstance", connectionString, this.tableName, bufferingInterval: TimeSpan.FromSeconds(1));
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    logger.EventWithoutPayloadNorMessage();
                });

            var events = AzureTableHelper.PollForEvents(connectionString, this.tableName, 1);
            Assert.AreEqual(1, events.Count());
            Assert.AreEqual(TestEventSource.EventWithoutPayloadNorMessageId, events.ElementAt(0).EventId);
        }

        [TestMethod]
        public void WhenEventHasAllValuesForAttribute()
        {
            this.tableName = "WhenEventHasAllValuesForAttribute";
            var connectionString = System.Configuration.ConfigurationManager.AppSettings["StorageConnectionString"];
            AzureTableHelper.DeleteTable(connectionString, this.tableName);
            var logger = TestEventSource.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToWindowsAzureTable("mytestinstance", connectionString, this.tableName, bufferingInterval: TimeSpan.FromSeconds(1));
                    listener.EnableEvents(logger, EventLevel.LogAlways, Keywords.All);
                    logger.AllParametersWithCustomValues();
                });

            var events = AzureTableHelper.PollForEvents(connectionString, this.tableName, 1);
            Assert.AreEqual(1, events.Count());
            Assert.AreEqual(10001, events.ElementAt(0).EventId);
        }

        [TestMethod]
        public void WhenSourceIsEnabledAndDisabled()
        {
            this.tableName = "WhenSourceIsEnabledAndDisabled";
            var connectionString = System.Configuration.ConfigurationManager.AppSettings["StorageConnectionString"];
            AzureTableHelper.DeleteTable(connectionString, this.tableName);
            var logger = TestEventSource.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToWindowsAzureTable("mytestinstance", connectionString, this.tableName, bufferingInterval: TimeSpan.FromSeconds(1));
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    logger.Critical("This is a critical message");
                    var events = AzureTableHelper.PollForEvents(connectionString, this.tableName, 1);
                    Assert.AreEqual(1, events.Count());

                    listener.DisableEvents(logger);
                    logger.Critical("This is a critical message");
                });

            var eventsCount = AzureTableHelper.GetEventsCount(connectionString, this.tableName);
            Assert.AreEqual(1, eventsCount);
        }

        [TestMethod]
        public void WhenEventHasMultiplePayloads()
        {
            this.tableName = "WhenEventHasMultiplePayloads";
            var connectionString = System.Configuration.ConfigurationManager.AppSettings["StorageConnectionString"];
            AzureTableHelper.DeleteTable(connectionString, this.tableName);
            var logger = TestEventSource.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToWindowsAzureTable("mytestinstance", connectionString, this.tableName, bufferingInterval: TimeSpan.FromSeconds(20));
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    logger.EventWithMultiplePayloads("TestPayload 1", "TestPayload 2", "TestPayload 3");
                });

            var events = AzureTableHelper.PollForEvents(connectionString, this.tableName, 1);
            Assert.AreEqual(1, events.Count());
            StringAssert.Contains(events.First().Payload, @"""payload1"": ""TestPayload 1""");
            StringAssert.Contains(events.First().Payload, @"""payload2"": ""TestPayload 2""");
            StringAssert.Contains(events.First().Payload, @"""payload3"": ""TestPayload 3""");
        }

        [TestMethod]
        public void WhenDefaultTableNameIsUsed()
        {
            var connectionString = System.Configuration.ConfigurationManager.AppSettings["StorageConnectionString"];
            AzureTableHelper.DeleteTable(connectionString, WindowsAzureTableLog.DefaultTableName);
            var logger = TestEventSource.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToWindowsAzureTable("mytestinstance", connectionString, bufferingInterval: TimeSpan.FromSeconds(1));
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    logger.Error("This is an error message");
                });

            var events = AzureTableHelper.PollForEvents(connectionString, WindowsAzureTableLog.DefaultTableName, 1);
            Assert.AreEqual(1, events.Count());
        }

        [TestMethod]
        public void WhenTableNameIsNull()
        {
            var ex = ExceptionAssertHelper.Throws<ArgumentNullException>(() =>
            {
                this.tableName = null;
                var connectionString = System.Configuration.ConfigurationManager.AppSettings["StorageConnectionString"];

                using (var listener = new ObservableEventListener())
                {
                    listener.LogToWindowsAzureTable("mytestinstance", connectionString, this.tableName, bufferingInterval: TimeSpan.FromSeconds(1));
                }
            });

            StringAssert.Contains(ex.Message, "Value cannot be null");
            StringAssert.Contains(ex.Message, "Parameter name: tableAddress");
        }

        [TestMethod]
        public void WhenTableNameIsEmpty()
        {
            var ex = ExceptionAssertHelper.Throws<ArgumentException>(() =>
            {
                this.tableName = string.Empty;
                var connectionString = System.Configuration.ConfigurationManager.AppSettings["StorageConnectionString"];

                using (var listener = new ObservableEventListener())
                {
                    listener.LogToWindowsAzureTable("mytestinstance", connectionString, this.tableName, bufferingInterval: TimeSpan.FromSeconds(1));
                }
            });

            StringAssert.Contains(ex.Message, "Argument is empty");
            StringAssert.Contains(ex.Message, "Parameter name: tableAddress");
        }

        [TestMethod]
        public void WhenTableNameIsInvalid()
        {
            var ex = ExceptionAssertHelper.Throws<ArgumentException>(() =>
            {
                this.tableName = "$$$$";
                var connectionString = System.Configuration.ConfigurationManager.AppSettings["StorageConnectionString"];

                using (var listener = new ObservableEventListener())
                {
                    listener.LogToWindowsAzureTable("mytestinstance", connectionString, this.tableName, bufferingInterval: TimeSpan.FromSeconds(1));
                }
            });

            StringAssert.Contains(ex.Message, "Table names may contain only alphanumeric characters, cannot begin with a numeric character and must be from 3 to 63 characters long.");
            StringAssert.Contains(ex.Message, "Parameter name: tableAddress");
        }

        [TestMethod]
        public void WhenConnectionStringIsEmpty()
        {
            var ex = ExceptionAssertHelper.Throws<ArgumentException>(() =>
            {
                using (var listener = new ObservableEventListener())
                {
                    listener.LogToWindowsAzureTable("mytestinstance", string.Empty);
                }
            });

            StringAssert.Contains(ex.Message, "Argument is empty");
            StringAssert.Contains(ex.Message, "Parameter name: connectionString");
        }

        [TestMethod]
        public void WhenConnectionStringIsNull()
        {
            var ex = ExceptionAssertHelper.Throws<ArgumentNullException>(() =>
            {
                using (var listener = new ObservableEventListener())
                {
                    listener.LogToWindowsAzureTable("mytestinstance", null);
                }
            });

            StringAssert.Contains(ex.Message, "Value cannot be null");
            StringAssert.Contains(ex.Message, "Parameter name: connectionString");
        }

        [TestMethod]
        public void WhenInstanceIsEmpty()
        {
            var ex = ExceptionAssertHelper.Throws<ArgumentException>(() =>
            {
                var connectionString = System.Configuration.ConfigurationManager.AppSettings["StorageConnectionString"];

                using (var listener = new ObservableEventListener())
                {
                    listener.LogToWindowsAzureTable(string.Empty, connectionString);
                }
            });

            StringAssert.Contains(ex.Message, "Argument is empty");
            StringAssert.Contains(ex.Message, "Parameter name: instanceName");
        }

        [TestMethod]
        public void WhenInstanceIsNull()
        {
            var ex = ExceptionAssertHelper.Throws<ArgumentNullException>(() =>
            {
                var connectionString = System.Configuration.ConfigurationManager.AppSettings["StorageConnectionString"];

                using (var listener = new ObservableEventListener())
                {
                    listener.LogToWindowsAzureTable(null, connectionString);
                }
            });

            StringAssert.Contains(ex.Message, "Value cannot be null");
            StringAssert.Contains(ex.Message, "Parameter name: instanceName");
        }

        [TestMethod]
        public void WhenBatchSizeIsExceeded()
        {
            this.tableName = "WhenBatchSizeIsExceeded";
            var connectionString = System.Configuration.ConfigurationManager.AppSettings["StorageConnectionString"];
            AzureTableHelper.DeleteTable(connectionString, this.tableName);
            var logger = TestEventSource.Logger;
            IEnumerable<WindowsAzureTableEventEntry> events = null;

            TestScenario.With2Listeners(
                logger,
                (listener1, listener2) =>
                {
                    listener1.LogToWindowsAzureTable("mytestinstance1", connectionString, this.tableName, bufferingInterval: TimeSpan.FromSeconds(20));
                    listener2.LogToWindowsAzureTable("mytestinstance2", connectionString, this.tableName, bufferingInterval: TimeSpan.FromSeconds(20));
                    listener1.EnableEvents(logger, EventLevel.LogAlways);
                    listener2.EnableEvents(logger, EventLevel.LogAlways);

                    // 100 events or more will be flushed by count before the buffering interval elapses
                    var logTaskList = new List<Task>();
                    for (int i = 0; i < 120; i++)
                    {
                        var messageNumber = i;
                        logTaskList.Add(Task.Run(() => logger.Critical(messageNumber + "Critical message")));
                    }

                    Task.WaitAll(logTaskList.ToArray(), TimeSpan.FromSeconds(10));

                    // Wait less than the buffering interval for the events to be written and assert
                    // Only the first batch of 100 is written for each listener
                    events = AzureTableHelper.PollForEvents(connectionString, this.tableName, 200, waitFor: TimeSpan.FromSeconds(10));
                    Assert.AreEqual(200, events.Count());
                    Assert.AreEqual(100, events.Where(e => e.InstanceName == "mytestinstance1").Count());
                    Assert.AreEqual(100, events.Where(e => e.InstanceName == "mytestinstance2").Count());
                });

            // The rest of the events are written during the Dispose flush
            events = AzureTableHelper.PollForEvents(connectionString, this.tableName, 240, waitFor: TimeSpan.FromSeconds(2));
            Assert.AreEqual(240, events.Count());
            Assert.AreEqual(120, events.Where(e => e.InstanceName == "mytestinstance1").Count());
            Assert.AreEqual(120, events.Where(e => e.InstanceName == "mytestinstance2").Count());
        }

        [TestMethod]
        public void WhenBufferingWithMinimumNonDefaultInterval()
        {
            this.tableName = "WhenBufferingWithMinimalNonDefaultInterval";
            var connectionString = System.Configuration.ConfigurationManager.AppSettings["StorageConnectionString"];
            AzureTableHelper.DeleteTable(connectionString, this.tableName);
            var logger = TestEventSource.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    // Minimum buffering interval is 500 ms
                    var minimumBufferingInterval = TimeSpan.FromMilliseconds(500);
                    listener.LogToWindowsAzureTable("mytestinstance1", connectionString, this.tableName, bufferingInterval: minimumBufferingInterval);
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    var logTaskList = new List<Task>();
                    for (int i = 0; i < 10; i++)
                    {
                        logger.Critical("Critical message");
                    }

                    // Wait for the events to be written and assert
                    Task.Delay(TimeSpan.FromSeconds(3)).Wait();
                    var eventsCount = AzureTableHelper.GetEventsCount(connectionString, this.tableName);
                    Assert.AreEqual(10, eventsCount);
                });

            // No more events should be written during the Dispose flush
            var eventsCountFinal = AzureTableHelper.GetEventsCount(connectionString, this.tableName);
            Assert.AreEqual(10, eventsCountFinal);
        }

        [TestMethod]
        public void WhenUsingNonDefaultBufferInterval()
        {
            this.tableName = "WhenUsingNonDefaultBufferInterval";
            var connectionString = System.Configuration.ConfigurationManager.AppSettings["StorageConnectionString"];
            AzureTableHelper.DeleteTable(connectionString, this.tableName);
            var logger = TestEventSource.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    var bufferingInterval = TimeSpan.FromSeconds(5);
                    listener.LogToWindowsAzureTable("mytestinstance", connectionString, this.tableName, bufferingInterval: bufferingInterval);
                    listener.EnableEvents(logger, EventLevel.LogAlways);

                    // Pre-condition: Wait for the events to be written and assert
                    Task.Delay(TimeSpan.FromSeconds(2)).Wait();
                    Assert.AreEqual(0, AzureTableHelper.GetEventsCount(connectionString, this.tableName));

                    for (int i = 0; i < 10; i++)
                    {
                        logger.Critical("Critical Message");
                    }

                    // Event must not be written before the interval has elapsed
                    Task.Delay(TimeSpan.FromSeconds(2)).Wait();
                    Assert.AreEqual(0, AzureTableHelper.GetEventsCount(connectionString, this.tableName));

                    // Wait for the buffer to flush at end of interval
                    Task.Delay(bufferingInterval).Wait();

                    // 1st interval: Wait for the events to be written and assert
                    Task.Delay(TimeSpan.FromSeconds(2)).Wait();
                    Assert.AreEqual(10, AzureTableHelper.GetEventsCount(connectionString, this.tableName));
                });
        }

        [TestMethod]
        public void WhenInternalBufferCountIsExceededAndIntervalExceeded()
        {
            this.tableName = "WhenInternalBufferCountIsExceededAndIntervalExceeded";
            var connectionString = System.Configuration.ConfigurationManager.AppSettings["StorageConnectionString"];
            AzureTableHelper.DeleteTable(connectionString, this.tableName);
            var logger = TestEventSource.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    var bufferingInterval = TimeSpan.FromSeconds(5);
                    listener.LogToWindowsAzureTable("mytestinstance", connectionString, this.tableName, bufferingInterval: bufferingInterval);
                    listener.EnableEvents(logger, EventLevel.Informational);

                    // When reachiing 100 events buffer will be flushed
                    for (int i = 0; i < 110; i++)
                    {
                        logger.Informational("Message1");
                    }

                    // Wait for buffer interval to elapse
                    Task.Delay(bufferingInterval).Wait();
                    var events = AzureTableHelper.GetEventsCount(connectionString, this.tableName);
                    Assert.AreEqual(100, events);
                });

            // Last events should be written during the Dispose flush
            var eventsCountFinal = AzureTableHelper.GetEventsCount(connectionString, this.tableName);
            Assert.AreEqual(110, eventsCountFinal);
        }

        [TestMethod]
        public void WhenBufferIntervalExceedsAndLessEntriesThanBufferCount()
        {
            this.tableName = "WhenBufferIntervalExceedsAndLessEntriesThanBufferCount";
            var connectionString = System.Configuration.ConfigurationManager.AppSettings["StorageConnectionString"];
            AzureTableHelper.DeleteTable(connectionString, this.tableName);
            var logger = TestEventSource.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    var bufferingInterval = TimeSpan.FromSeconds(2);
                    listener.LogToWindowsAzureTable("mytestinstance", connectionString, this.tableName, bufferingInterval: bufferingInterval);
                    listener.EnableEvents(logger, EventLevel.Informational);

                    // 100 events or more will be flushed by count before the buffering interval elapses
                    for (int i = 0; i < 90; i++)
                    {
                        logger.Informational("Message1");
                    }

                    // Wait for buffer interval to elapse and allow time for events to be written
                    Task.Delay(bufferingInterval.Add(TimeSpan.FromSeconds(5))).Wait();
                    var events = AzureTableHelper.GetEventsCount(connectionString, this.tableName);
                    Assert.AreEqual(90, events);
                });
        }

        [TestMethod]
        public void WhenEventsInThreeConsecutiveIntervals()
        {
            this.tableName = "WhenEventsInThreeConsecutiveIntervals";
            var connectionString = System.Configuration.ConfigurationManager.AppSettings["StorageConnectionString"];
            AzureTableHelper.DeleteTable(connectionString, this.tableName);
            var logger = TestEventSource.Logger;

            var bufferingInterval = TimeSpan.FromSeconds(6);
            var insertionInterval = TimeSpan.FromSeconds(2);
            TestScenario.With1Listener(
                logger,
                (listener, errorsListener) =>
                {
                    listener.LogToWindowsAzureTable("mytestinstance", connectionString, this.tableName, bufferingInterval: bufferingInterval);
                    listener.EnableEvents(logger, EventLevel.Informational);

                    // 1st interval: Log 10 events
                    for (int i = 0; i < 10; i++)
                    {
                        logger.Informational("Message1");
                    }

                    // 1st interval: Wait for the buffer to flush at end of interval
                    Task.Delay(bufferingInterval).Wait();
                    // 2nd interval: start

                    // 1st interval: Wait for the events to be written and assert
                    Task.Delay(insertionInterval).Wait();
                    Assert.AreEqual(10, AzureTableHelper.GetEventsCount(connectionString, this.tableName));

                    // 2nd interval: Log 10 events
                    for (int i = 0; i < 10; i++)
                    {
                        logger.Informational("Message1");
                    }

                    // 2nd interval: Wait for the buffer to flush at end of interval
                    Task.Delay(bufferingInterval).Wait();
                    // 3rd interval: start

                    // 2nd interval: Wait for the events to be written and assert
                    Task.Delay(insertionInterval).Wait();
                    Assert.AreEqual(20, AzureTableHelper.GetEventsCount(connectionString, this.tableName));

                    // 3rd interval: Log 10 events
                    for (int i = 0; i < 10; i++)
                    {
                        logger.Informational("Message1");
                    }

                    // 3rd interval: Wait for the buffer to flush at end of interval
                    Task.Delay(bufferingInterval).Wait();
                    // 4th interval: start

                    // 3rd interval: Wait for the events to be written and assert
                    Task.Delay(insertionInterval).Wait();
                    Assert.AreEqual(30, AzureTableHelper.GetEventsCount(connectionString, this.tableName));

                    // No errors should have been reported
                    Assert.AreEqual(string.Empty, errorsListener.ToString());
                });

            // No more events should have been written during the last flush in the Dispose
            Assert.AreEqual(30, AzureTableHelper.GetEventsCount(connectionString, this.tableName));
        }

        [TestMethod]
        public void WhenSourceEnabledWitKeywordsAll()
        {
            this.tableName = "WhenSourceEnabledWitKeywordsAll";
            var connectionString = System.Configuration.ConfigurationManager.AppSettings["StorageConnectionString"];
            AzureTableHelper.DeleteTable(connectionString, this.tableName);
            var logger = TestEventSource.Logger;

            TestScenario.With1Listener(
               logger,
               listener =>
               {
                   listener.LogToWindowsAzureTable("mytestinstance", connectionString, this.tableName, bufferingInterval: TimeSpan.FromSeconds(10));
                   listener.EnableEvents(logger, EventLevel.LogAlways, Keywords.All);
                   logger.ErrorWithKeywordDiagnostic("Error with keyword Diagnostic");
                   logger.CriticalWithKeywordPage("Critical with keyword Page");
               });

            var events = AzureTableHelper.PollForEvents(connectionString, this.tableName, 2);
            Assert.AreEqual(2, events.Count());
            Assert.AreEqual("1", events.First().Keywords.ToString());
            Assert.AreEqual("4", events.ElementAt(1).Keywords.ToString());
        }

        [TestMethod]
        public void WhenNotEnabledWithKeywordsAndEventWithSpecificKeywordIsRaised()
        {
            this.tableName = "WhenNotEnabledWithKeywordsAndEventWithSpecificKeywordIsRaised";
            var connectionString = System.Configuration.ConfigurationManager.AppSettings["StorageConnectionString"];
            AzureTableHelper.DeleteTable(connectionString, this.tableName);
            var logger = TestEventSource.Logger;

            TestScenario.With1Listener(
               logger,
               listener =>
               {
                   listener.LogToWindowsAzureTable("mytestinstance", connectionString, this.tableName, bufferingInterval: TimeSpan.FromSeconds(10));
                   listener.EnableEvents(logger, EventLevel.LogAlways);
                   logger.ErrorWithKeywordDiagnostic("Error with keyword EventlogClassic");
               });

            var eventsCount = AzureTableHelper.GetEventsCount(connectionString, this.tableName);
            int eventCount = 0;
#if EVENT_SOURCE_PACKAGE
            eventCount = 1;
#endif
            Assert.AreEqual(eventCount, eventsCount);
        }

        [TestMethod]
        public void WhenListenerIsDisposed()
        {
            this.tableName = "WhenListenerIsDisposed";
            var connectionString = System.Configuration.ConfigurationManager.AppSettings["StorageConnectionString"];
            AzureTableHelper.DeleteTable(connectionString, this.tableName);
            var logger = TestEventSource.Logger;

            TestScenario.With2Listeners(
               logger,
               (listener1, listener2) =>
               {
                   listener1.LogToWindowsAzureTable("mytestinstance1", connectionString, this.tableName, bufferingInterval: TimeSpan.FromSeconds(20));
                   listener2.LogToWindowsAzureTable("mytestinstance2", connectionString, this.tableName, bufferingInterval: TimeSpan.FromSeconds(20));
                   listener1.EnableEvents(logger, EventLevel.LogAlways);
                   listener2.EnableEvents(logger, EventLevel.LogAlways);
                   var logTaskList = new List<Task>();
                   for (int i = 0; i < 105; i++)
                   {
                       var messageNumber = i;
                       logTaskList.Add(Task.Run(() => logger.Critical(messageNumber + "Critical message")));
                   }

                   Task.WaitAll(logTaskList.ToArray(), TimeSpan.FromSeconds(10));
                   listener1.Dispose();
                   listener2.Dispose();

                   var events = AzureTableHelper.PollForEvents(connectionString, this.tableName, 600);
                   Assert.AreEqual(210, events.Count());
               });
        }

        [TestMethod]
        public void WhenEventWithTaskNameInAttributeIsRaised()
        {
            this.tableName = "WhenEventWithTaskNameInAttributeIsRaised";
            var connectionString = System.Configuration.ConfigurationManager.AppSettings["StorageConnectionString"];
            AzureTableHelper.DeleteTable(connectionString, this.tableName);
            var logger = TestEventSource.Logger;

            TestScenario.With1Listener(
               logger,
               listener =>
               {
                   listener.LogToWindowsAzureTable("mytestinstance", connectionString, this.tableName, bufferingInterval: TimeSpan.FromSeconds(10));
                   listener.EnableEvents(logger, EventLevel.LogAlways, Keywords.All);
                   logger.CriticalWithTaskName("Critical with task name");
                   logger.CriticalWithKeywordPage("Critical with no task name");
               });

            var events = AzureTableHelper.PollForEvents(connectionString, this.tableName, 2);
            Assert.AreEqual(2, events.Count());
            Assert.AreEqual("64513", events.First().Task.ToString());
            Assert.AreEqual("2", events.ElementAt(1).Task.ToString());
        }

        [TestMethod]
        public void WhenEventWithEnumsInPayloadIsRaised()
        {
            this.tableName = "WhenEventWithEnumsInPayloadIsRaised";
            var connectionString = System.Configuration.ConfigurationManager.AppSettings["StorageConnectionString"];
            AzureTableHelper.DeleteTable(connectionString, this.tableName);
            var logger = MockEventSourceInProcEnum.Logger;

            TestScenario.With1Listener(
               logger,
               listener =>
               {
                   listener.LogToWindowsAzureTable("mytestinstance1", connectionString, this.tableName, bufferingInterval: TimeSpan.Zero);
                   listener.EnableEvents(logger, EventLevel.LogAlways);
                   logger.SendEnumsEvent17(MockEventSourceInProcEnum.MyColor.Green, MockEventSourceInProcEnum.MyFlags.Flag2);
               });

            var events = AzureTableHelper.PollForEvents(connectionString, this.tableName, 1);
            Assert.AreEqual(1, events.Count());
            Assert.AreEqual((int)MockEventSourceInProcEnum.Tasks.DBQuery, events.ElementAt(0).Task);
            Assert.AreEqual((int)EventOpcode.Resume, events.ElementAt(0).Opcode);
            StringAssert.Contains(events.ElementAt(0).Payload, @"""a"": 2");
            StringAssert.Contains(events.ElementAt(0).Payload, @"""b"": 2");
        }

        [TestMethod]
        public void WhenProcessId()
        {
            this.tableName = "WhenProcessId";
            var connectionString = System.Configuration.ConfigurationManager.AppSettings["StorageConnectionString"];
            AzureTableHelper.DeleteTable(connectionString, this.tableName);
            var logger = MockEventSourceInProcEnum.Logger;

            int processId = System.Diagnostics.Process.GetCurrentProcess().Id;

            TestScenario.With1Listener(
               logger,
               listener =>
               {
                   listener.LogToWindowsAzureTable("mytestinstance1", connectionString, this.tableName, bufferingInterval: TimeSpan.Zero);
                   listener.EnableEvents(logger, EventLevel.LogAlways);
                   logger.SendEnumsEvent17(MockEventSourceInProcEnum.MyColor.Green, MockEventSourceInProcEnum.MyFlags.Flag2);
               });

            var events = AzureTableHelper.PollForEvents(connectionString, this.tableName, 1);
            Assert.AreEqual(1, events.Count());
            Assert.AreEqual(processId, events.ElementAt(0).ProcessId);
        }

        [TestMethod]
        public void WhenThreadId()
        {
            this.tableName = "WhenThreadId";
            var connectionString = System.Configuration.ConfigurationManager.AppSettings["StorageConnectionString"];
            AzureTableHelper.DeleteTable(connectionString, this.tableName);
            var logger = MockEventSourceInProcEnum.Logger;

            int threadId = ThreadHelper.GetCurrentUnManagedThreadId();

            TestScenario.With1Listener(
               logger,
               listener =>
               {
                   listener.LogToWindowsAzureTable("mytestinstance1", connectionString, this.tableName, bufferingInterval: TimeSpan.Zero);
                   listener.EnableEvents(logger, EventLevel.LogAlways);
                   logger.SendEnumsEvent17(MockEventSourceInProcEnum.MyColor.Green, MockEventSourceInProcEnum.MyFlags.Flag2);
               });

            var events = AzureTableHelper.PollForEvents(connectionString, this.tableName, 1);
            Assert.AreEqual(1, events.Count());
            Assert.AreEqual(threadId, events.ElementAt(0).ThreadId);
        }
    }
}
