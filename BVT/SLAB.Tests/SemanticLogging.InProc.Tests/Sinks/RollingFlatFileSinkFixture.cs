// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Formatters;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.InProc.Tests.TestObjects;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.InProc.Tests.TestScenarios;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Sinks;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.Shared.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.InProc.Tests.Sinks
{
    [TestClass]
    public class RollingFlatFileSinkFixture
    {
        [TestMethod]
        public void WhenMaxArchivedFilesExceedsAndRollForSize()
        {
            var fileNameWithoutExtension = @".\Logs11\TestMaxArchivedFiles_RollSize";
            var fileName = fileNameWithoutExtension + ".log";
            FlatFileHelper.DeleteDirectory(@".\Logs11");
            var logger = MockEventSource.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToRollingFlatFile(fileName, 4, "yyyy", RollFileExistsBehavior.Increment, RollInterval.None, new EventTextFormatter(EventTextFormatter.DashSeparator), 2);
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    for (int msg = 0; msg < 40; msg++)
                    {
                        logger.Informational("The quick brown fox jumps over the lazy dog.  And then the fox did it again and again for time #  " + msg.ToString());
                    }
                });

            var files = FlatFileHelper.GetFileNames(fileNameWithoutExtension + "*.log");
            Assert.AreEqual<int>(3, files.Count());

            Assert.IsFalse(File.Exists(fileNameWithoutExtension + "." + DateTime.Now.Year + ".1" + ".log"));
            Assert.IsTrue(File.Exists(fileNameWithoutExtension + "." + DateTime.Now.Year + ".2" + ".log"));
            Assert.IsTrue(File.Exists(fileNameWithoutExtension + "." + DateTime.Now.Year + ".3" + ".log"));
        }

        [TestMethod]
        public void WhenRollSizeMetAndRollIntervalProvidedNotMet()
        {
            var fileNameWithoutExtension = "RollForSize_WhenSizeMet_RollIntervalProvidedNotMet";
            var fileName = fileNameWithoutExtension + ".log";
            FlatFileHelper.DeleteCreatedLogFiles(fileNameWithoutExtension);
            var logger = MockEventSource.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToRollingFlatFile(fileName, 1, "yyyy", RollFileExistsBehavior.Increment, RollInterval.Day, new EventTextFormatter(EventTextFormatter.DashSeparator));
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    logger.Informational("Message 1");
                    logger.Informational("Message 2");
                    logger.Informational("Message 3");
                    logger.Informational("Message 4");
                    logger.Informational("Message 5");
                    logger.Informational("Message 6");
                    logger.Informational("Message 7");
                    logger.Informational("Message 8");
                    logger.Informational("Message 9");
                });

            Assert.IsTrue(File.Exists(fileNameWithoutExtension + ".log"));
            Assert.IsTrue(File.Exists(fileNameWithoutExtension + "." + DateTime.Now.Year + ".1" + ".log"));
        }

        [TestMethod]
        public void WhenRollForSizeWithNegativeSize()
        {
            var fileNameWithoutExtension = "RollForSize_WithnegativeValue";
            var fileName = fileNameWithoutExtension + ".log";
            FlatFileHelper.DeleteCreatedLogFiles(fileNameWithoutExtension);
            var logger = MockEventSource.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToRollingFlatFile(fileName, -1, "yyyy", RollFileExistsBehavior.Increment, RollInterval.None, new EventTextFormatter(EventTextFormatter.DashSeparator));
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    logger.Informational("Message 1");
                    logger.Informational("Message 2");
                    logger.Informational("Message 3");
                    logger.Informational("Message 4");
                    logger.Informational("Message 5");
                    logger.Informational("Message 6");
                    logger.Informational("Message 7");
                    logger.Informational("Message 8");
                    logger.Informational("Message 9");
                });

            Assert.IsTrue(File.Exists(fileNameWithoutExtension + ".log"));
            Assert.IsFalse(File.Exists(fileNameWithoutExtension + "." + DateTime.Now.Year + ".1" + ".log"));
        }

        [TestMethod]
        public void WhenLoggingBeforeRollOccurs()
        {
            var fileNameWithoutExtension = "LogInConfiguredFileName";
            var fileName = fileNameWithoutExtension + ".log";
            FlatFileHelper.DeleteCreatedLogFiles(fileNameWithoutExtension);
            var logger = MockEventSource.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToRollingFlatFile(fileName, 1, "yyyy", RollFileExistsBehavior.Increment, RollInterval.Day, new EventTextFormatter(EventTextFormatter.DashSeparator));
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    logger.Informational("Message 1");
                    logger.Informational("Message 2");

                    Assert.IsTrue(File.Exists(fileNameWithoutExtension + ".log"));
                });

            StringAssert.Contains(File.ReadAllText(fileNameWithoutExtension + ".log"), "Payload : [message : Message 2]");
            StringAssert.Contains(File.ReadAllText(fileNameWithoutExtension + ".log"), "Payload : [message : Message 1]");
        }

        [TestMethod]
        public void WhenRollIntervalExceedsBeforeEvents()
        {
            var fileNameWithoutExtension = @".\Logs1\Roll_WhenNoEntryInLogFile";
            var fileName = fileNameWithoutExtension + ".log";
            FlatFileHelper.DeleteDirectory(@".\Logs1");
            var logger = MockEventSource.Logger;
            var dateTimeProvider = new MockDateTimeProvider();

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    var subscription = listener.LogToRollingFlatFile(fileName, 1, "yyyy", RollFileExistsBehavior.Increment, RollInterval.Day, new EventTextFormatter(EventTextFormatter.DashSeparator));
                    listener.EnableEvents(logger, EventLevel.LogAlways);

                    dateTimeProvider.OverrideCurrentDateTime = DateTime.Now.ToLocalTime();
                    subscription.Sink.RollingHelper.DateTimeProvider = dateTimeProvider;
                    subscription.Sink.RollingHelper.UpdateRollingInformationIfNecessary();

                    dateTimeProvider.OverrideCurrentDateTime = DateTime.Now.AddDays(1);
                    subscription.Sink.RollingHelper.DateTimeProvider = dateTimeProvider;

                    logger.Informational("Message 1");
                    logger.Informational("Message 2");
                });

            Assert.IsTrue(File.Exists(fileNameWithoutExtension + ".log"));
            Assert.IsTrue(File.Exists(fileNameWithoutExtension + "." + dateTimeProvider.OverrideCurrentDateTime.Value.Year + ".1" + ".log"));
            StringAssert.Contains(File.ReadAllText(fileNameWithoutExtension + ".log"), "Payload : [message : Message 2]");
        }

        [TestMethod]
        public void RollFileDateUsesFileCreationDate()
        {
            var fileNameWithoutExtension = "RollDateBasedOnLogCreationDate";
            var fileName = fileNameWithoutExtension + ".log";
            FlatFileHelper.DeleteCreatedLogFiles(fileNameWithoutExtension);
            var logger = MockEventSource.Logger;
            var dateTimeProvider = new MockDateTimeProvider();

            string existingMessage = "Existing Message";
            DateTime currentDateTime = new DateTime(2011, 1, 1);
            File.WriteAllText(fileName, existingMessage);
            File.SetCreationTime(fileName, currentDateTime);
            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    var subscription = listener.LogToRollingFlatFile(fileName, 1, "yyyy", RollFileExistsBehavior.Increment, RollInterval.Day, new EventTextFormatter());
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    dateTimeProvider.OverrideCurrentDateTime = currentDateTime;
                    dateTimeProvider.OverrideCurrentDateTime = currentDateTime.AddDays(2);
                    subscription.Sink.RollingHelper.DateTimeProvider = dateTimeProvider;

                    logger.Informational("New message");
                });

            Assert.AreEqual(existingMessage, File.ReadAllText(fileNameWithoutExtension + ".2011.1" + ".log"));
            Assert.IsTrue(File.ReadAllText(fileName).Contains("New message"));
        }

        [TestMethod]
        public void WhenRollWithSizeAndRollIntervalNoneWithOverwrite()
        {
            var fileNameWithoutExtension = "RollOverwrite_Timestamp_RollSize_RollIntervalNone";
            var fileName = fileNameWithoutExtension + ".log";
            FlatFileHelper.DeleteCreatedLogFiles(fileNameWithoutExtension);
            var logger = MockEventSource.Logger;
            var dateTimeProvider = new MockDateTimeProvider();

            string existingMessage = "Existing Message";
            File.WriteAllText(fileName, existingMessage);
            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToRollingFlatFile(fileName, 1, "yyyy", RollFileExistsBehavior.Overwrite, RollInterval.None, new EventTextFormatter());
                    listener.EnableEvents(logger, EventLevel.LogAlways);

                    for (int msg = 0; msg < 26; msg++)
                    {
                        logger.Informational("Message " + msg.ToString());
                    }
                });

            Assert.IsTrue(File.ReadAllText(fileName).Contains("Message 25"));
            Assert.IsFalse(File.ReadAllText(fileName).Contains("Existing Message"));
        }

        [TestMethod]
        public void WhenRollWithSizeAndRollIntervalNoneWithOverwriteTimeStampIsNotMandatory()
        {
            var fileNameWithoutExtension = "RollOverwrite_TimestampNone_RollSize_RollIntervalNone";
            var fileName = fileNameWithoutExtension + ".log";
            FlatFileHelper.DeleteCreatedLogFiles(fileNameWithoutExtension);
            var logger = MockEventSource.Logger;
            var dateTimeProvider = new MockDateTimeProvider();

            string existingMessage = "Existing Message";
            File.WriteAllText(fileName, existingMessage);
            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    var subscription = listener.LogToRollingFlatFile(fileName, 1, string.Empty, RollFileExistsBehavior.Overwrite, RollInterval.None, new EventTextFormatter());
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    subscription.Sink.RollingHelper.DateTimeProvider = dateTimeProvider;
                    logger.Informational("Message 1");
                    logger.Informational("Message 2");
                    logger.Informational("Message 3");
                    logger.Informational("Message 4");
                    logger.Informational("Message 5");
                    logger.Informational("Message 6");
                    logger.Informational("Message 7");
                    logger.Informational("Message 8");
                    logger.Informational("Message 9");
                    logger.Informational("Message 10");
                });

            Assert.IsTrue(File.ReadAllText(fileName).Contains("Message 10"));
            Assert.IsFalse(File.ReadAllText(fileName).Contains("Existing Message"));
        }

        [TestMethod]
        public void WhenRollOverwrite_TimestampNone_RollSizeNone_RollIntervalNone()
        {
            var fileNameWithoutExtension = "RollOverwrite_TimestampNone_RollSizeNone_RollIntervalNone";
            var fileName = fileNameWithoutExtension + ".log";
            FlatFileHelper.DeleteCreatedLogFiles(fileNameWithoutExtension);
            var logger = MockEventSource.Logger;
            var dateTimeProvider = new MockDateTimeProvider();

            string existingMessage = "Existing Message";
            File.WriteAllText(fileName, existingMessage);
            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    var subscription = listener.LogToRollingFlatFile(fileName, 0, string.Empty, RollFileExistsBehavior.Overwrite, RollInterval.None, new EventTextFormatter());
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    subscription.Sink.RollingHelper.DateTimeProvider = dateTimeProvider;
                    logger.Informational("Message 1");
                    logger.Informational("Message 2");
                    logger.Informational("Message 3");
                    logger.Informational("Message 4");
                    logger.Informational("Message 5");
                    logger.Informational("Message 6");
                    logger.Informational("Message 7");
                    logger.Informational("Message 8");
                    logger.Informational("Message 9");
                    logger.Informational("Message 10");
                });

            Assert.IsTrue(File.ReadAllText(fileName).Contains("Message 10"));
            Assert.IsTrue(File.ReadAllText(fileName).Contains("Existing Message"));
        }

        [TestMethod]
        public void WhenRollOverwrite_Timestamp_RollSizeNone_RollIntervalNone()
        {
            var fileNameWithoutExtension = "RollOverwrite_Timestamp_RollSizeNone_RollIntervalNone";
            var fileName = fileNameWithoutExtension + ".log";
            FlatFileHelper.DeleteCreatedLogFiles(fileNameWithoutExtension);
            var logger = MockEventSource.Logger;
            var dateTimeProvider = new MockDateTimeProvider();

            string existingMessage = "Existing Message";
            File.WriteAllText(fileName, existingMessage);
            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    var subscription = listener.LogToRollingFlatFile(fileName, 0, "yyyy", RollFileExistsBehavior.Overwrite, RollInterval.None, new EventTextFormatter());
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    subscription.Sink.RollingHelper.DateTimeProvider = dateTimeProvider;
                    logger.Informational("Message 1");
                    logger.Informational("Message 2");
                    logger.Informational("Message 3");
                    logger.Informational("Message 4");
                    logger.Informational("Message 5");
                    logger.Informational("Message 6");
                    logger.Informational("Message 7");
                    logger.Informational("Message 8");
                    logger.Informational("Message 9");
                    logger.Informational("Message 10");
                });

            Assert.IsTrue(File.ReadAllText(fileName).Contains("Message 10"));
            Assert.IsTrue(File.ReadAllText(fileName).Contains("Existing Message"));
        }

        [TestMethod]
        public void WhenRollInIncrementModeBasedOnInterval()
        {
            var fileNameWithoutExtension = @".\Logs2\RollFileIncrement";
            var fileName = fileNameWithoutExtension + ".log";
            FlatFileHelper.DeleteDirectory(@".\Logs2");
            var logger = MockEventSource.Logger;
            var dateTimeProvider = new MockDateTimeProvider();

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    var subscription = listener.LogToRollingFlatFile(fileName, 10, "yyyy", RollFileExistsBehavior.Increment, RollInterval.Day, new EventTextFormatter(EventTextFormatter.DashSeparator));
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    logger.Informational("Message 1");

                    MockDateTimeProvider dtp = new MockDateTimeProvider();
                    dtp.OverrideCurrentDateTime = DateTime.Now.AddDays(1);
                    subscription.Sink.RollingHelper.DateTimeProvider = dtp;

                    logger.Informational("Message 2");
                });

            Assert.IsTrue(File.Exists(fileNameWithoutExtension + ".log"));
            Assert.IsTrue(File.ReadAllText(fileNameWithoutExtension + ".log").Contains("Message 2"));

            Assert.IsTrue(File.Exists(fileNameWithoutExtension + "." + DateTime.Now.Year + ".1" + ".log"));
            Assert.IsTrue(File.ReadAllText(fileNameWithoutExtension + "." + DateTime.Now.Year + ".1" + ".log").Contains("Message 1"));
        }

        [TestMethod]
        public void WhenRollIntervalIsMinute()
        {
            var fileNameWithoutExtension = @".\Logs3\RollForSize_WhenRollInterval_Minute";
            var fileName = fileNameWithoutExtension + ".log";
            FlatFileHelper.DeleteDirectory(@".\Logs3");
            var logger = MockEventSource.Logger;
            var dateTimeProvider = new MockDateTimeProvider();

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    var subscription = listener.LogToRollingFlatFile(fileName, 1000, "yyyy", RollFileExistsBehavior.Increment, RollInterval.Minute, new EventTextFormatter(EventTextFormatter.DashSeparator));
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    logger.Informational("Message 1");
                    logger.Informational("Message 2");

                    dateTimeProvider.OverrideCurrentDateTime = DateTime.Now.AddMinutes(2);
                    subscription.Sink.RollingHelper.DateTimeProvider = dateTimeProvider;

                    logger.Informational("Message 3");
                    logger.Informational("Message 4");
                });

            Assert.IsTrue(File.Exists(fileNameWithoutExtension + ".log"));
            Assert.IsTrue(File.Exists(fileNameWithoutExtension + "." + dateTimeProvider.OverrideCurrentDateTime.Value.Year + ".1" + ".log"));
            Assert.IsTrue(File.ReadAllText(fileNameWithoutExtension + ".log").Contains("Message 4"));
            Assert.IsFalse(File.ReadAllText(fileNameWithoutExtension + ".log").Contains("Message 1"));
            Assert.IsTrue(File.ReadAllText(fileNameWithoutExtension + "." + dateTimeProvider.OverrideCurrentDateTime.Value.Year + ".1" + ".log").Contains("Message 1"));
        }

        [TestMethod]
        public void WhenRollIntervalIsHour()
        {
            var fileNameWithoutExtension = @".\Logs4\RollForSize_WhenRollInterval_Hour";
            var fileName = fileNameWithoutExtension + ".log";
            FlatFileHelper.DeleteDirectory(@".\Logs4");
            var logger = MockEventSource.Logger;
            var dateTimeProvider = new MockDateTimeProvider();
            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    var subscription = listener.LogToRollingFlatFile(fileName, 1000, "yyyy", RollFileExistsBehavior.Increment, RollInterval.Hour, new EventTextFormatter(EventTextFormatter.DashSeparator));
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    logger.Informational("Message 1");
                    logger.Informational("Message 2");

                    dateTimeProvider.OverrideCurrentDateTime = DateTime.Now.AddHours(1);
                    subscription.Sink.RollingHelper.DateTimeProvider = dateTimeProvider;
                    logger.Informational("Message 3");
                    logger.Informational("Message 4");
                });

            Assert.IsTrue(File.Exists(fileNameWithoutExtension + ".log"));
            Assert.IsTrue(File.ReadAllText(fileNameWithoutExtension + ".log").Contains("Message 4"));
            Assert.IsFalse(File.ReadAllText(fileNameWithoutExtension + ".log").Contains("Message 1"));
            Assert.IsTrue(File.ReadAllText(fileNameWithoutExtension + "." + DateTime.Now.Year + ".1" + ".log").Contains("Message 1"));
        }

        [TestMethod]
        public void WhenRollIntervalIsDay()
        {
            var fileNameWithoutExtension = @".\Logs5\RollForSize_WhenRollInterval_Day";
            var fileName = fileNameWithoutExtension + ".log";
            FlatFileHelper.DeleteDirectory(@".\Logs5");

            var logger = MockEventSource.Logger;
            var dateTimeProvider = new MockDateTimeProvider();
            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    var subscription = listener.LogToRollingFlatFile(fileName, 1000, "yyyy", RollFileExistsBehavior.Increment, RollInterval.Day, new EventTextFormatter(EventTextFormatter.DashSeparator));
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    logger.Informational("Message 1");
                    logger.Informational("Message 2");

                    dateTimeProvider.OverrideCurrentDateTime = DateTime.Now.AddDays(1);
                    subscription.Sink.RollingHelper.DateTimeProvider = dateTimeProvider;
                    subscription.Sink.RollingHelper.UpdateRollingInformationIfNecessary();

                    logger.Informational("Message 3");
                    logger.Informational("Message 4");
                });

            Assert.IsTrue(File.Exists(fileNameWithoutExtension + ".log"));
            Assert.IsTrue(File.Exists(fileNameWithoutExtension + "." + dateTimeProvider.OverrideCurrentDateTime.Value.Year + ".1" + ".log"));
            Assert.IsTrue(File.ReadAllText(fileNameWithoutExtension + ".log").Contains("Message 4"));
            Assert.IsFalse(File.ReadAllText(fileNameWithoutExtension + ".log").Contains("Message 1"));
            Assert.IsTrue(File.ReadAllText(fileNameWithoutExtension + "." + dateTimeProvider.OverrideCurrentDateTime.Value.Year + ".1" + ".log").Contains("Message 1"));
        }

        [TestMethod]
        public void WhenRollIntervalIsWeek()
        {
            var fileNameWithoutExtension = @".\Logs6\RollForSize_WhenRollInterval_Week";
            var fileName = fileNameWithoutExtension + ".log";
            FlatFileHelper.DeleteDirectory(@".\Logs6");
            var logger = MockEventSource.Logger;
            var dateTimeProvider = new MockDateTimeProvider();

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    var subscription = listener.LogToRollingFlatFile(fileName, 1000, "yyyy", RollFileExistsBehavior.Increment, RollInterval.Week, new EventTextFormatter(EventTextFormatter.DashSeparator));
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    logger.Informational("Message 1");
                    logger.Informational("Message 2");
                    dateTimeProvider.OverrideCurrentDateTime = DateTime.Now.AddDays(7);
                    subscription.Sink.RollingHelper.DateTimeProvider = dateTimeProvider;
                    logger.Informational("Message 3");
                    logger.Informational("Message 4");
                });

            Assert.IsTrue(File.Exists(fileNameWithoutExtension + ".log"));
            Assert.IsTrue(File.Exists(fileNameWithoutExtension + "." + dateTimeProvider.OverrideCurrentDateTime.Value.Year + ".1" + ".log"));
            Assert.IsTrue(File.ReadAllText(fileNameWithoutExtension + ".log").Contains("Message 4"));
            Assert.IsFalse(File.ReadAllText(fileNameWithoutExtension + ".log").Contains("Message 1"));
            Assert.IsTrue(File.ReadAllText(fileNameWithoutExtension + "." + dateTimeProvider.OverrideCurrentDateTime.Value.Year + ".1" + ".log").Contains("Message 1"));
        }

        [TestMethod]
        public void WhenRollIntervalIsMonth()
        {
            var fileNameWithoutExtension = @".\Logs7\RollForSize_WhenRollInterval_Month";
            var fileName = fileNameWithoutExtension + ".log";
            FlatFileHelper.DeleteDirectory(@".\Logs7");
            var logger = MockEventSource.Logger;
            var dateTimeProvider = new MockDateTimeProvider();

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    var subscription = listener.LogToRollingFlatFile(fileName, 1000, "yyyy", RollFileExistsBehavior.Increment, RollInterval.Month, new EventTextFormatter(EventTextFormatter.DashSeparator));
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    logger.Informational("Message 1");
                    logger.Informational("Message 2");
                    dateTimeProvider.OverrideCurrentDateTime = DateTime.Now.AddMonths(2);
                    subscription.Sink.RollingHelper.DateTimeProvider = dateTimeProvider;
                    logger.Informational("Message 3");
                    logger.Informational("Message 4");
                });

            Assert.IsTrue(File.Exists(fileNameWithoutExtension + ".log"));
            Assert.IsTrue(File.Exists(fileNameWithoutExtension + "." + dateTimeProvider.OverrideCurrentDateTime.Value.Year + ".1" + ".log"));
            Assert.IsTrue(File.ReadAllText(fileNameWithoutExtension + ".log").Contains("Message 4"));
            Assert.IsTrue(File.ReadAllText(fileNameWithoutExtension + "." + dateTimeProvider.OverrideCurrentDateTime.Value.Year + ".1" + ".log").Contains("Message 1"));
        }

        [TestMethod]
        public void WhenRollIntervalIsYear()
        {
            var fileNameWithoutExtension = @".\Logs8\RollForSize_WhenRollInterval_Year";
            var fileName = fileNameWithoutExtension + ".log";
            FlatFileHelper.DeleteDirectory(@".\Logs8");
            var logger = MockEventSource.Logger;
            var dateTimeProvider = new MockDateTimeProvider();
            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    var subscription = listener.LogToRollingFlatFile(fileName, 0, "yyyy", RollFileExistsBehavior.Increment, RollInterval.Year, new EventTextFormatter(EventTextFormatter.DashSeparator));
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    logger.Informational("Message 1");
                    logger.Informational("Message 2");
                    dateTimeProvider.OverrideCurrentDateTime = DateTime.Now.AddYears(1);
                    subscription.Sink.RollingHelper.DateTimeProvider = dateTimeProvider;
                    logger.Informational("Message 3");
                    logger.Informational("Message 4");
                });

            Assert.IsTrue(File.Exists(fileNameWithoutExtension + ".log"));
            Assert.IsTrue(File.Exists(fileNameWithoutExtension + "." + dateTimeProvider.OverrideCurrentDateTime.Value.Year + ".1" + ".log"));
            Assert.IsTrue(File.ReadAllText(fileNameWithoutExtension + ".log").Contains("Message 4"));
            Assert.IsFalse(File.ReadAllText(fileNameWithoutExtension + ".log").Contains("Message 1"));
            Assert.IsTrue(File.ReadAllText(fileNameWithoutExtension + "." + dateTimeProvider.OverrideCurrentDateTime.Value.Year + ".1" + ".log").Contains("Message 1"));
        }

        [TestMethod]
        public void WhenRollAtMidnight()
        {
            var fileNameWithoutExtension = @".\Logs9\WhenRollInterval_Midnight";
            var fileName = fileNameWithoutExtension + ".log";
            FlatFileHelper.DeleteDirectory(@".\Logs9");
            var logger = MockEventSource.Logger;
            var dateTimeProvider = new MockDateTimeProvider();
            DateTime? nextRoll = null;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    var subscription = listener.LogToRollingFlatFile(fileName, 0, "yyyy", RollFileExistsBehavior.Increment, RollInterval.Midnight, new EventTextFormatter(EventTextFormatter.DashSeparator));
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    logger.Informational("Message 1");
                    logger.Informational("Message 2");

                    nextRoll = DateTime.Now.AddDays(1);

                    dateTimeProvider.OverrideCurrentDateTime = nextRoll;
                    subscription.Sink.RollingHelper.DateTimeProvider = dateTimeProvider;

                    logger.Informational("Message 3");
                    logger.Informational("Message 4");
                });

            Assert.IsTrue(File.Exists(fileNameWithoutExtension + ".log"));
            Assert.IsTrue(File.Exists(fileNameWithoutExtension + "." + dateTimeProvider.OverrideCurrentDateTime.Value.Year + ".1" + ".log"));
            Assert.IsTrue(File.ReadAllText(fileNameWithoutExtension + ".log").Contains("Message 4"));
            Assert.IsFalse(File.ReadAllText(fileNameWithoutExtension + ".log").Contains("Message 1"));
            Assert.IsTrue(File.ReadAllText(fileNameWithoutExtension + "." + dateTimeProvider.OverrideCurrentDateTime.Value.Year + ".1" + ".log").Contains("Message 1"));
        }

        [TestMethod]
        public void WhenRollForSize()
        {
            var fileNameWithoutExtension = "Reset_RollForSize";
            var fileName = fileNameWithoutExtension + ".log";
            FlatFileHelper.DeleteCreatedLogFiles(fileNameWithoutExtension);
            var logger = MockEventSource.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToRollingFlatFile(fileName, 4, "yyyy", RollFileExistsBehavior.Increment, RollInterval.Day, new EventTextFormatter(EventTextFormatter.DashSeparator));
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    for (int msg = 0; msg < 100; msg++)
                    {
                        logger.Informational("Message " + msg.ToString());
                    }
                });

            var files = FlatFileHelper.GetFileNames(fileNameWithoutExtension + "*.log");
            string result = FlatFileHelper.ReadFromFiles(fileNameWithoutExtension + "*.log");

            Assert.IsTrue(files.Count() >= 5);
            for (int i = 0; i < 100; i++)
            {
                Assert.IsTrue(result.Contains("Message " + i.ToString()));
            }

            foreach (var file in files)
            {
                FileInfo fileInfo = new FileInfo(file);
                if ((fileInfo.Length >= 4300) && (fileInfo.Length <= 3200))
                {
                    Assert.Fail("Reset_RollForSize failed as the flatfile size exceeds the maximum limit for rolling");
                }
            }
        }

        [TestMethod]
        public void WhenRollIntervalExceedsMultipleTimes()
        {
            var fileNameWithoutExtension = @".\Logs10\RollForTime_MultipleLogs";
            var fileName = fileNameWithoutExtension + ".log";
            FlatFileHelper.DeleteDirectory(@".\Logs10");
            var logger = MockEventSource.Logger;
            var dateTimeProvider = new MockDateTimeProvider();

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    var subscription = listener.LogToRollingFlatFile(fileName, 10, "yyyy", RollFileExistsBehavior.Increment, RollInterval.Minute, new EventTextFormatter(EventTextFormatter.DashSeparator));
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    logger.Informational("Message 1");
                    logger.Informational("Message 2");

                    dateTimeProvider.OverrideCurrentDateTime = DateTime.Now.AddMinutes(2);
                    subscription.Sink.RollingHelper.DateTimeProvider = dateTimeProvider;
                    logger.Informational("Message 3");
                    logger.Informational("Message 4");

                    dateTimeProvider.OverrideCurrentDateTime = DateTime.Now.AddMinutes(4);
                    subscription.Sink.RollingHelper.DateTimeProvider = dateTimeProvider;
                    logger.Informational("Message 5");
                    logger.Informational("Message 6");
                });

            Assert.IsTrue(File.ReadAllText(fileNameWithoutExtension + ".log").Contains("Message 6"));
            Assert.IsTrue(File.Exists(fileNameWithoutExtension + "." + DateTime.Now.Year + ".2" + ".log"));
            Assert.IsTrue(File.ReadAllText(fileNameWithoutExtension + "." + DateTime.Now.Year + ".2" + ".log").Contains("Message 3"));
        }

        [TestMethod]
        public void WhenRollWithHyphenInTimestampPattern()
        {
            var fileNameWithoutExtension = "TimestamppatternWithHyphen";
            var fileName = fileNameWithoutExtension + ".log";
            FlatFileHelper.DeleteCreatedLogFiles(fileNameWithoutExtension);
            var logger = MockEventSource.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToRollingFlatFile(fileName, 1, "MM-dd-yyyy", RollFileExistsBehavior.Increment, RollInterval.Day, new EventTextFormatter(EventTextFormatter.DashSeparator));
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    logger.Informational("Message 1");
                    logger.Informational("Message 2");
                    logger.Informational("Message 3");
                    logger.Informational("Message 4");
                    logger.Informational("Message 5");
                    logger.Informational("Message 6");
                    logger.Informational("Message 7");
                    logger.Informational("Message 8");
                    Assert.IsTrue(File.Exists(fileNameWithoutExtension + ".log"));
                });

            Assert.IsTrue(File.ReadAllText(fileNameWithoutExtension + ".log").Contains("Message 8"));
            Assert.IsTrue(File.Exists(fileNameWithoutExtension + "." + DateTime.Now.ToString("MM-dd-yyyy") + ".1" + ".log"));
        }

        [TestMethod]
        public void WhenMaxArchivedFilesExceedsAndRollForInterval()
        {
            var fileNameWithoutExtension = @".\Logs12\TestMaxArchivedFiles_RollInterval";
            var fileName = fileNameWithoutExtension + ".log";
            FlatFileHelper.DeleteDirectory(@".\Logs12");
            var logger = MockEventSource.Logger;
            var dateTimeProvider = new MockDateTimeProvider();

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    var subscription = listener.LogToRollingFlatFile(fileName, 0, "yyyy", RollFileExistsBehavior.Increment, RollInterval.Day, new EventTextFormatter(EventTextFormatter.DashSeparator), 2);
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    logger.Informational("Message 1");
                    logger.Informational("Message 2");

                    dateTimeProvider.OverrideCurrentDateTime = DateTime.Now.AddDays(2);
                    subscription.Sink.RollingHelper.DateTimeProvider = dateTimeProvider;
                    logger.Informational("Message 3");
                    logger.Informational("Message 4");

                    dateTimeProvider.OverrideCurrentDateTime = DateTime.Now.AddDays(4);
                    subscription.Sink.RollingHelper.DateTimeProvider = dateTimeProvider;
                    logger.Informational("Message 5");
                    logger.Informational("Message 6");

                    dateTimeProvider.OverrideCurrentDateTime = DateTime.Now.AddDays(6);
                    subscription.Sink.RollingHelper.DateTimeProvider = dateTimeProvider;
                    logger.Informational("Message 7");
                    logger.Informational("Message 8");

                    dateTimeProvider.OverrideCurrentDateTime = DateTime.Now.AddDays(8);
                    subscription.Sink.RollingHelper.DateTimeProvider = dateTimeProvider;
                    logger.Informational("Message 9");
                    logger.Informational("Message 10");
                });

            Assert.AreEqual<int>(3, FlatFileHelper.GetFileNames(fileNameWithoutExtension + "*.log").Count());
            Assert.IsFalse(File.Exists(fileNameWithoutExtension + "." + DateTime.Now.Year + ".1" + ".log"));
            Assert.IsFalse(File.Exists(fileNameWithoutExtension + "." + DateTime.Now.Year + ".2" + ".log"));
            Assert.IsTrue(File.Exists(fileNameWithoutExtension + "." + DateTime.Now.Year + ".3" + ".log"));
            Assert.IsTrue(File.Exists(fileNameWithoutExtension + "." + DateTime.Now.Year + ".4" + ".log"));
        }

        [TestMethod]
        public void WhenMaxArchivedFilesExceedsAndRollingACrossDifferentDatesWithRollInterval()
        {
            var fileNameWithoutExtension = @".\Logs13\TestMaxArchivedFiles_RollInterval_CreatedOnDifferentDates";
            var fileName = fileNameWithoutExtension + ".log";
            FlatFileHelper.DeleteDirectory(@".\Logs13");
            var logger = MockEventSource.Logger;
            var dateTimeProvider = new MockDateTimeProvider();

            string existingMessage = "Existing Message";
            DateTime currentDateTime = new DateTime(2010, 1, 1);
            Directory.CreateDirectory(@".\Logs13");
            File.WriteAllText(fileName, existingMessage);
            File.SetCreationTime(fileName, currentDateTime);
            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    var subscription = listener.LogToRollingFlatFile(fileName, 0, "yyyy", RollFileExistsBehavior.Increment, RollInterval.Day, new EventTextFormatter(EventTextFormatter.DashSeparator), 2);
                    listener.EnableEvents(logger, EventLevel.LogAlways);

                    dateTimeProvider.OverrideCurrentDateTime = currentDateTime;
                    subscription.Sink.RollingHelper.DateTimeProvider = dateTimeProvider;
                    logger.Informational("Message 1");
                    dateTimeProvider.OverrideCurrentDateTime = currentDateTime.AddDays(4);
                    subscription.Sink.RollingHelper.DateTimeProvider = dateTimeProvider;

                    logger.Informational("Message 2");

                    dateTimeProvider.OverrideCurrentDateTime = currentDateTime.AddDays(6);
                    subscription.Sink.RollingHelper.DateTimeProvider = dateTimeProvider;
                    logger.Informational("Message 3");

                    dateTimeProvider.OverrideCurrentDateTime = DateTime.Now.AddDays(4);
                    subscription.Sink.RollingHelper.DateTimeProvider = dateTimeProvider;
                    logger.Informational("Message 4");

                    dateTimeProvider.OverrideCurrentDateTime = DateTime.Now.AddDays(6);
                    subscription.Sink.RollingHelper.DateTimeProvider = dateTimeProvider;
                    logger.Informational("Message 5");

                    dateTimeProvider.OverrideCurrentDateTime = DateTime.Now.AddDays(8);
                    subscription.Sink.RollingHelper.DateTimeProvider = dateTimeProvider;
                    logger.Informational("Message 6");
                });

            var files = FlatFileHelper.GetFileNames(fileNameWithoutExtension + "*.log");
            Assert.IsFalse(File.Exists(fileNameWithoutExtension + "." + DateTime.Now.Year + ".1" + ".log"));
            Assert.AreEqual<int>(3, files.Count());
        }

        [TestMethod]
        public void WhenMaxArchivedFileSizeIsNegativeFilesAreNotDeleted()
        {
            var fileNameWithoutExtension = "TestMaxArchivedFilesNegative_RollSize";
            var fileName = fileNameWithoutExtension + ".log";
            FlatFileHelper.DeleteCreatedLogFiles(fileNameWithoutExtension);
            var logger = MockEventSource.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToRollingFlatFile(fileName, 1, "yyyy", RollFileExistsBehavior.Increment, RollInterval.None, new EventTextFormatter(EventTextFormatter.DashSeparator), -2);
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    for (int msg = 0; msg < 30; msg++)
                    {
                        logger.Informational("Message " + msg.ToString());
                    }
                });

            Assert.IsTrue(File.Exists(fileNameWithoutExtension + "." + DateTime.Now.Year + ".1" + ".log"));
            Assert.IsTrue(File.Exists(fileNameWithoutExtension + "." + DateTime.Now.Year + ".2" + ".log"));
            Assert.IsTrue(File.Exists(fileNameWithoutExtension + "." + DateTime.Now.Year + ".3" + ".log"));
            Assert.IsTrue(File.Exists(fileNameWithoutExtension + "." + DateTime.Now.Year + ".4" + ".log"));
        }

        [TestMethod]
        public void WhenMaxArchivedFilesIsZeroFilesAreNotDeleted()
        {
            var fileNameWithoutExtension = "TestMaxArchivedFilesAsZero_RollSize";
            var fileName = fileNameWithoutExtension + ".log";
            FlatFileHelper.DeleteCreatedLogFiles(fileNameWithoutExtension);
            var logger = MockEventSource.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToRollingFlatFile(fileName, 1, "yyyy", RollFileExistsBehavior.Increment, RollInterval.None, new EventTextFormatter(EventTextFormatter.DashSeparator), 0);
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    for (int msg = 0; msg < 30; msg++)
                    {
                        logger.Informational("Message " + msg.ToString());
                    }
                });

            Assert.IsTrue(File.Exists(fileNameWithoutExtension + "." + DateTime.Now.Year + ".1" + ".log"));
            Assert.IsTrue(File.Exists(fileNameWithoutExtension + "." + DateTime.Now.Year + ".2" + ".log"));
            Assert.IsTrue(File.Exists(fileNameWithoutExtension + "." + DateTime.Now.Year + ".3" + ".log"));
            Assert.IsTrue(File.Exists(fileNameWithoutExtension + "." + DateTime.Now.Year + ".4" + ".log"));
        }

        [TestMethod]
        public void WhenLoggingEventsInDifferentLevels()
        {
            var fileNameWithoutExtension = "TestAllEventLevels";
            var fileName = fileNameWithoutExtension + ".log";
            FlatFileHelper.DeleteCreatedLogFiles(fileNameWithoutExtension);
            var logger = MockEventSource.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToRollingFlatFile(fileName, 2, "yyyy", RollFileExistsBehavior.Increment, RollInterval.None, new EventTextFormatter(EventTextFormatter.DashSeparator));
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    logger.Informational("Message Info");
                    logger.Verbose("Message Verb");
                    logger.Critical("Message Critic");
                    logger.Error("Message Err");
                    logger.Warning("Message Warn");
                    logger.LogAlways("Message Log");
                });

            Assert.IsTrue(File.Exists(fileNameWithoutExtension + ".log"));
            Assert.IsTrue(File.ReadAllText(fileNameWithoutExtension + ".log").Contains("Message Info"));
            Assert.IsTrue(File.ReadAllText(fileNameWithoutExtension + ".log").Contains("Message Verb"));
            Assert.IsTrue(File.ReadAllText(fileNameWithoutExtension + ".log").Contains("Message Critic"));
            Assert.IsTrue(File.ReadAllText(fileNameWithoutExtension + ".log").Contains("Message Err"));
            Assert.IsTrue(File.ReadAllText(fileNameWithoutExtension + ".log").Contains("Message Warn"));
            Assert.IsTrue(File.ReadAllText(fileNameWithoutExtension + ".log").Contains("Message Log"));
        }

        [TestMethod]
        public void WhenFileNameIsNull()
        {
            using (var listener = new ObservableEventListener())
            {
                var subscription = listener.LogToRollingFlatFile(null, 2, "yyyy", RollFileExistsBehavior.Increment, RollInterval.None, new EventTextFormatter(EventTextFormatter.DashSeparator));

                Assert.IsNotNull(subscription);
            }
        }

        [TestMethod]
        public void WhenFileNameIsEmpty()
        {
            using (var listener = new ObservableEventListener())
            {
                var subscription = listener.LogToRollingFlatFile(string.Empty, 1, "yyyy", RollFileExistsBehavior.Increment, RollInterval.None, new EventTextFormatter(EventTextFormatter.DashSeparator));

                Assert.IsNotNull(subscription);
            }
        }

        [TestMethod]
        public void WhenTimestampPatternIsEmpty()
        {
            var excpectionThrown = ExceptionAssertHelper.Throws<ArgumentException>(() =>
                {
                    using (var listener = new ObservableEventListener())
                    {
                        listener.LogToRollingFlatFile("TestForTimestampAsEmptyString.log", 0, string.Empty, RollFileExistsBehavior.Increment, RollInterval.Midnight, new EventTextFormatter(EventTextFormatter.DashSeparator));
                    }
                });

            Assert.AreEqual("Argument is empty\r\nParameter name: timestampPattern", excpectionThrown.Message);
        }

        [TestMethod]
        public void WhenFilenameInvalidErrorOCcursEarly()
        {
            var excpectionThrown = ExceptionAssertHelper.Throws<ArgumentException>(() =>
                {
                    using (var listener = new ObservableEventListener())
                    {
                        listener.LogToRollingFlatFile(@">", 1, "yyyy", RollFileExistsBehavior.Increment, RollInterval.None, new EventTextFormatter(EventTextFormatter.DashSeparator));
                    }
                });

            Assert.AreEqual("Illegal characters in path.", excpectionThrown.Message);
        }

        [TestMethod]
        public void WhenFilenameInvalidErrorOCcursEarly1()
        {
            var excpectionThrown = ExceptionAssertHelper.Throws<ArgumentException>(() =>
                {
                    using (var listener = new ObservableEventListener())
                    {
                        listener.LogToRollingFlatFile(@"|", 1, "yyyy", RollFileExistsBehavior.Increment, RollInterval.None, new EventTextFormatter(EventTextFormatter.DashSeparator));
                    }
                });

            Assert.AreEqual("Illegal characters in path.", excpectionThrown.Message);
        }

        [TestMethod]
        public void WhenFilenameInvalidErrorOCcursEarly2()
        {
            var excpectionThrown = ExceptionAssertHelper.Throws<ArgumentException>(() =>
                {
                    using (var listener = new ObservableEventListener())
                    {
                        listener.LogToRollingFlatFile(@"..\", 1, "yyyy", RollFileExistsBehavior.Increment, RollInterval.None, new EventTextFormatter(EventTextFormatter.DashSeparator));
                    }
                });

            Assert.AreEqual("A file name with a relative path is not allowed. Provide only the file name or the full path of the file.", excpectionThrown.Message);
        }

        [TestMethod]
        public void WhenEventTextFormatterIsNull()
        {
            var fileNameWithoutExtension = "RFFLWhenEventTextFormatterIsNull";
            var fileName = fileNameWithoutExtension + ".log";
            FlatFileHelper.DeleteCreatedLogFiles(fileNameWithoutExtension);
            var logger = MockEventSource.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToRollingFlatFile(fileName, 2, "yyyy", RollFileExistsBehavior.Increment, RollInterval.None, null);
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    logger.Informational("Message Info");
                });

            Assert.IsTrue(File.Exists(fileNameWithoutExtension + ".log"));
            Assert.IsTrue(File.ReadAllText(fileNameWithoutExtension + ".log").Contains("Message Info"));
            Assert.IsFalse(File.ReadAllText(fileNameWithoutExtension + ".log").Contains(EventTextFormatter.DashSeparator));
        }

        [TestMethod]
        public void WhenTimeStampPatternIsNull()
        {
            var fileNameWithoutExtension = "TestForTimeStampPatternAsNull";
            var fileName = fileNameWithoutExtension + ".log";
            FlatFileHelper.DeleteCreatedLogFiles(fileNameWithoutExtension);
            var logger = MockEventSource.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToRollingFlatFile(fileName, 1, null, RollFileExistsBehavior.Increment, RollInterval.None);
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    logger.Informational("Message Info1");
                    logger.Informational("Message Info2");
                    logger.Informational("Message Info3");
                    logger.Informational("Message Info4");
                    logger.Informational("Message Info5");
                    logger.Informational("Message Info6");
                    logger.Informational("Message Info7");
                    logger.Informational("Message Info8");
                });

            Assert.IsTrue(File.Exists(fileNameWithoutExtension + ".log"));
            Assert.IsTrue(File.Exists(fileNameWithoutExtension + ".1" + ".log"));
        }

        [TestMethod]
        public void WhenFormattingErrorExceptionIsRoutedToBuiltInSource()
        {
            var fileNameWithoutExtension = "ErrorInFormatterIsHandled";
            var fileName = fileNameWithoutExtension + ".log";
            FlatFileHelper.DeleteCreatedLogFiles(fileNameWithoutExtension);
            var logger = TestEventSourceNonTransient.Logger;

            TestScenario.With1Listener(
                logger,
                (listener, errorsListener) =>
                {
                    listener.LogToRollingFlatFile(fileName, 1, null, RollFileExistsBehavior.Increment, RollInterval.None, new MockFormatter(true));
                    listener.EnableEvents(logger, EventLevel.LogAlways);

                    logger.EventWithPayload("payload1", 100);

                    StringAssert.Contains(errorsListener.ToString(), "Payload : [message : System.InvalidOperationException: Operation is not valid due to the current state of the object.");
                });
        }

        [TestMethod]
        public void WhenAllKeywordsAreEnabled()
        {
            var fileNameWithoutExtension = "RollingFlatFileEL_For_Keywords_All";
            var fileName = fileNameWithoutExtension + ".log";
            FlatFileHelper.DeleteCreatedLogFiles(fileNameWithoutExtension);
            var logger = MockEventSource.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    var formatter = new EventTextFormatter("====", "=====", EventLevel.LogAlways);
                    listener.LogToRollingFlatFile(fileName, 1, "yyyy", RollFileExistsBehavior.Increment, RollInterval.Day, formatter);
                    listener.EnableEvents(logger, EventLevel.LogAlways, Keywords.All);
                    logger.VerboseWithKeywordPage("VerboseWithKeywordPage");
                    logger.InfoWithKeywordDiagnostic("InfoWithKeywordDiagnostic");
                    Assert.IsTrue(File.Exists(fileNameWithoutExtension + ".log"));
                });

            StringAssert.Contains(File.ReadAllText(fileNameWithoutExtension + ".log"), "Keywords : 1");
            StringAssert.Contains(File.ReadAllText(fileNameWithoutExtension + ".log"), "Keywords : 4");
        }

        [TestMethod]
        public void WhenKeywordsAreNotSpecifiedWithEnabled()
        {
            var fileNameWithoutExtension = "RollingFlatFileEL_Without_Keywords_All_KeywordAsNotNone";
            var fileName = fileNameWithoutExtension + ".log";
            FlatFileHelper.DeleteCreatedLogFiles(fileNameWithoutExtension);
            var logger = MockEventSource.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToRollingFlatFile(fileName, 1, "yyyy", RollFileExistsBehavior.Increment, RollInterval.Day, new EventTextFormatter(EventTextFormatter.DashSeparator));
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    logger.VerboseWithKeywordPage("VerboseWithKeywordPage");
                    logger.Critical("Critical");
                });

            Assert.IsTrue(File.Exists(fileNameWithoutExtension + ".log"));
            Assert.IsTrue(File.ReadAllText(fileNameWithoutExtension + ".log").Contains("Payload : [message : Critical]"));
            Assert.IsFalse(File.ReadAllText(fileNameWithoutExtension + ".log").Contains("Payload : [message : VerboseWithKeywordPage]"));
        }

        [TestMethod]
        public void WhenActivityId()
        {
            var fileNameWithoutExtension = "RollingFlatFileEL_WhenActivityId";
            var fileName = fileNameWithoutExtension + ".log";
            FlatFileHelper.DeleteCreatedLogFiles(fileNameWithoutExtension);
            var logger = MockEventSource.Logger;

            var activityId = Guid.NewGuid();
            var previousActivityId = Guid.Empty;
            EventSource.SetCurrentThreadActivityId(activityId, out previousActivityId);
            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToRollingFlatFile(fileName, 1, "yyyy", RollFileExistsBehavior.Increment, RollInterval.Day, new EventTextFormatter(EventTextFormatter.DashSeparator));
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    logger.Critical("Critical");
                });

            EventSource.SetCurrentThreadActivityId(previousActivityId);

            Assert.IsTrue(File.Exists(fileNameWithoutExtension + ".log"));
            Assert.IsTrue(File.ReadAllText(fileNameWithoutExtension + ".log").Contains("Payload : [message : Critical]"));
            Assert.IsTrue(File.ReadAllText(fileNameWithoutExtension + ".log").Contains("ActivityId : " + activityId.ToString()));
            Assert.IsFalse(File.ReadAllText(fileNameWithoutExtension + ".log").Contains("RelatedActivityId : "));
        }

        [TestMethod]
        public void WhenActivityIdAndRelatedActivityId()
        {
            var fileNameWithoutExtension = "RollingFlatFileEL_WhenActivityId";
            var fileName = fileNameWithoutExtension + ".log";
            FlatFileHelper.DeleteCreatedLogFiles(fileNameWithoutExtension);
            var logger = MockEventSource.Logger;

            var activityId = Guid.NewGuid();
            var relatedActivityId = Guid.NewGuid();
            var previousActivityId = Guid.Empty;
            EventSource.SetCurrentThreadActivityId(activityId, out previousActivityId);
            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToRollingFlatFile(fileName, 1, "yyyy", RollFileExistsBehavior.Increment, RollInterval.Day, new EventTextFormatter(EventTextFormatter.DashSeparator));
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    logger.CriticalWithRelatedActivityId("Critical", relatedActivityId);
                });

            EventSource.SetCurrentThreadActivityId(previousActivityId);

            Assert.IsTrue(File.Exists(fileNameWithoutExtension + ".log"));
            Assert.IsTrue(File.ReadAllText(fileNameWithoutExtension + ".log").Contains("Payload : [message : Critical]"));
            Assert.IsTrue(File.ReadAllText(fileNameWithoutExtension + ".log").Contains("ActivityId : " + activityId.ToString()));
            Assert.IsTrue(File.ReadAllText(fileNameWithoutExtension + ".log").Contains("RelatedActivityId : " + relatedActivityId.ToString()));
        }

        [TestMethod]
        public void WhenTaskNameIsSpecifiedForEvent()
        {
            var fileNameWithoutExtension = "RollingFlatFileEL_For_TaskName";
            var fileName = fileNameWithoutExtension + ".log";
            FlatFileHelper.DeleteCreatedLogFiles(fileNameWithoutExtension);
            var logger = MockEventSource.Logger;
            var eventTextFormatter = new EventTextFormatter(EventTextFormatter.DashSeparator);
            eventTextFormatter.VerbosityThreshold = EventLevel.LogAlways;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToRollingFlatFile(fileName, 1, "yyyy", RollFileExistsBehavior.Increment, RollInterval.Day, eventTextFormatter);
                    listener.EnableEvents(logger, EventLevel.LogAlways, Keywords.All);
                    logger.CriticalWithTaskName("CriticalWithTaskName");
                    logger.InfoWithKeywordDiagnostic("InfoWithKeywordDiagnostic");
                    Assert.IsTrue(File.Exists(fileNameWithoutExtension + ".log"));
                });

            StringAssert.Contains(File.ReadAllText(fileNameWithoutExtension + ".log"), "Task : 1\r\nVersion : 0\r\nPayload : [message : CriticalWithTaskName] \r\nEventName : PageInfo");
            StringAssert.Contains(File.ReadAllText(fileNameWithoutExtension + ".log"), "Task : 64512\r\nVersion : 0\r\nPayload : [message : InfoWithKeywordDiagnostic] \r\nEventName : InfoWithKeywordDiagnosticInfo");
        }

        [TestMethod]
        public void WhenMultipleEventsAreLogged()
        {
            var fileNameWithoutExtension = "CanLogMultipleMessages";
            var fileName = fileNameWithoutExtension + ".log";
            FlatFileHelper.DeleteCreatedLogFiles(fileNameWithoutExtension);
            var logger = MockEventSource.Logger;
            EventTextFormatter formatter = new EventTextFormatter("------======------");

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToRollingFlatFile(fileName, 300000, "d", RollFileExistsBehavior.Increment, RollInterval.Year, formatter);
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    for (int n = 0; n < 300; n++)
                    {
                        logger.Informational("some message to flat file " + n.ToString());
                    }
                });

            Assert.IsTrue(File.Exists(fileName));
            var entries = FlatFileHelper.PollUntilTextEventsAreWritten(fileName, 300, formatter.Header);
            Assert.AreEqual<int>(300, entries.Count());
            StringAssert.Contains(entries.First(), "some message to flat file 0");
            StringAssert.Contains(entries.Last(), "some message to flat file 299");
        }

        [TestMethod]
        public void WhenPositionalParametersInPayload()
        {
            var fileNameWithoutExtension = "Task1557_ForRollingFlatFile_EventLogFormatter";
            var fileName = fileNameWithoutExtension + ".log";
            FlatFileHelper.DeleteCreatedLogFiles(fileNameWithoutExtension);
            var logger = TestEventSourceNoAttributes.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToRollingFlatFile(fileName, 1, "yyyy", RollFileExistsBehavior.Increment, RollInterval.None, new EventTextFormatter(EventTextFormatter.DashSeparator), 0);
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    for (int i = 0; i < 7; i++)
                    {
                        logger.ObjectArrayEvent4(100, "stringstringarg1", 200, "stringstringarg2", 300);
                    }

                    Assert.IsTrue(File.Exists(fileNameWithoutExtension + "." + DateTime.Now.Year + ".1.log"));
                });

            string readFile = File.ReadAllText(fileName);
            Assert.IsTrue(readFile.Contains("Check if it is logged"));
            Assert.IsTrue(readFile.Contains("[arg0 : 100] [arg1 : stringstringarg1] [arg2 : 200] [arg3 : stringstringarg2] [arg4 : 300]"));
        }

        [TestMethod]
        public void WhenPositionalParametersInPayloadInJson()
        {
            var fileNameWithoutExtension = "Task1557_ForRollingFlatFile_JsonFormatter";
            var fileName = fileNameWithoutExtension + ".log";
            FlatFileHelper.DeleteCreatedLogFiles(fileNameWithoutExtension);
            var logger = TestEventSourceNoAttributes.Logger;
            var jsonFormatter = new JsonEventTextFormatter();
            jsonFormatter.DateTimeFormat = "dd/MM/yyyy";

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToRollingFlatFile(fileName, 1, "yyyy", RollFileExistsBehavior.Increment, RollInterval.None, jsonFormatter, 0);
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    for (int i = 0; i < 7; i++)
                    {
                        logger.ObjectArrayEvent4(1000, "stringstringarg10", 2000, "stringstringarg20", 3000);
                    }

                    Assert.IsTrue(File.Exists(fileNameWithoutExtension + "." + DateTime.Now.Year + ".1.log"));
                });

            string readFile = File.ReadAllText(fileName);
            Assert.IsTrue(readFile.Contains("Check if it is logged"));
            Assert.IsTrue(readFile.Contains("{\"arg0\":1000,\"arg1\":\"stringstringarg10\",\"arg2\":2000,\"arg3\":\"stringstringarg20\",\"arg4\":3000}"));
        }

        [TestMethod]
        public void WhenPositionalParametersInPayloadInXml()
        {
            var fileNameWithoutExtension = "Task1557_ForRollingFlatFile_XmlFormatter";
            var fileName = fileNameWithoutExtension + ".log";
            FlatFileHelper.DeleteCreatedLogFiles(fileNameWithoutExtension);
            var logger = TestEventSourceNoAttributes.Logger;
            var xmlFormatter = new XmlEventTextFormatter();
            xmlFormatter.DateTimeFormat = "dd/MM/yyyy";

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToRollingFlatFile(fileName, 1, "yyyy", RollFileExistsBehavior.Increment, RollInterval.None, xmlFormatter, 0);
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    for (int i = 0; i < 7; i++)
                    {
                        logger.ObjectArrayEvent4(1000, "stringstringarg10", 2000, "stringstringarg20", 3000);
                    }

                    Assert.IsTrue(File.Exists(fileNameWithoutExtension + "." + DateTime.Now.Year + ".1.log"));
                });

            string readFile = File.ReadAllText(fileName);
            Assert.IsTrue(readFile.Contains("<Message>Check if it is logged</Message>"));
            Assert.IsTrue(readFile.Contains("<Data Name=\"arg0\">1000</Data><Data Name=\"arg1\">stringstringarg10</Data><Data Name=\"arg2\">2000</Data><Data Name=\"arg3\">stringstringarg20</Data><Data Name=\"arg4\">3000</Data>"));
        }

        [TestMethod]
        public void WhenProcessId()
        {
            var fileNameWithoutExtension = "RollingFlatFileEL_WhenProcessId";
            var fileName = fileNameWithoutExtension + ".log";
            FlatFileHelper.DeleteCreatedLogFiles(fileNameWithoutExtension);
            var logger = MockEventSource.Logger;

            int processId = System.Diagnostics.Process.GetCurrentProcess().Id;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToRollingFlatFile(fileName, 1, "yyyy", RollFileExistsBehavior.Increment, RollInterval.Day, new EventTextFormatter(EventTextFormatter.DashSeparator));
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    logger.Critical("Critical");
                });

            Assert.IsTrue(File.Exists(fileNameWithoutExtension + ".log"));
            Assert.IsTrue(File.ReadAllText(fileNameWithoutExtension + ".log").Contains("Payload : [message : Critical]"));
            Assert.IsTrue(File.ReadAllText(fileNameWithoutExtension + ".log").Contains("ProcessId : " + processId.ToString()));
        }

        public void WhenThreadId()
        {
            var fileNameWithoutExtension = "RollingFlatFileEL_WhenThreadId";
            var fileName = fileNameWithoutExtension + ".log";
            FlatFileHelper.DeleteCreatedLogFiles(fileNameWithoutExtension);
            var logger = MockEventSource.Logger;

            int threadId = ThreadHelper.GetCurrentUnManagedThreadId();

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToRollingFlatFile(fileName, 1, "yyyy", RollFileExistsBehavior.Increment, RollInterval.Day, new EventTextFormatter(EventTextFormatter.DashSeparator));
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    logger.Critical("Critical");
                });

            Assert.IsTrue(File.Exists(fileNameWithoutExtension + ".log"));
            Assert.IsTrue(File.ReadAllText(fileNameWithoutExtension + ".log").Contains("Payload : [message : Critical]"));
            Assert.IsTrue(File.ReadAllText(fileNameWithoutExtension + ".log").Contains("ThreadId : " + threadId.ToString()));
        }
    }
}