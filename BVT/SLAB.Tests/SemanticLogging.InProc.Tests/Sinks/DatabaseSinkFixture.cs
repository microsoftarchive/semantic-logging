// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.InProc.Tests.TestObjects;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.Shared.TestObjects;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.Shared.TestSupport;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Utility;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.InProc.Tests.Sinks
{
    [TestClass]
    public class DatabaseSinkFixture
    {
        [TestMethod]
        public void WhenInformationalEvent()
        {
            var validConnectionString = System.Configuration.ConfigurationManager.ConnectionStrings["valid"].ConnectionString;
            DatabaseHelper.CleanLoggingDB(validConnectionString);
            var logger = MockEventSource.Logger;

            var message = string.Empty;
            using (ObservableEventListener eventListener = new ObservableEventListener())
            {
                try
                {
                    eventListener.LogToSqlDatabase("mytestinstance1", validConnectionString);
                    eventListener.EnableEvents(logger, EventLevel.LogAlways);

                    message = string.Concat("Message ", Guid.NewGuid());
                    logger.Informational(message);
                }
                finally
                {
                    eventListener.DisableEvents(logger);
                }
            }

            var dt = DatabaseHelper.GetLoggedTable(validConnectionString);
            Assert.IsNotNull(dt, "No data logged");
            Assert.IsTrue(dt.Rows.Count > 0);
            var dr = dt.Rows[0];
            Assert.AreEqual((int)EventLevel.Informational, int.Parse(dr["Level"].ToString()));
            Assert.AreEqual(1, (int)dr["EventID"]);
            Assert.AreEqual("mytestinstance1", dr["InstanceName"].ToString());
            StringAssert.Contains((string)dr["Payload"], message);
        }

        [TestMethod]
        public void WhenEventWithNullArgParam()
        {
            var validConnectionString = System.Configuration.ConfigurationManager.ConnectionStrings["valid"].ConnectionString;
            DatabaseHelper.CleanLoggingDB(validConnectionString);
            var logger = MockEventSource.Logger;

            using (ObservableEventListener eventListener = new ObservableEventListener())
            {
                try
                {
                    eventListener.LogToSqlDatabase("mytestinstance1", validConnectionString);
                    eventListener.EnableEvents(logger, EventLevel.LogAlways);
                    logger.Informational(null);
                }
                finally
                {
                    eventListener.DisableEvents(logger);
                }
            }

            var dt = DatabaseHelper.GetLoggedTable(validConnectionString);
            Assert.IsNotNull(dt, "No data logged");
            Assert.IsTrue(dt.Rows.Count > 0);
            var dr = dt.Rows[0];
            Assert.AreEqual((int)EventLevel.Informational, int.Parse(dr["Level"].ToString()));
            Assert.AreEqual(1, (int)dr["EventID"]);
            Assert.AreEqual("mytestinstance1", dr["InstanceName"].ToString());
            StringAssert.Contains((string)dr["Payload"], @"""message"": """"");
        }

        [TestMethod]
        public void WhenErrorEvent()
        {
            var validConnectionString = System.Configuration.ConfigurationManager.ConnectionStrings["valid"].ConnectionString;
            DatabaseHelper.CleanLoggingDB(validConnectionString);
            var logger = MockEventSource.Logger;

            var message = string.Empty;
            using (ObservableEventListener eventListener = new ObservableEventListener())
            {
                try
                {
                    eventListener.LogToSqlDatabase("mytestinstance1", validConnectionString);
                    eventListener.EnableEvents(logger, EventLevel.LogAlways);
                    message = string.Concat("Error " + Guid.NewGuid());
                    logger.Error(message);
                }
                finally
                {
                    eventListener.DisableEvents(logger);
                }
            }

            var dt = DatabaseHelper.GetLoggedTable(validConnectionString);
            Assert.IsNotNull(dt, "No data logged");
            Assert.IsTrue(dt.Rows.Count > 0);
            var dr = dt.Rows[0];
            Assert.AreEqual((int)EventLevel.Error, int.Parse(dr["Level"].ToString()));
            Assert.AreEqual(3, (int)dr["EventID"]);
            StringAssert.Contains((string)dr["Payload"], message);
        }

        [TestMethod]
        public void WhenCriticalEvent()
        {
            var validConnectionString = System.Configuration.ConfigurationManager.ConnectionStrings["valid"].ConnectionString;
            DatabaseHelper.CleanLoggingDB(validConnectionString);
            var logger = MockEventSource.Logger;

            var message = string.Empty;
            using (ObservableEventListener eventListener = new ObservableEventListener())
            {
                try
                {
                    eventListener.LogToSqlDatabase("mytestinstance1", validConnectionString);
                    eventListener.EnableEvents(logger, EventLevel.LogAlways);
                    message = string.Concat("Critical " + Guid.NewGuid());
                    logger.Critical(message);
                }
                finally
                {
                    eventListener.DisableEvents(logger);
                }
            }

            var dt = DatabaseHelper.GetLoggedTable(validConnectionString);
            Assert.IsNotNull(dt, "No data logged");
            Assert.IsTrue(dt.Rows.Count > 0);
            var dr = dt.Rows[0];
            Assert.AreEqual((int)EventLevel.Critical, int.Parse(dr["Level"].ToString()));
            Assert.AreEqual(2, (int)dr["EventID"]);
            StringAssert.Contains((string)dr["Payload"], message);
        }

        [TestMethod]
        public void WhenInformationalEventWithOpCode()
        {
            var validConnectionString = System.Configuration.ConfigurationManager.ConnectionStrings["valid"].ConnectionString;
            DatabaseHelper.CleanLoggingDB(validConnectionString);
            var logger = MockEventSource.Logger;

            var message = string.Empty;
            using (ObservableEventListener eventListener = new ObservableEventListener())
            {
                try
                {
                    eventListener.LogToSqlDatabase("mytestinstance1", validConnectionString);
                    eventListener.EnableEvents(logger, EventLevel.LogAlways);
                    message = string.Concat("Message ", Guid.NewGuid());
                    logger.WriteWithOpCode(message);
                }
                finally
                {
                    eventListener.DisableEvents(logger);
                }
            }

            var dt = DatabaseHelper.GetLoggedTable(validConnectionString);
            Assert.IsNotNull(dt, "No data logged");
            Assert.IsTrue(dt.Rows.Count > 0);
            var dr = dt.Rows[0];
            Assert.AreEqual(7, (int)dr["OpCode"]);
        }

        [TestMethod]
        public void WhenConcurrentEvents()
        {
            var validConnectionString = System.Configuration.ConfigurationManager.ConnectionStrings["valid"].ConnectionString;
            DatabaseHelper.CleanLoggingDB(validConnectionString);
            var logger = MockEventSource.Logger;

            using (ObservableEventListener eventListener = new ObservableEventListener())
            {
                try
                {
                    eventListener.LogToSqlDatabase("mytestinstance1", validConnectionString);
                    eventListener.EnableEvents(logger, EventLevel.LogAlways);
                    var logTaskList = new List<Task>();
                    for (int i = 0; i < 9; i++)
                    {
                        var messageNumber = i;
                        logTaskList.Add(Task.Run(() => logger.Informational(messageNumber + "Message ")));
                    }

                    Task.WaitAll(logTaskList.ToArray(), TimeSpan.FromSeconds(10));
                }
                finally
                {
                    eventListener.DisableEvents(logger);
                }
            }

            var dt = DatabaseHelper.GetLoggedTable(validConnectionString, 9);
            Assert.IsNotNull(dt, "No data logged");
            Assert.AreEqual(dt.Rows.Count, 9);
            DataRow[] result = dt.Select("Payload like '%Message%' ");
            Assert.AreEqual(result.Length, 9);
            for (int n = 0; n < 9; n++)
            {
                DataRow[] singleResult = dt.Select(string.Format("Payload like '%{0}Message%' ", n));
                Assert.AreEqual(1, singleResult.Length);
            }
        }

        [TestMethod]
        public void WhenConcurrentEventsFromMultipleSources()
        {
            var validConnectionString = System.Configuration.ConfigurationManager.ConnectionStrings["valid"].ConnectionString;
            DatabaseHelper.CleanLoggingDB(validConnectionString);
            var logger = MockEventSource.Logger;
            var loggerNoTask = MockEventSourceNoTask.Logger;

            using (ObservableEventListener eventListener = new ObservableEventListener())
            {
                try
                {
                    eventListener.LogToSqlDatabase("mytestinstance1", validConnectionString);
                    eventListener.EnableEvents(logger, EventLevel.Critical);
                    eventListener.EnableEvents(loggerNoTask, EventLevel.Informational);
                    string criticalMessage = string.Concat("CriticalMessage");
                    string infoMessage = string.Concat("InfoMessage");
                    var logTaskList = new List<Task>();
                    for (int i = 0; i < 9; i++)
                    {
                        var messageNumber = i;
                        logTaskList.Add(Task.Run(() =>
                        {
                            logger.Critical(messageNumber + criticalMessage);
                            loggerNoTask.Informational(messageNumber + infoMessage);
                        }));
                    }

                    Task.WaitAll(logTaskList.ToArray(), TimeSpan.FromSeconds(10));
                }
                finally
                {
                    eventListener.DisableEvents(logger);
                }
            }

            var dt = DatabaseHelper.GetLoggedTable(validConnectionString, 18);
            Assert.IsNotNull(dt, "No data logged");
            Assert.AreEqual(18, dt.Rows.Count);
            DataRow[] result1 = dt.Select("Payload like '%CriticalMessage%' ");
            Assert.AreEqual(result1.Length, 9);
            for (int n = 0; n < 9; n++)
            {
                DataRow[] singleResult = dt.Select(string.Format("Payload like '%{0}CriticalMessage%' ", n));
                Assert.AreEqual(singleResult.Length, 1);
            }

            result1 = dt.Select("Payload like '%InfoMessage%' ");
            Assert.AreEqual(result1.Length, 9);
            for (int n = 0; n < 9; n++)
            {
                DataRow[] singleResult = dt.Select(string.Format("Payload like '%{0}InfoMessage%' ", n));
                Assert.AreEqual(singleResult.Length, 1);
            }
        }

        [TestMethod]
        public void WhenMultipleEventsUsingSingleListener()
        {
            var validConnectionString = System.Configuration.ConfigurationManager.ConnectionStrings["valid"].ConnectionString;
            DatabaseHelper.CleanLoggingDB(validConnectionString);
            var logger = MockEventSource.Logger;

            using (ObservableEventListener eventListener = new ObservableEventListener())
            {
                try
                {
                    eventListener.LogToSqlDatabase("mytestinstance1", validConnectionString);
                    eventListener.EnableEvents(logger, EventLevel.LogAlways);
                    for (int n = 0; n < 300; n++)
                    {
                        logger.Informational("logging multiple messages " + n.ToString());
                    } 
                }
                finally
                {
                    eventListener.DisableEvents(logger);
                }
            }

            var dt = DatabaseHelper.GetLoggedTable(validConnectionString, 300);
            Assert.IsNotNull(dt, "No data logged");
            Assert.AreEqual(300, dt.Rows.Count);
            StringAssert.Contains(dt.Rows[0]["payload"].ToString(), "logging multiple messages 0");
            StringAssert.Contains(dt.Rows[299]["payload"].ToString(), "logging multiple messages 299");
        }

        [TestMethod]
        public void WhenOneSourceTwoListeners()
        {
            var validConnectionString = System.Configuration.ConfigurationManager.ConnectionStrings["valid"].ConnectionString;
            DatabaseHelper.CleanLoggingDB(validConnectionString);
            var logger = MockEventSource.Logger;

            string errorMessage = string.Concat("Error ", Guid.NewGuid());
            string infoMessage = string.Concat("Message", Guid.NewGuid());
            using (ObservableEventListener eventListener = new ObservableEventListener())
            using (ObservableEventListener eventListener2 = new ObservableEventListener())
            {
                try
                {
                    eventListener.LogToSqlDatabase("mytestinstance1", validConnectionString);
                    eventListener2.LogToSqlDatabase("mytestinstance1", validConnectionString);
                    eventListener2.EnableEvents(logger, EventLevel.Error);
                    eventListener.EnableEvents(logger, EventLevel.Informational);
                    logger.Informational(infoMessage);
                    logger.Error(errorMessage);
                }
                finally
                {
                    eventListener.DisableEvents(logger);
                    eventListener2.DisableEvents(logger);
                }
            }

            var dt = DatabaseHelper.GetLoggedTable(validConnectionString, 3);
            Assert.IsNotNull(dt, "No data logged");
            Assert.AreEqual(3, dt.Rows.Count);
        }

        [TestMethod]
        public void WhenOneListenerTwoSources()
        {
            var validConnectionString = System.Configuration.ConfigurationManager.ConnectionStrings["valid"].ConnectionString;
            DatabaseHelper.CleanLoggingDB(validConnectionString);
            var logger = MockEventSource.Logger;
            var logger2 = MockEventSource2.Logger;

            using (ObservableEventListener eventListener = new ObservableEventListener())
            {
                try
                {
                    eventListener.LogToSqlDatabase("mytestinstance1", validConnectionString);
                    string message = string.Concat("Message ", Guid.NewGuid());
                    string errorMessage = string.Concat("Error ", Guid.NewGuid());
                    eventListener.EnableEvents(logger, EventLevel.LogAlways);
                    eventListener.EnableEvents(logger2, EventLevel.LogAlways);
                    logger.Informational(message);
                    logger2.Error(errorMessage);
                }
                finally
                {
                    eventListener.DisableEvents(logger);
                    eventListener.DisableEvents(logger2);
                }
            }

            var dt = DatabaseHelper.GetLoggedTable(validConnectionString, 2);
            Assert.IsNotNull(dt, "No data logged");
            Assert.IsTrue(dt.Rows.Count == 2);
            var dr = dt.Rows[0];
            var dr2 = dt.Rows[1];
            Assert.AreNotEqual(dr2["Level"], dr["Level"]);
            StringAssert.Contains(dr2["Level"].ToString() + "|" + dr["Level"].ToString(), ((int)EventLevel.Error).ToString());
            StringAssert.Contains(dr2["Level"].ToString() + "|" + dr["Level"].ToString(), ((int)EventLevel.Informational).ToString());
            Assert.AreNotEqual((Guid)dr["ProviderID"], (Guid)dr2["ProviderID"]);
        }

        [TestMethod]
        public void WhenEventWithNoTaskInSchema()
        {
            var validConnectionString = System.Configuration.ConfigurationManager.ConnectionStrings["valid"].ConnectionString;
            DatabaseHelper.CleanLoggingDB(validConnectionString);
            var logger = MockEventSourceNoTask.Logger;

            using (ObservableEventListener eventListener = new ObservableEventListener())
            {
                try
                {
                    eventListener.LogToSqlDatabase("mytestinstance1", validConnectionString);
                    eventListener.EnableEvents(logger, EventLevel.LogAlways);
                    logger.NoTaskSpecfied1(1, 2, 3);
                 }
                finally
                {
                    eventListener.DisableEvents(logger);
                }
            }

            var dt = DatabaseHelper.GetLoggedTable(validConnectionString);
            Assert.IsNotNull(dt, "No data logged");
            Assert.IsTrue(dt.Rows.Count > 0);
            var dr = dt.Rows[0];
            Assert.AreEqual(0, (int)dr["Task"]);
        }

        [TestMethod]
        public void WhenEventWithNoOpCodeNoKeywordsNoVersionNoMessage()
        {
            var validConnectionString = System.Configuration.ConfigurationManager.ConnectionStrings["valid"].ConnectionString;
            DatabaseHelper.CleanLoggingDB(validConnectionString);
            var logger = MockEventSourceNoTask.Logger;

            using (ObservableEventListener eventListener = new ObservableEventListener())
            {
                try
                {
                    eventListener.LogToSqlDatabase("mytestinstance1", validConnectionString);
                    eventListener.EnableEvents(logger, EventLevel.LogAlways);
                    logger.NoTaskNoOpCode1(1, 2, 3);
                 }
                finally
                {
                    eventListener.DisableEvents(logger);
                }
            }

            var dt = DatabaseHelper.GetLoggedTable(validConnectionString);
            Assert.IsNotNull(dt, "No data logged");
            Assert.IsTrue(dt.Rows.Count > 0);
            var dr = dt.Rows[0];
            Assert.AreEqual(0, (int)dr["OpCode"]);
            Assert.AreEqual(0, (long)dr["EventKeywords"]);
            Assert.AreEqual(0, (int)dr["Version"]);
            Assert.AreEqual(String.Empty, dr["FormattedMessage"].ToString());
        }

        [TestMethod]
        public void WhenActivityId()
        {
            var validConnectionString = System.Configuration.ConfigurationManager.ConnectionStrings["valid"].ConnectionString;
            DatabaseHelper.CleanLoggingDB(validConnectionString);
            var logger = MockEventSource.Logger;

            var activityId = Guid.NewGuid();
            var previousActivityId = Guid.Empty;
            var message = string.Empty;
            using (ObservableEventListener eventListener = new ObservableEventListener())
            {
                try
                {
                    eventListener.LogToSqlDatabase("mytestinstance1", validConnectionString);
                    eventListener.EnableEvents(logger, EventLevel.LogAlways);

                    EventSource.SetCurrentThreadActivityId(activityId, out previousActivityId);
                    message = string.Concat("Message ", Guid.NewGuid());
                    logger.Informational(message);
                }
                finally
                {
                    eventListener.DisableEvents(logger);
                    EventSource.SetCurrentThreadActivityId(previousActivityId);
                }
            }

            var dt = DatabaseHelper.GetLoggedTable(validConnectionString);
            Assert.IsNotNull(dt, "No data logged");
            Assert.IsTrue(dt.Rows.Count > 0);
            var dr = dt.Rows[0];
            Assert.AreEqual((int)EventLevel.Informational, int.Parse(dr["Level"].ToString()));
            Assert.AreEqual(1, (int)dr["EventID"]);
            Assert.AreEqual("mytestinstance1", dr["InstanceName"].ToString());
            StringAssert.Contains((string)dr["Payload"], message);
            Assert.AreEqual(activityId, (Guid)dr["ActivityId"]);
            Assert.AreEqual(Guid.Empty, (Guid)dr["RelatedActivityId"]);
        }

        [TestMethod]
        public void WhenActivityIdAndRelatedActivityId()
        {
            var validConnectionString = System.Configuration.ConfigurationManager.ConnectionStrings["valid"].ConnectionString;
            DatabaseHelper.CleanLoggingDB(validConnectionString);
            var logger = MockEventSource.Logger;

            var activityId = Guid.NewGuid();
            var relatedActivityId = Guid.NewGuid();
            var previousActivityId = Guid.Empty;
            var message = string.Empty;
            using (ObservableEventListener eventListener = new ObservableEventListener())
            {
                try
                {
                    eventListener.LogToSqlDatabase("mytestinstance1", validConnectionString);
                    eventListener.EnableEvents(logger, EventLevel.LogAlways);

                    EventSource.SetCurrentThreadActivityId(activityId, out previousActivityId);
                    message = string.Concat("Message ", Guid.NewGuid());
                    logger.InformationalWithRelatedActivityId(message, relatedActivityId);
                }
                finally
                {
                    eventListener.DisableEvents(logger);
                    EventSource.SetCurrentThreadActivityId(previousActivityId);
                }
            }

            var dt = DatabaseHelper.GetLoggedTable(validConnectionString);
            Assert.IsNotNull(dt, "No data logged");
            Assert.IsTrue(dt.Rows.Count > 0);
            var dr = dt.Rows[0];
            Assert.AreEqual((int)EventLevel.Informational, int.Parse(dr["Level"].ToString()));
            Assert.AreEqual(14, (int)dr["EventID"]);
            Assert.AreEqual("mytestinstance1", dr["InstanceName"].ToString());
            StringAssert.Contains((string)dr["Payload"], message);
            Assert.AreEqual(activityId, (Guid)dr["ActivityId"]);
            Assert.AreEqual(relatedActivityId, (Guid)dr["RelatedActivityId"]);
        }

        [TestMethod]
        public void WhenMaxLengthPayload()
        {
            var validConnectionString = System.Configuration.ConfigurationManager.ConnectionStrings["valid"].ConnectionString;
            DatabaseHelper.CleanLoggingDB(validConnectionString);
            var logger = MockEventSourceNoTask.Logger;

            string largeMessage = new string('*', 3900);
            using (ObservableEventListener eventListener = new ObservableEventListener())
            {
                try
                {
                    eventListener.LogToSqlDatabase("mytestinstance1", validConnectionString);
                    eventListener.EnableEvents(logger, EventLevel.LogAlways);
                    logger.MaxValues(largeMessage, long.MaxValue, int.MaxValue);
                 }
                finally
                {
                    eventListener.DisableEvents(logger);
                }
            }

            var dt = DatabaseHelper.GetLoggedTable(validConnectionString);
            Assert.IsNotNull(dt, "No data logged");
            Assert.IsTrue(dt.Rows.Count > 0);
            var dr = dt.Rows[0];
            Assert.AreEqual((int)EventLevel.Informational, int.Parse(dr["Level"].ToString()));
            StringAssert.Contains((string)dr["Payload"], largeMessage);
            StringAssert.Contains((string)dr["Payload"], long.MaxValue.ToString());
            StringAssert.Contains((string)dr["Payload"], int.MaxValue.ToString());
        }

        [TestMethod]
        public void WhenEventWithDifferentTypesPayload()
        {
            var validConnectionString = System.Configuration.ConfigurationManager.ConnectionStrings["valid"].ConnectionString;
            DatabaseHelper.CleanLoggingDB(validConnectionString);
            var logger = MockEventSourceNoTask.Logger;

            using (ObservableEventListener eventListener = new ObservableEventListener())
            {
                try
                {
                    eventListener.LogToSqlDatabase("mytestinstance1", validConnectionString);
                    eventListener.EnableEvents(logger, EventLevel.LogAlways);
                    logger.DifferentTypes("testString", 500000);
                 }
                finally
                {
                    eventListener.DisableEvents(logger);
                }
            }

            var dt = DatabaseHelper.GetLoggedTable(validConnectionString);
            Assert.IsNotNull(dt, "No data logged");
            Assert.IsTrue(dt.Rows.Count > 0);
            var dr = dt.Rows[0];
            StringAssert.Contains((string)dr["Payload"], @"""strArg"": ""testString""");
            StringAssert.Contains((string)dr["Payload"], @"""longArg"": 500000");
        }

        [TestMethod]
        public void WhenEventWithPayloadWithSupportedTypes()
        {
            var validConnectionString = System.Configuration.ConfigurationManager.ConnectionStrings["valid"].ConnectionString;
            DatabaseHelper.CleanLoggingDB(validConnectionString);
            var logger = MockEventSourceNoTask.Logger;

            var guidArg = Guid.NewGuid();
            using (ObservableEventListener eventListener = new ObservableEventListener())
            {
                try
                {
                    eventListener.LogToSqlDatabase("mytestinstance1", validConnectionString);
                    eventListener.EnableEvents(logger, EventLevel.LogAlways);
                    logger.AllSupportedTypes(Int16.MinValue, Int32.MaxValue, Int64.MaxValue, 10 / 3, TestEnum.value1, guidArg);
                 }
                finally
                {
                    eventListener.DisableEvents(logger);
                }
            }

            var dt = DatabaseHelper.GetLoggedTable(validConnectionString);
            Assert.IsNotNull(dt, "No data logged");
            Assert.IsTrue(dt.Rows.Count > 0);
            var dr = dt.Rows[0];
            StringAssert.Contains((string)dr["Payload"], @"""srtArg"": " + Int16.MinValue.ToString());
            StringAssert.Contains((string)dr["Payload"], @"""intArg"": " + Int32.MaxValue.ToString());
            StringAssert.Contains((string)dr["Payload"], @"""lngArg"": " + Int64.MaxValue.ToString());
            StringAssert.Contains((string)dr["Payload"], @"""fltArg"": 3.0");
            StringAssert.Contains((string)dr["Payload"], @"""enumArg"": " + ((int)TestEnum.value1).ToString());
            StringAssert.Contains((string)dr["Payload"], @"""guidArg"": """ + guidArg.ToString());
        }

        [TestMethod]
        public void WhenEventWithPayloadDifferentTypesAndNull()
        {
            var validConnectionString = System.Configuration.ConfigurationManager.ConnectionStrings["valid"].ConnectionString;
            DatabaseHelper.CleanLoggingDB(validConnectionString);
            var logger = MockEventSourceNoTask.Logger;

            using (ObservableEventListener eventListener = new ObservableEventListener())
            {
                try
                {
                    eventListener.LogToSqlDatabase("mytestinstance1", validConnectionString);
                    eventListener.EnableEvents(logger, EventLevel.LogAlways);
                    logger.DifferentTypes(null, 500000);
                 }
                finally
                {
                    eventListener.DisableEvents(logger);
                }
            }

            var dt = DatabaseHelper.GetLoggedTable(validConnectionString);
            Assert.IsNotNull(dt, "No data logged");
            Assert.IsTrue(dt.Rows.Count > 0);
            var dr = dt.Rows[0];
            StringAssert.Contains((string)dr["Payload"], @"""strArg"": """"");
            StringAssert.Contains((string)dr["Payload"], @"""longArg"": 500000");
        }

        [TestMethod]
        public void WhenRawMessageAndFormattedMessage()
        {
            var validConnectionString = System.Configuration.ConfigurationManager.ConnectionStrings["valid"].ConnectionString;
            DatabaseHelper.CleanLoggingDB(validConnectionString);
            var logger = MockEventSourceNoTask.Logger;

            using (ObservableEventListener eventListener = new ObservableEventListener())
            {
                try
                {
                    eventListener.LogToSqlDatabase("mytestinstance1", validConnectionString);
                    eventListener.EnableEvents(logger, EventLevel.LogAlways);
                    logger.Informational("testing");
                }
                finally
                {
                    eventListener.DisableEvents(logger);
                }
            }

            var dt = DatabaseHelper.GetLoggedTable(validConnectionString);
            Assert.IsNotNull(dt, "No data logged");
            Assert.IsTrue(dt.Rows.Count > 0);
            var dr = dt.Rows[0];
            Assert.AreEqual("message param", dr["FormattedMessage"]);
        }

        [TestMethod]
        public void WhenEventWithEnumsInPayload()
        {
            var validConnectionString = System.Configuration.ConfigurationManager.ConnectionStrings["valid"].ConnectionString;
            DatabaseHelper.CleanLoggingDB(validConnectionString);
            var logger = MockEventSourceInProcEnum.Logger;

            using (ObservableEventListener eventListener = new ObservableEventListener())
            {
                try
                {
                    eventListener.LogToSqlDatabase("mytestinstance1", validConnectionString);
                    eventListener.EnableEvents(logger, EventLevel.LogAlways);
                    string message = string.Concat("Message ", Guid.NewGuid());
                    logger.SendEnumsEvent15(MockEventSourceInProcEnum.MyColor.Blue, MockEventSourceInProcEnum.MyFlags.Flag3);
                }
                finally
                {
                    eventListener.DisableEvents(logger);
                }
            }

            var dt = DatabaseHelper.GetLoggedTable(validConnectionString);
            Assert.IsNotNull(dt, "No data logged");
            Assert.IsTrue(dt.Rows.Count > 0);
            var dr = dt.Rows[0];
            Assert.AreEqual((int)EventLevel.Informational, int.Parse(dr["Level"].ToString()));
            Assert.AreEqual(2, (int)dr["EventID"]);
            StringAssert.Contains((string)dr["Payload"], @"a"": 1");
            StringAssert.Contains((string)dr["Payload"], @"b"": 4");
        }

        [TestMethod]
        public void WhenAmbientTransactionIsDisposed()
        {
            var validConnectionString = System.Configuration.ConfigurationManager.ConnectionStrings["valid"].ConnectionString;
            DatabaseHelper.CleanLoggingDB(validConnectionString);
            var logger = MockEventSource.Logger;

            using (ObservableEventListener eventListener = new ObservableEventListener())
            {
                try
                {
                    eventListener.LogToSqlDatabase("mytestinstance1", validConnectionString);
                    string message = string.Concat("Message ", Guid.NewGuid());
                    eventListener.EnableEvents(logger, EventLevel.LogAlways);

                    TransactionScope tran = new TransactionScope();
                    logger.Informational(message);
                    logger.Error(message);
                    tran.Dispose();
                }
                finally
                {
                    eventListener.DisableEvents(logger);
                }
            }

            var dt = DatabaseHelper.GetLoggedTable(validConnectionString);
            Assert.IsNotNull(dt, "No data logged");
            Assert.IsTrue(dt.Rows.Count == 2);
        }

        [TestMethod]
        public void WhenSingleLineTextFormatter()
        {
            var validConnectionString = System.Configuration.ConfigurationManager.ConnectionStrings["valid"].ConnectionString;
            DatabaseHelper.CleanLoggingDB(validConnectionString);
            var logger = MockEventSource.Logger;

            string strGuid = Guid.NewGuid().ToString();
            using (ObservableEventListener eventListener = new ObservableEventListener())
            {
                try
                {
                    eventListener.LogToSqlDatabase("mytestinstance1", validConnectionString);
                    eventListener.EnableEvents(logger, EventLevel.Error);
                    logger.Informational(string.Concat("Informational ", strGuid));
                    logger.Error(string.Concat("Error ", strGuid));
                    logger.Critical(string.Concat("Critical ", strGuid));
                }
                finally
                {
                    eventListener.DisableEvents(logger);
                }
            }

            var dt = DatabaseHelper.GetLoggedTable(validConnectionString, 5);
            Assert.IsNotNull(dt, "No data logged");
            Assert.AreEqual(dt.Rows.Count, 2);
            var errorRow = dt.Rows[0];
            var criticalRow = dt.Rows[1];
            Assert.AreEqual((int)EventLevel.Error, int.Parse(errorRow["Level"].ToString()));
            Assert.AreEqual((int)EventLevel.Critical, int.Parse(criticalRow["Level"].ToString()));
            Assert.AreEqual("{\r\n  \"message\": \"Error " + strGuid + "\"\r\n}", (string)errorRow["Payload"]);
            Assert.AreEqual("{\r\n  \"message\": \"Critical " + strGuid + "\"\r\n}", (string)criticalRow["Payload"]);
        }

        [TestMethod]
        public void WhenWrongTableNameExceptionsAreRoutedToErrorEventSource()
        {
            var validConnectionString = System.Configuration.ConfigurationManager.ConnectionStrings["valid"].ConnectionString;
            DatabaseHelper.CleanLoggingDB(validConnectionString);
            var logger = MockEventSource.Logger;

            using (ObservableEventListener eventListener = new ObservableEventListener())
            {
                try
                {
                    eventListener.LogToSqlDatabase("mytestinstance1", validConnectionString, "WrongTable", bufferingCount: 200, bufferingInterval: TimeSpan.FromSeconds(1));
                    eventListener.EnableEvents(logger, EventLevel.LogAlways);
                    using (var collectErrorsListener = new InMemoryEventListener(true))
                    {
                        try
                        {
                            collectErrorsListener.EnableEvents(SemanticLoggingEventSource.Log, EventLevel.Warning, Keywords.All);
                            for (int n = 0; n < 200; n++)
                            {
                                logger.Informational("Message: " + n);
                            }

                            collectErrorsListener.WaitEvents.Wait(2000);
                            StringAssert.Contains(collectErrorsListener.ToString(), "Cannot access destination table 'WrongTable'.");
                        }
                        finally
                        {
                            collectErrorsListener.DisableEvents(SemanticLoggingEventSource.Log);
                        }
                    }
                }
                finally
                {
                    eventListener.DisableEvents(logger);
                }
            }

            var rowCount = DatabaseHelper.GetRowCount(validConnectionString);
            Assert.AreEqual(0, rowCount);
        }

        [TestMethod]
        public void WhenWrongDbExceptionsAreRoutedToErrorEventSource()
        {
            var validConnectionString = System.Configuration.ConfigurationManager.ConnectionStrings["valid"].ConnectionString;
            DatabaseHelper.CleanLoggingDB(validConnectionString);
            var logger = MockEventSource.Logger;

            var invalidConnectionString = System.Configuration.ConfigurationManager.ConnectionStrings["invalid"].ConnectionString;
            using (ObservableEventListener eventListener = new ObservableEventListener())
            {
                try
                {
                    eventListener.LogToSqlDatabase("mytestinstance1", invalidConnectionString, bufferingCount: 1);
                    eventListener.EnableEvents(logger, EventLevel.LogAlways);
                    using (var collectErrorsListener = new InMemoryEventListener(true))
                    {
                        try
                        {
                            collectErrorsListener.EnableEvents(SemanticLoggingEventSource.Log, EventLevel.Warning, Keywords.All);
                            logger.Informational("Message 1");

                            collectErrorsListener.WaitEvents.Wait(1000);
                            StringAssert.Contains(collectErrorsListener.ToString(), @"Cannot open database ""Invalid"" requested by the login. The login failed.");
                        }
                        finally
                        {
                            collectErrorsListener.DisableEvents(SemanticLoggingEventSource.Log);
                        }
                    }
                }
                finally
                {
                    eventListener.DisableEvents(logger);
                }
            }

            var dt = DatabaseHelper.GetLoggedTable(validConnectionString);
            Assert.IsTrue(dt.Rows.Count == 0);
        }

        [TestMethod]
        public void WhenEnablingKeywordsAll()
        {
            var validConnectionString = System.Configuration.ConfigurationManager.ConnectionStrings["valid"].ConnectionString;
            DatabaseHelper.CleanLoggingDB(validConnectionString);
            var logger = MockEventSource.Logger;

            string strGuid = Guid.NewGuid().ToString();
            using (ObservableEventListener eventListener = new ObservableEventListener())
            {
                try
                {
                    eventListener.LogToSqlDatabase("mytestinstance1", validConnectionString);
                    eventListener.EnableEvents(logger, EventLevel.LogAlways, Keywords.All);
                    logger.ErrorWithKeywordDiagnostic(string.Concat("ErrorWithKeywordDiagnostic ", strGuid));
                    logger.CriticalWithKeywordPage(string.Concat("CriticalWithKeywordPage ", strGuid));
                }
                finally
                {
                    eventListener.DisableEvents(logger);
                }
            }

            var dt = DatabaseHelper.GetLoggedTable(validConnectionString, 5);
            Assert.IsNotNull(dt, "No data logged");
            Assert.AreEqual(2, dt.Rows.Count);
            var errorRow = dt.Rows[0];
            var criticalRow = dt.Rows[1];
            Assert.AreEqual((int)EventLevel.Error, int.Parse(errorRow["Level"].ToString()));
            Assert.AreEqual((int)EventLevel.Critical, int.Parse(criticalRow["Level"].ToString()));
            Assert.AreEqual("4", (string)errorRow["EventKeywords"].ToString());
            Assert.AreEqual("1", (string)criticalRow["EventKeywords"].ToString());
        }

        [TestMethod]
        public void WhenNotSpecifyingKeywordsWhileEnabling()
        {
            var validConnectionString = System.Configuration.ConfigurationManager.ConnectionStrings["valid"].ConnectionString;
            DatabaseHelper.CleanLoggingDB(validConnectionString);
            var logger = MockEventSource.Logger;

            string strGuid = Guid.NewGuid().ToString();
            using (ObservableEventListener eventListener = new ObservableEventListener())
            {
                try
                {
                    eventListener.LogToSqlDatabase("mytestinstance1", validConnectionString);
                    eventListener.EnableEvents(logger, EventLevel.LogAlways);
                    logger.ErrorWithKeywordDiagnostic(string.Concat("ErrorWithKeywordDiagnostic ", strGuid));
                    logger.CriticalWithKeywordPage(string.Concat("CriticalWithKeywordPage ", strGuid));
                }
                finally
                {
                    eventListener.DisableEvents(logger);
                }
            }

            Assert.AreEqual(0, DatabaseHelper.GetRowCount(validConnectionString));
        }

        [TestMethod]
        public void WhenUnEscalatedTransactionRollsBack()
        {
            var validConnectionString = System.Configuration.ConfigurationManager.ConnectionStrings["valid"].ConnectionString;
            DatabaseHelper.CleanLoggingDB(validConnectionString);
            var logger = MockEventSource.Logger;

            var transaction = new TransactionScope();
            try
            {
                using (ObservableEventListener eventListener = new ObservableEventListener())
                {
                    try
                    {
                        eventListener.LogToSqlDatabase("mytestinstance1", validConnectionString);
                        eventListener.EnableEvents(logger, EventLevel.Verbose);
                        logger.Warning("warning1");
                        logger.Informational("info1");
                    }
                    finally
                    {
                        eventListener.DisableEvents(logger);
                    }

                    throw new Exception();
                }
            }
            catch { }
            finally
            {
                transaction.Dispose();
            }

            var dt = DatabaseHelper.GetLoggedTable(validConnectionString);
            Assert.AreEqual(2, dt.Rows.Count);
        }

        [TestMethod]
        public void WhenUnEscalatedTransactionSucceeds()
        {
            var validConnectionString = System.Configuration.ConfigurationManager.ConnectionStrings["valid"].ConnectionString;
            DatabaseHelper.CleanLoggingDB(validConnectionString);
            var logger = MockEventSource.Logger;

            using (var transaction = new System.Transactions.TransactionScope())
            {
                using (ObservableEventListener eventListener = new ObservableEventListener())
                {
                    try
                    {
                        eventListener.LogToSqlDatabase("mytestinstance1", validConnectionString);
                        eventListener.EnableEvents(logger, EventLevel.Verbose);
                        logger.Warning("warning1");
                        logger.Informational("info1");
                    }
                    finally
                    {
                        eventListener.DisableEvents(logger);
                    }

                    transaction.Complete();
                }
            }

            var dt = DatabaseHelper.GetLoggedTable(validConnectionString);
            Assert.AreEqual(2, dt.Rows.Count);
        }

        [TestMethod]
        public void WhenEventWithMessageInAttribute()
        {
            var validConnectionString = System.Configuration.ConfigurationManager.ConnectionStrings["valid"].ConnectionString;
            DatabaseHelper.CleanLoggingDB(validConnectionString);
            var logger = TestEventSourceNoAttributes.Logger;

            using (ObservableEventListener eventListener = new ObservableEventListener())
            {
                try
                {
                    eventListener.LogToSqlDatabase("mytestinstance1", validConnectionString);
                    eventListener.EnableEvents(logger, EventLevel.LogAlways);
                    logger.ObjectArrayEvent4(10, "stringarg1", 20, "stringarg3", 30);
                }
                finally
                {
                    eventListener.DisableEvents(logger);
                }
            }

            var dt = DatabaseHelper.GetLoggedTable(validConnectionString);
            Assert.IsNotNull(dt, "No data logged");
            Assert.IsTrue(dt.Rows.Count > 0);
            var dr = dt.Rows[0];
            StringAssert.Contains((string)dr["FormattedMessage"], "Check if it is logged");
            StringAssert.Contains((string)dr["Payload"], "{\r\n  \"arg0\": 10,\r\n  \"arg1\": \"stringarg1\",\r\n  \"arg2\": 20,\r\n  \"arg3\": \"stringarg3\",\r\n  \"arg4\": 30\r\n}");
        }

        [TestMethod]
        public void WhenConcurrentEventsToMultipleListenersWithSameSource()
        {
            var validConnectionString = System.Configuration.ConfigurationManager.ConnectionStrings["valid"].ConnectionString;
            DatabaseHelper.CleanLoggingDB(validConnectionString);
            var logger = TestEventSource.Logger;

            using (ObservableEventListener eventListener = new ObservableEventListener())
            using (ObservableEventListener eventListener1 = new ObservableEventListener())
            using (ObservableEventListener eventListener2 = new ObservableEventListener())
            {
                try
                {
                    eventListener.LogToSqlDatabase("WaitFor_BufferingInterval", validConnectionString, bufferingInterval: TimeSpan.FromSeconds(30));
                    eventListener.LogToSqlDatabase("WaitFor_BufferingInterval", validConnectionString);
                    eventListener.LogToSqlDatabase("WaitFor_BufferingInterval", validConnectionString);
                    eventListener.EnableEvents(logger, EventLevel.LogAlways);
                    eventListener1.EnableEvents(logger, EventLevel.LogAlways);
                    eventListener2.EnableEvents(logger, EventLevel.LogAlways);
                    var logTaskList = new List<Task>();
                    for (int i = 0; i < 1000; i++)
                    {
                        var messageNumber = i;
                        logTaskList.Add(Task.Run(() =>
                        {
                            logger.Critical(messageNumber + "Critical message");
                        }));
                    }

                    Task.WaitAll(logTaskList.ToArray(), TimeSpan.FromSeconds(10));
                }
                finally
                {
                    eventListener.DisableEvents(logger);
                    eventListener1.DisableEvents(logger);
                    eventListener2.DisableEvents(logger);
                }
            }

            var dt = DatabaseHelper.GetLoggedTable(validConnectionString);
            Assert.AreEqual(3000, dt.Rows.Count);
        }

        [TestMethod]
        public void WhenConcurrentEventsSameListener()
        {
            var validConnectionString = System.Configuration.ConfigurationManager.ConnectionStrings["valid"].ConnectionString;
            DatabaseHelper.CleanLoggingDB(validConnectionString);
            var logger = TestEventSource.Logger;

            using (ObservableEventListener eventListener = new ObservableEventListener())
            {
                try
                {
                    eventListener.LogToSqlDatabase("MultipleThreads", validConnectionString, bufferingInterval: TimeSpan.FromSeconds(20));
                    eventListener.EnableEvents(TestEventSource.Logger, EventLevel.LogAlways);
                    var threads = new System.Threading.Thread[10];
                    for (int i = 0; i < 10; i++)
                    {
                        threads[i] = new System.Threading.Thread(new System.Threading.ThreadStart(() =>
                            {
                                for (int j = 0; j < 50; j++)
                                {
                                    TestEventSource.Logger.Critical("Test MultipleThreads");
                                }
                            }));

                        threads[i].Start();
                    }

                    for (int i = 0; i < 10; i++)
                    {
                        threads[i].Join();
                    }
                }
                finally
                {
                    eventListener.DisableEvents(logger);
                }
            }

            var dt = DatabaseHelper.PollUntilEventsAreWritten(validConnectionString, 500);
            Assert.AreEqual<int>(500, dt.Rows.Count);
        }

        [TestMethod]
        public void WhenDisposeFlushesBufferedEvents()
        {
            var validConnectionString = System.Configuration.ConfigurationManager.ConnectionStrings["valid"].ConnectionString;
            DatabaseHelper.CleanLoggingDB(validConnectionString);
            var logger = MockEventSource.Logger;

            using (ObservableEventListener eventListener = new ObservableEventListener())
            {
                try
                {
                    eventListener.LogToSqlDatabase("mytestinstance1", validConnectionString, bufferingInterval: TimeSpan.FromSeconds(10), bufferingCount: 100);
                    eventListener.EnableEvents(logger, EventLevel.LogAlways);
                    for (int msg = 0; msg < 10; msg++)
                    {
                        logger.Informational("Message " + msg.ToString());
                    }
                }
                finally
                {
                    eventListener.DisableEvents(logger);
                }
            }

            var dt = DatabaseHelper.GetLoggedTable(validConnectionString);
            Assert.IsNotNull(dt, "No data logged");
            Assert.AreEqual(10, dt.Rows.Count);
        }

        [TestMethod]
        public void WhenSmallBufferingInterval()
        {
            var validConnectionString = System.Configuration.ConfigurationManager.ConnectionStrings["valid"].ConnectionString;
            DatabaseHelper.CleanLoggingDB(validConnectionString);
            var logger = TestEventSource.Logger;

            using (ObservableEventListener eventListener = new ObservableEventListener())
            using (ObservableEventListener eventListener1 = new ObservableEventListener())
            using (ObservableEventListener eventListener2 = new ObservableEventListener())
            {
                try
                {
                    var bufferingInterval = TimeSpan.FromSeconds(1);
                    eventListener.LogToSqlDatabase("WithMinBufferingInterval1", validConnectionString, bufferingInterval: bufferingInterval, bufferingCount: 1000);
                    eventListener.LogToSqlDatabase("WithMinBufferingInterval2", validConnectionString, bufferingInterval: bufferingInterval, bufferingCount: 1000);
                    eventListener.LogToSqlDatabase("WithMinBufferingInterval3", validConnectionString, bufferingInterval: bufferingInterval, bufferingCount: 1000);
                    eventListener.EnableEvents(logger, EventLevel.LogAlways);
                    eventListener1.EnableEvents(logger, EventLevel.LogAlways);
                    eventListener2.EnableEvents(logger, EventLevel.LogAlways);
                    var logTaskList = new List<Task>();
                    for (int i = 0; i < 50; i++)
                    {
                        var messageNumber = i;
                        logTaskList.Add(Task.Run(() =>
                        {
                            logger.Critical(messageNumber + "Critical message");
                        }));
                    }

                    Task.WaitAll(logTaskList.ToArray(), TimeSpan.FromSeconds(10));

                    // Wait for the buffer to flush at end of interval
                    Task.Delay(new TimeSpan(0, 0, 0, 1, 800)).Wait();
                    var dt = DatabaseHelper.GetLoggedTable(validConnectionString);
                    Assert.AreEqual(150, dt.Rows.Count);
                    Assert.AreEqual(50, dt.Select("InstanceName = 'WithMinBufferingInterval1'").Count());
                    Assert.AreEqual(50, dt.Select("InstanceName = 'WithMinBufferingInterval2'").Count());
                    Assert.AreEqual(50, dt.Select("InstanceName = 'WithMinBufferingInterval3'").Count());
                }
                finally
                {
                    eventListener.DisableEvents(logger);
                    eventListener1.DisableEvents(logger);
                    eventListener2.DisableEvents(logger);
                }
            }

            // There should not be remaining events flushed during Dispose
            var finalTable = DatabaseHelper.GetLoggedTable(validConnectionString);
            Assert.AreEqual(150, finalTable.Rows.Count);
            Assert.AreEqual(50, finalTable.Select("InstanceName = 'WithMinBufferingInterval1'").Count());
            Assert.AreEqual(50, finalTable.Select("InstanceName = 'WithMinBufferingInterval2'").Count());
            Assert.AreEqual(50, finalTable.Select("InstanceName = 'WithMinBufferingInterval3'").Count());
        }

        [TestMethod]
        public void WhenMinBufferingCount()
        {
            var validConnectionString = System.Configuration.ConfigurationManager.ConnectionStrings["valid"].ConnectionString;
            DatabaseHelper.CleanLoggingDB(validConnectionString);
            var logger = TestEventSource.Logger;

            using (ObservableEventListener eventListener = new ObservableEventListener())
            using (ObservableEventListener eventListener1 = new ObservableEventListener())
            using (ObservableEventListener eventListener2 = new ObservableEventListener())
            {
                eventListener.LogToSqlDatabase("WithMinBufferingCount1", validConnectionString, bufferingInterval: TimeSpan.FromSeconds(10), bufferingCount: 1);
                eventListener.LogToSqlDatabase("WithMinBufferingCount2", validConnectionString, bufferingInterval: TimeSpan.FromSeconds(10), bufferingCount: 1);
                eventListener.LogToSqlDatabase("WithMinBufferingCount3", validConnectionString, bufferingInterval: TimeSpan.FromSeconds(10), bufferingCount: 1);
                eventListener.EnableEvents(TestEventSource.Logger, EventLevel.LogAlways);
                eventListener1.EnableEvents(TestEventSource.Logger, EventLevel.LogAlways);
                eventListener2.EnableEvents(TestEventSource.Logger, EventLevel.LogAlways);

                logger.Critical("Critical message 1");
                logger.Critical("Critical message 2");

                // Wait for the events to be written to the database in each listener
                Task.Delay(TimeSpan.FromSeconds(1)).Wait();
                var dt = DatabaseHelper.GetLoggedTable(validConnectionString);
                Assert.AreEqual(6, dt.Rows.Count);
                Assert.AreEqual(2, dt.Select("InstanceName = 'WithMinBufferingCount1'").Count());
                Assert.AreEqual(2, dt.Select("InstanceName = 'WithMinBufferingCount2'").Count());
                Assert.AreEqual(2, dt.Select("InstanceName = 'WithMinBufferingCount3'").Count());
            }

            // There should not be remaining events flushed during Dispose
            var finalTable = DatabaseHelper.GetLoggedTable(validConnectionString);
            Assert.AreEqual(6, finalTable.Rows.Count);
            Assert.AreEqual(2, finalTable.Select("InstanceName = 'WithMinBufferingCount1'").Count());
            Assert.AreEqual(2, finalTable.Select("InstanceName = 'WithMinBufferingCount2'").Count());
            Assert.AreEqual(2, finalTable.Select("InstanceName = 'WithMinBufferingCount3'").Count());
        }

        [TestMethod]
        public void WhenBufferingIntervalNotReachedBufferingCountExceeded()
        {
            var validConnectionString = System.Configuration.ConfigurationManager.ConnectionStrings["valid"].ConnectionString;
            DatabaseHelper.CleanLoggingDB(validConnectionString);
            var logger = TestEventSource.Logger;

            using (ObservableEventListener eventListener = new ObservableEventListener())
            {
                try
                {
                    var longBufferingInterval = TimeSpan.FromSeconds(30);
                    eventListener.LogToSqlDatabase("WithBuffering_IntervalNotReached_MaxLogReached", validConnectionString, bufferingInterval: longBufferingInterval, bufferingCount: 10);
                    eventListener.EnableEvents(logger, EventLevel.LogAlways);
                    for (int i = 0; i < 15; i++)
                    {
                        logger.Critical(i + "Critical message");
                    }

                    // Wait for the first 10 buffered events to be written to the database in each listener
                    Task.Delay(TimeSpan.FromSeconds(1)).Wait();
                    var dt = DatabaseHelper.GetLoggedTable(validConnectionString);
                    Assert.AreEqual(10, dt.Rows.Count);
                    Assert.AreEqual(10, dt.Select("InstanceName = 'WithBuffering_IntervalNotReached_MaxLogReached'").Count());
                }
                finally
                {
                    eventListener.DisableEvents(logger);
                }
            }

            // The dispose should flush the remaining events
            var finalTable = DatabaseHelper.GetLoggedTable(validConnectionString);
            Assert.AreEqual(15, finalTable.Rows.Count);
            Assert.AreEqual(15, finalTable.Select("InstanceName = 'WithBuffering_IntervalNotReached_MaxLogReached'").Count());
        }

        [TestMethod]
        public void WhenBufferIntervalIsReachedButBufferingCountNotExceeded()
        {
            var validConnectionString = System.Configuration.ConfigurationManager.ConnectionStrings["valid"].ConnectionString;
            DatabaseHelper.CleanLoggingDB(validConnectionString);
            var logger = TestEventSource.Logger;

            using (ObservableEventListener eventListener = new ObservableEventListener())
            {
                try
                {
                    var bufferingInterval = TimeSpan.FromSeconds(4);
                    eventListener.LogToSqlDatabase("WithMinBufferingInterval", validConnectionString, bufferingInterval: bufferingInterval, bufferingCount: 1000);
                    eventListener.EnableEvents(logger, EventLevel.LogAlways);
                    for (int i = 0; i < 50; i++)
                    {
                        logger.Critical(i + "Critical message");
                    }

                    // Before buffer interval is exceeded there should be no events written
                    Assert.AreEqual(0, DatabaseHelper.GetRowCount(validConnectionString));

                    // Wait for the buffer to flush at end of interval
                    Task.Delay(bufferingInterval).Wait();

                    var dt = DatabaseHelper.GetLoggedTable(validConnectionString);
                    Assert.AreEqual(50, dt.Rows.Count);
                    Assert.AreEqual(50, dt.Select("InstanceName = 'WithMinBufferingInterval'").Count());
                }
                finally
                {
                    eventListener.DisableEvents(logger);
                }
            }

            // There should not be remaining events flushed during Dispose
            var finalTable = DatabaseHelper.GetLoggedTable(validConnectionString);
            Assert.AreEqual(50, finalTable.Rows.Count);
            Assert.AreEqual(50, finalTable.Select("InstanceName = 'WithMinBufferingInterval'").Count());
        }

        [TestMethod]
        public void WhenBufferingIntervalAndBufferingCountIsExceeded()
        {
            var validConnectionString = System.Configuration.ConfigurationManager.ConnectionStrings["valid"].ConnectionString;
            DatabaseHelper.CleanLoggingDB(validConnectionString);
            var logger = TestEventSource.Logger;

            using (ObservableEventListener eventListener = new ObservableEventListener())
            {
                try
                {
                    var bufferingInterval = TimeSpan.FromSeconds(2);
                    eventListener.LogToSqlDatabase("WithMinBufferingInterval", validConnectionString, bufferingInterval: bufferingInterval, bufferingCount: 50);
                    eventListener.EnableEvents(logger, EventLevel.LogAlways);

                    // When reaching 50 events the buffer will be flushed
                    for (int i = 0; i < 50; i++)
                    {
                        logger.Critical(i + "Critical message");
                    }

                    // Wait for the buffer to flush at end of interval
                    Task.Delay(bufferingInterval).Wait();
                    var dt = DatabaseHelper.GetLoggedTable(validConnectionString);
                    Assert.AreEqual(50, dt.Rows.Count);
                    Assert.AreEqual(50, dt.Select("InstanceName = 'WithMinBufferingInterval'").Count());
                }
                finally
                {
                    eventListener.DisableEvents(logger);
                }
            }

            // There should not be remaining events flushed during Dispose
            var finalTable = DatabaseHelper.GetLoggedTable(validConnectionString);
            Assert.AreEqual(50, finalTable.Rows.Count);
            Assert.AreEqual(50, finalTable.Select("InstanceName = 'WithMinBufferingInterval'").Count());
        }

        [TestMethod]
        public void WhenDefaultBufferingCountAndNonDefaultBufferInterval()
        {
            var validConnectionString = System.Configuration.ConfigurationManager.ConnectionStrings["valid"].ConnectionString;
            DatabaseHelper.CleanLoggingDB(validConnectionString);
            var logger = TestEventSource.Logger;

            using (ObservableEventListener eventListener = new ObservableEventListener())
            {
                try
                {
                    var bufferingInterval = TimeSpan.FromSeconds(2);
                    eventListener.LogToSqlDatabase("WhenDefaultBufferingCountAndNonDefaultBufferInterval", validConnectionString, bufferingInterval: bufferingInterval);
                    eventListener.EnableEvents(logger, EventLevel.LogAlways);
                    for (int i = 0; i < 50; i++)
                    {
                        logger.Critical(i + "Critical message");
                    }

                    // Wait for the buffer to flush at end of interval
                    Task.Delay(bufferingInterval).Wait();
                    var dt = DatabaseHelper.GetLoggedTable(validConnectionString);
                    Assert.AreEqual(50, dt.Rows.Count);
                    Assert.AreEqual(50, dt.Select("InstanceName = 'WhenDefaultBufferingCountAndNonDefaultBufferInterval'").Count());
                }
                finally
                {
                    eventListener.DisableEvents(logger);
                }
            }

            // There should not be remaining events flushed during Dispose
            var finalTable = DatabaseHelper.GetLoggedTable(validConnectionString);
            Assert.AreEqual(50, finalTable.Rows.Count);
            Assert.AreEqual(50, finalTable.Select("InstanceName = 'WhenDefaultBufferingCountAndNonDefaultBufferInterval'").Count());
        }

        [TestMethod]
        public void WhenNonDefaultBufferingCountAndDefaultBufferInterval()
        {
            var validConnectionString = System.Configuration.ConfigurationManager.ConnectionStrings["valid"].ConnectionString;
            DatabaseHelper.CleanLoggingDB(validConnectionString);
            var logger = TestEventSource.Logger;

            using (ObservableEventListener eventListener = new ObservableEventListener())
            {
                try
                {
                    eventListener.LogToSqlDatabase("WithMinBufferingInterval", validConnectionString, bufferingCount: 50);
                    eventListener.EnableEvents(logger, EventLevel.LogAlways);

                    // When reaching 50 events the buffer will be flushed
                    for (int i = 0; i < 50; i++)
                    {
                        logger.Critical(i + "Critical message");
                    }

                    // Wait for the events to be written
                    Task.Delay(TimeSpan.FromSeconds(1)).Wait();
                    var dt = DatabaseHelper.GetLoggedTable(validConnectionString);
                    Assert.AreEqual(50, dt.Rows.Count);
                    Assert.AreEqual(50, dt.Select("InstanceName = 'WithMinBufferingInterval'").Count());
                }
                finally
                {
                    eventListener.DisableEvents(logger);
                }
            }

            // There should not be remaining events flushed during Dispose
            var finalTable = DatabaseHelper.GetLoggedTable(validConnectionString);
            Assert.AreEqual(50, finalTable.Rows.Count);
            Assert.AreEqual(50, finalTable.Select("InstanceName = 'WithMinBufferingInterval'").Count());
        }

        [TestMethod]
        public void WhenInfiniteBufferingIntervalAndMinBufferingCount()
        {
            var validConnectionString = System.Configuration.ConfigurationManager.ConnectionStrings["valid"].ConnectionString;
            DatabaseHelper.CleanLoggingDB(validConnectionString);
            var logger = TestEventSource.Logger;

            using (ObservableEventListener eventListener = new ObservableEventListener())
            using (ObservableEventListener eventListener1 = new ObservableEventListener())
            using (ObservableEventListener eventListener2 = new ObservableEventListener())
            {
                eventListener.LogToSqlDatabase("WithMinBufferingCount1", validConnectionString, bufferingInterval: Timeout.InfiniteTimeSpan, bufferingCount: 1);
                eventListener.LogToSqlDatabase("WithMinBufferingCount2", validConnectionString, bufferingInterval: Timeout.InfiniteTimeSpan, bufferingCount: 1);
                eventListener.LogToSqlDatabase("WithMinBufferingCount3", validConnectionString, bufferingInterval: Timeout.InfiniteTimeSpan, bufferingCount: 1);
                eventListener.EnableEvents(TestEventSource.Logger, EventLevel.LogAlways);
                eventListener1.EnableEvents(TestEventSource.Logger, EventLevel.LogAlways);
                eventListener2.EnableEvents(TestEventSource.Logger, EventLevel.LogAlways);

                logger.Critical("Critical message 1");
                logger.Critical("Critical message 2");

                // Wait for the events to be written to the database in each listener
                Task.Delay(TimeSpan.FromSeconds(1)).Wait();
                var dt = DatabaseHelper.GetLoggedTable(validConnectionString);
                Assert.AreEqual(6, dt.Rows.Count);
                Assert.AreEqual(2, dt.Select("InstanceName = 'WithMinBufferingCount1'").Count());
                Assert.AreEqual(2, dt.Select("InstanceName = 'WithMinBufferingCount2'").Count());
                Assert.AreEqual(2, dt.Select("InstanceName = 'WithMinBufferingCount3'").Count());
            }

            // There should not be remaining events flushed during Dispose
            var finalTable = DatabaseHelper.GetLoggedTable(validConnectionString);
            Assert.AreEqual(6, finalTable.Rows.Count);
            Assert.AreEqual(2, finalTable.Select("InstanceName = 'WithMinBufferingCount1'").Count());
            Assert.AreEqual(2, finalTable.Select("InstanceName = 'WithMinBufferingCount2'").Count());
            Assert.AreEqual(2, finalTable.Select("InstanceName = 'WithMinBufferingCount3'").Count());
        }

        [TestMethod]
        public void WhenBufferingIntervalIsReachedAndBufferingCountIsZero()
        {
            var validConnectionString = System.Configuration.ConfigurationManager.ConnectionStrings["valid"].ConnectionString;
            DatabaseHelper.CleanLoggingDB(validConnectionString);
            var logger = TestEventSource.Logger;

            using (ObservableEventListener eventListener = new ObservableEventListener())
            using (ObservableEventListener eventListener1 = new ObservableEventListener())
            using (ObservableEventListener eventListener2 = new ObservableEventListener())
            {
                var bufferingInterval = TimeSpan.FromSeconds(2);
                eventListener.LogToSqlDatabase("WhenBufferIntervalIsZeroAndCountIsExceeded1", validConnectionString, bufferingInterval: bufferingInterval, bufferingCount: 0);
                eventListener.LogToSqlDatabase("WhenBufferIntervalIsZeroAndCountIsExceeded2", validConnectionString, bufferingInterval: bufferingInterval, bufferingCount: 0);
                eventListener.LogToSqlDatabase("WhenBufferIntervalIsZeroAndCountIsExceeded3", validConnectionString, bufferingInterval: bufferingInterval, bufferingCount: 0);
                eventListener.EnableEvents(TestEventSource.Logger, EventLevel.LogAlways);
                eventListener1.EnableEvents(TestEventSource.Logger, EventLevel.LogAlways);
                eventListener2.EnableEvents(TestEventSource.Logger, EventLevel.LogAlways);

                logger.Critical("Critical message 1");
                logger.Critical("Critical message 2");

                // Wait for the buffer to flush at end of interval
                Task.Delay(bufferingInterval).Wait();
                var dt = DatabaseHelper.GetLoggedTable(validConnectionString);
                Assert.AreEqual(6, dt.Rows.Count);
                Assert.AreEqual(2, dt.Select("InstanceName = 'WhenBufferIntervalIsZeroAndCountIsExceeded1'").Count());
                Assert.AreEqual(2, dt.Select("InstanceName = 'WhenBufferIntervalIsZeroAndCountIsExceeded2'").Count());
                Assert.AreEqual(2, dt.Select("InstanceName = 'WhenBufferIntervalIsZeroAndCountIsExceeded3'").Count());
            }

            // There should not be remaining events flushed during Dispose
            var finalTable = DatabaseHelper.GetLoggedTable(validConnectionString);
            Assert.AreEqual(6, finalTable.Rows.Count);
            Assert.AreEqual(2, finalTable.Select("InstanceName = 'WhenBufferIntervalIsZeroAndCountIsExceeded1'").Count());
            Assert.AreEqual(2, finalTable.Select("InstanceName = 'WhenBufferIntervalIsZeroAndCountIsExceeded2'").Count());
            Assert.AreEqual(2, finalTable.Select("InstanceName = 'WhenBufferIntervalIsZeroAndCountIsExceeded3'").Count());
        }

        [TestMethod]
        public void WhenErraticEventsInDifferentBufferIntervals()
        {
            var validConnectionString = System.Configuration.ConfigurationManager.ConnectionStrings["valid"].ConnectionString;
            DatabaseHelper.CleanLoggingDB(validConnectionString);
            var logger = MockEventSourceNoTask.Logger;

            using (ObservableEventListener eventListener = new ObservableEventListener())
            {
                try
                {
                    eventListener.LogToSqlDatabase("mytestinstance1", validConnectionString, "Traces", TimeSpan.FromSeconds(3));
                    using (InMemoryEventListener errorsListener = new InMemoryEventListener(true))
                    {
                        try
                        {
                            errorsListener.EnableEvents(SemanticLoggingEventSource.Log, EventLevel.Verbose);
                            eventListener.EnableEvents(logger, EventLevel.Informational);
                            for (int i = 0; i < 20; i++)
                            {
                                logger.InformationalNoMessage("test" + i);
                            }

                            Task.Delay(TimeSpan.FromSeconds(5)).Wait();
                            var result = DatabaseHelper.GetRowCount(validConnectionString);
                            Assert.AreEqual<int>(20, result);

                            Task.Delay(TimeSpan.FromSeconds(5)).Wait();
                            for (int i = 20; i < 50; i++)
                            {
                                logger.InformationalNoMessage("test" + i);
                            }

                            Task.Delay(TimeSpan.FromSeconds(5)).Wait();
                            var result2 = DatabaseHelper.GetRowCount(validConnectionString);
                            Assert.AreEqual<int>(50, result2);

                            for (int i = 50; i < 100; i++)
                            {
                                logger.InformationalNoMessage("test" + i);
                            }

                            Task.Delay(TimeSpan.FromSeconds(5)).Wait();
                            var result3 = DatabaseHelper.GetRowCount(validConnectionString);
                            Assert.AreEqual<int>(100, result3);
                            errorsListener.WaitEvents.Wait(10000);
                            Assert.AreEqual(string.Empty, errorsListener.ToString());
                        }
                        finally
                        {
                            errorsListener.DisableEvents(SemanticLoggingEventSource.Log);
                        }
                    }
                }
                finally
                {
                    eventListener.DisableEvents(logger);
                }
            }
        }

        [TestMethod]
        public void FirstFlushByBufferingCountNextByBufferingIntervalNextByBufferingInterval()
        {
            var validConnectionString = System.Configuration.ConfigurationManager.ConnectionStrings["valid"].ConnectionString;
            DatabaseHelper.CleanLoggingDB(validConnectionString);
            var logger = MockEventSourceNoTask.Logger;

            TimeSpan bufferingInterval = TimeSpan.FromSeconds(4);
            int bufferCount = 10;
            using (ObservableEventListener eventListener = new ObservableEventListener())
            {
                try
                {
                    eventListener.LogToSqlDatabase("mytestinstance1", validConnectionString, bufferingInterval: bufferingInterval, bufferingCount: bufferCount);
                    using (var errorsListener = new InMemoryEventListener(true))
                    {
                        try
                        {
                            errorsListener.EnableEvents(SemanticLoggingEventSource.Log, EventLevel.LogAlways);
                            eventListener.EnableEvents(logger, EventLevel.LogAlways);

                            // The first 10 events reach the buffer size and should be flushed before interval ends
                            for (int i = 0; i < 10; i++)
                            {
                                logger.MaxValues("test", long.MaxValue, int.MaxValue);
                            }

                            // Wait for events to be written
                            Task.Delay(TimeSpan.FromSeconds(1)).Wait();
                            Assert.AreEqual(10, DatabaseHelper.GetRowCount(validConnectionString));

                            // Wait for new next interval
                            Task.Delay(bufferingInterval).Wait();
                            for (int i = 0; i < 5; i++)
                            {
                                logger.MaxValues("test", long.MaxValue, int.MaxValue);
                            }

                            // Wait for interval to flush
                            Task.Delay(bufferingInterval).Wait();
                            Assert.AreEqual(15, DatabaseHelper.GetRowCount(validConnectionString));

                            // Insert new events in next interval
                            Task.Delay(bufferingInterval).Wait();
                            for (int i = 0; i < 5; i++)
                            {
                                logger.MaxValues("test", long.MaxValue, int.MaxValue);
                            }

                            // Wait for interval to flush
                            Task.Delay(bufferingInterval).Wait();
                            Assert.AreEqual(20, DatabaseHelper.GetRowCount(validConnectionString));

                            errorsListener.WaitEvents.Wait(TimeSpan.FromSeconds(1));
                            Assert.AreEqual(string.Empty, errorsListener.ToString());
                        }
                        finally
                        {
                            errorsListener.DisableEvents(SemanticLoggingEventSource.Log);
                        }
                    }
                }
                finally
                {
                    eventListener.DisableEvents(logger);
                }
            }

            // There should not be remaining events flushed during Dispose
            Assert.AreEqual(20, DatabaseHelper.GetRowCount(validConnectionString));
        }

        [TestMethod]
        public void FirstFlushByBufferingCountNextByBufferingIntervalNextByBufferingCount()
        {
            var validConnectionString = System.Configuration.ConfigurationManager.ConnectionStrings["valid"].ConnectionString;
            DatabaseHelper.CleanLoggingDB(validConnectionString);
            var logger = MockEventSourceNoTask.Logger;

            TimeSpan bufferingInterval = TimeSpan.FromSeconds(4);
            int bufferCount = 10;
            using (ObservableEventListener eventListener = new ObservableEventListener())
            {
                try
                {
                    eventListener.LogToSqlDatabase("mytestinstance1", validConnectionString, bufferingInterval: bufferingInterval, bufferingCount: bufferCount);
                    using (var errorsListener = new InMemoryEventListener(true))
                    {
                        try
                        {
                            errorsListener.EnableEvents(SemanticLoggingEventSource.Log, EventLevel.LogAlways);
                            eventListener.EnableEvents(logger, EventLevel.LogAlways);

                            // The first 10 events reach the buffer size and should be flushed before interval ends
                            for (int i = 0; i < 10; i++)
                            {
                                logger.MaxValues("test", long.MaxValue, int.MaxValue);
                            }

                            // Wait for events to be written
                            Task.Delay(TimeSpan.FromSeconds(1)).Wait();
                            Assert.AreEqual(10, DatabaseHelper.GetRowCount(validConnectionString));

                            // Wait for new next interval
                            Task.Delay(bufferingInterval).Wait();
                            for (int i = 0; i < 5; i++)
                            {
                                logger.MaxValues("test", long.MaxValue, int.MaxValue);
                            }

                            // Wait for interval to flush
                            Task.Delay(bufferingInterval).Wait();
                            Assert.AreEqual(15, DatabaseHelper.GetRowCount(validConnectionString));

                            // The first 10 events reach the buffer size and should be flushed before interval ends
                            for (int i = 0; i < 10; i++)
                            {
                                logger.MaxValues("test", long.MaxValue, int.MaxValue);
                            }

                            // Wait for events to be written
                            Task.Delay(TimeSpan.FromSeconds(1)).Wait();
                            Assert.AreEqual(25, DatabaseHelper.GetRowCount(validConnectionString));

                            errorsListener.WaitEvents.Wait(TimeSpan.FromSeconds(1));
                            Assert.AreEqual(string.Empty, errorsListener.ToString());
                        }
                        finally
                        {
                            errorsListener.DisableEvents(SemanticLoggingEventSource.Log);
                        }
                    }
                }
                finally
                {
                    eventListener.DisableEvents(logger);
                }
            }

            // There should not be remaining events flushed during Dispose
            Assert.AreEqual(25, DatabaseHelper.GetRowCount(validConnectionString));
        }

        [TestMethod]
        public void FirstFlushByBufferingCountNextByBufferingCountNextByBufferingCount()
        {
            var validConnectionString = System.Configuration.ConfigurationManager.ConnectionStrings["valid"].ConnectionString;
            DatabaseHelper.CleanLoggingDB(validConnectionString);
            var logger = MockEventSourceNoTask.Logger;

            TimeSpan bufferingInterval = TimeSpan.FromSeconds(4);
            int bufferCount = 10;
            using (ObservableEventListener eventListener = new ObservableEventListener())
            {
                try
                {
                    eventListener.LogToSqlDatabase("mytestinstance1", validConnectionString, bufferingInterval: bufferingInterval, bufferingCount: bufferCount);
                    using (var errorsListener = new InMemoryEventListener(true))
                    {
                        try
                        {
                            errorsListener.EnableEvents(SemanticLoggingEventSource.Log, EventLevel.LogAlways);
                            eventListener.EnableEvents(logger, EventLevel.LogAlways);

                            // The first 10 events reach the buffer size and should be flushed before interval ends
                            for (int i = 0; i < 10; i++)
                            {
                                logger.MaxValues("test", long.MaxValue, int.MaxValue);
                            }

                            // Wait for events to be written
                            Task.Delay(TimeSpan.FromSeconds(1)).Wait();
                            Assert.AreEqual(10, DatabaseHelper.GetRowCount(validConnectionString));

                            // The first 10 events reach the buffer size and should be flushed before interval ends
                            for (int i = 0; i < 10; i++)
                            {
                                logger.MaxValues("test", long.MaxValue, int.MaxValue);
                            }

                            // Wait for events to be written
                            Task.Delay(TimeSpan.FromSeconds(1)).Wait();
                            Assert.AreEqual(20, DatabaseHelper.GetRowCount(validConnectionString));

                            // The first 10 events reach the buffer size and should be flushed before interval ends
                            for (int i = 0; i < 10; i++)
                            {
                                logger.MaxValues("test", long.MaxValue, int.MaxValue);
                            }

                            // Wait for events to be written
                            Task.Delay(TimeSpan.FromSeconds(1)).Wait();
                            Assert.AreEqual(30, DatabaseHelper.GetRowCount(validConnectionString));

                            errorsListener.WaitEvents.Wait(TimeSpan.FromSeconds(1));
                            Assert.AreEqual(string.Empty, errorsListener.ToString());
                        }
                        finally
                        {
                            errorsListener.DisableEvents(SemanticLoggingEventSource.Log);
                        }
                    }
                }
                finally
                {
                    eventListener.DisableEvents(logger);
                }
            }

            // There should not be remaining events flushed during Dispose
            Assert.AreEqual(30, DatabaseHelper.GetRowCount(validConnectionString));
        }

        [TestMethod]
        public void FirstFlushByBufferingIntervalNextByBufferingIntervalNextByBufferingInterval()
        {
            var validConnectionString = System.Configuration.ConfigurationManager.ConnectionStrings["valid"].ConnectionString;
            DatabaseHelper.CleanLoggingDB(validConnectionString);
            var logger = MockEventSourceNoTask.Logger;

            TimeSpan bufferingInterval = TimeSpan.FromSeconds(4);
            int bufferCount = 10;
            using (ObservableEventListener eventListener = new ObservableEventListener())
            {
                try
                {
                    eventListener.LogToSqlDatabase("mytestinstance1", validConnectionString, bufferingInterval: bufferingInterval, bufferingCount: bufferCount);
                    using (var errorsListener = new InMemoryEventListener(true))
                    {
                        try
                        {
                            errorsListener.EnableEvents(SemanticLoggingEventSource.Log, EventLevel.LogAlways);
                            eventListener.EnableEvents(logger, EventLevel.LogAlways);

                            // Insert new events in this interval
                            for (int i = 0; i < 5; i++)
                            {
                                logger.MaxValues("test", long.MaxValue, int.MaxValue);
                            }

                            // Wait for interval to flush
                            Task.Delay(bufferingInterval).Wait();

                            // Wait for events to be written
                            Task.Delay(TimeSpan.FromSeconds(1)).Wait();
                            Assert.AreEqual(5, DatabaseHelper.GetRowCount(validConnectionString));

                            // Insert new events in next interval
                            for (int i = 0; i < 5; i++)
                            {
                                logger.MaxValues("test", long.MaxValue, int.MaxValue);
                            }

                            // Wait for interval to flush
                            Task.Delay(bufferingInterval).Wait();
                            Assert.AreEqual(10, DatabaseHelper.GetRowCount(validConnectionString));

                            // Insert new events in next interval
                            for (int i = 0; i < 5; i++)
                            {
                                logger.MaxValues("test", long.MaxValue, int.MaxValue);
                            }

                            // Wait for interval to flush
                            Task.Delay(bufferingInterval).Wait();
                            Assert.AreEqual(15, DatabaseHelper.GetRowCount(validConnectionString));

                            errorsListener.WaitEvents.Wait(TimeSpan.FromSeconds(1));
                            Assert.AreEqual(string.Empty, errorsListener.ToString());
                        }
                        finally
                        {
                            errorsListener.DisableEvents(SemanticLoggingEventSource.Log);
                        }
                    }
                }
                finally
                {
                    eventListener.DisableEvents(logger);
                }
            }

            // There should not be remaining events flushed during Dispose
            Assert.AreEqual(15, DatabaseHelper.GetRowCount(validConnectionString));
        }

        [TestMethod]
        public void FirstFlushByBufferingIntervalNextByBufferingCountNextByBufferingInterval()
        {
            var validConnectionString = System.Configuration.ConfigurationManager.ConnectionStrings["valid"].ConnectionString;
            DatabaseHelper.CleanLoggingDB(validConnectionString);
            var logger = MockEventSourceNoTask.Logger;

            TimeSpan bufferingInterval = TimeSpan.FromSeconds(4);
            int bufferCount = 10;
            using (ObservableEventListener eventListener = new ObservableEventListener())
            {
                try
                {
                    eventListener.LogToSqlDatabase("mytestinstance1", validConnectionString, bufferingInterval: bufferingInterval, bufferingCount: bufferCount);
                    using (var errorsListener = new InMemoryEventListener(true))
                    {
                        try
                        {
                            errorsListener.EnableEvents(SemanticLoggingEventSource.Log, EventLevel.LogAlways);
                            eventListener.EnableEvents(logger, EventLevel.LogAlways);

                            // Insert new events in this interval
                            for (int i = 0; i < 5; i++)
                            {
                                logger.MaxValues("test", long.MaxValue, int.MaxValue);
                            }

                            // Wait for interval to flush
                            Task.Delay(bufferingInterval).Wait();

                            // Wait for events to be written
                            Task.Delay(TimeSpan.FromSeconds(1)).Wait();
                            Assert.AreEqual(5, DatabaseHelper.GetRowCount(validConnectionString));

                            // The first 10 events reach the buffer size and should be flushed before interval ends
                            for (int i = 0; i < 10; i++)
                            {
                                logger.MaxValues("test", long.MaxValue, int.MaxValue);
                            }

                            // Wait for events to be written
                            Task.Delay(TimeSpan.FromSeconds(1)).Wait();
                            Assert.AreEqual(15, DatabaseHelper.GetRowCount(validConnectionString));

                            // Insert new events in next interval
                            for (int i = 0; i < 5; i++)
                            {
                                logger.MaxValues("test", long.MaxValue, int.MaxValue);
                            }

                            // Wait for interval to flush
                            Task.Delay(bufferingInterval).Wait();
                            Assert.AreEqual(20, DatabaseHelper.GetRowCount(validConnectionString));

                            errorsListener.WaitEvents.Wait(TimeSpan.FromSeconds(1));
                            Assert.AreEqual(string.Empty, errorsListener.ToString());
                        }
                        finally
                        {
                            errorsListener.DisableEvents(SemanticLoggingEventSource.Log);
                        }
                    }
                }
                finally
                {
                    eventListener.DisableEvents(logger);
                }
            }

            // There should not be remaining events flushed during Dispose
            Assert.AreEqual(20, DatabaseHelper.GetRowCount(validConnectionString));
        }

        [TestMethod]
        public void FirstFlushByBufferingIntervalNextByBufferingCountNextByBufferingCount()
        {
            var validConnectionString = System.Configuration.ConfigurationManager.ConnectionStrings["valid"].ConnectionString;
            DatabaseHelper.CleanLoggingDB(validConnectionString);
            var logger = MockEventSourceNoTask.Logger;

            TimeSpan bufferingInterval = TimeSpan.FromSeconds(4);
            int bufferCount = 10;
            using (ObservableEventListener eventListener = new ObservableEventListener())
            {
                try
                {
                    eventListener.LogToSqlDatabase("mytestinstance1", validConnectionString, bufferingInterval: bufferingInterval, bufferingCount: bufferCount);
                    using (var errorsListener = new InMemoryEventListener(true))
                    {
                        try
                        {
                            errorsListener.EnableEvents(SemanticLoggingEventSource.Log, EventLevel.LogAlways);
                            eventListener.EnableEvents(logger, EventLevel.LogAlways);

                            // Insert new events in this interval
                            for (int i = 0; i < 5; i++)
                            {
                                logger.MaxValues("test", long.MaxValue, int.MaxValue);
                            }

                            // Wait for interval to flush
                            Task.Delay(bufferingInterval).Wait();

                            // Wait for events to be written
                            Task.Delay(TimeSpan.FromSeconds(1)).Wait();
                            Assert.AreEqual(5, DatabaseHelper.GetRowCount(validConnectionString));

                            // The first 10 events reach the buffer size and should be flushed before interval ends
                            for (int i = 0; i < 10; i++)
                            {
                                logger.MaxValues("test", long.MaxValue, int.MaxValue);
                            }

                            // Wait for events to be written
                            Task.Delay(TimeSpan.FromSeconds(1)).Wait();
                            Assert.AreEqual(15, DatabaseHelper.GetRowCount(validConnectionString));

                            // The first 10 events reach the buffer size and should be flushed before interval ends
                            for (int i = 0; i < 10; i++)
                            {
                                logger.MaxValues("test", long.MaxValue, int.MaxValue);
                            }

                            // Wait for events to be written
                            Task.Delay(TimeSpan.FromSeconds(1)).Wait();
                            Assert.AreEqual(25, DatabaseHelper.GetRowCount(validConnectionString));

                            errorsListener.WaitEvents.Wait(TimeSpan.FromSeconds(1));
                            Assert.AreEqual(string.Empty, errorsListener.ToString());
                        }
                        finally
                        {
                            errorsListener.DisableEvents(SemanticLoggingEventSource.Log);
                        }
                    }
                }
                finally
                {
                    eventListener.DisableEvents(logger);
                }
            }

            // There should not be remaining events flushed during Dispose
            Assert.AreEqual(25, DatabaseHelper.GetRowCount(validConnectionString));
        }
    }
}