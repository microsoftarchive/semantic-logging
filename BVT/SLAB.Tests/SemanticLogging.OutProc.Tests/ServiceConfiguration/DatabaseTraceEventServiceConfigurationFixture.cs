// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw.Configuration;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.OutProc.Tests.TestObjects;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.Shared.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Configuration;
using System.Threading;
using System.Threading.Tasks;
using EtwConfig = Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw.Configuration;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.OutProc.Tests.ServiceConfiguration
{
    [TestClass]
    public class DatabaseTraceEventServiceConfigurationFixture
    {
        [TestMethod]
        public void SqlDatabaseCorrectConfiguration()
        {
            var validConnectionString = ConfigurationManager.ConnectionStrings["valid"].ConnectionString;
            DatabaseHelper.CleanLoggingDB(validConnectionString);
            var logger = MockEventSourceOutProc.Logger;

            System.Data.DataTable eventsDataTable = null;
            var svcConfiguration = TraceEventServiceConfiguration.Load("Configurations\\SqlDatabase\\SqlDatabaseHappyPath.xml");
            using (var evtService = new TraceEventService(svcConfiguration))
            {
                evtService.Start();
                try
                {
                    for (int n = 0; n < 10; n++)
                    {
                        logger.LogSomeMessage("some message" + n.ToString());
                    }

                    eventsDataTable = DatabaseHelper.PollUntilEventsAreWritten(validConnectionString, 10);
                }
                finally
                {
                    evtService.Stop();
                }
            }

            Assert.AreEqual(10, eventsDataTable.Rows.Count);
        }

        [TestMethod]
        public void SqlDatabaseCorrectConfigurationWithCustomTable()
        {
            var validConnectionString = ConfigurationManager.ConnectionStrings["valid"].ConnectionString;
            DatabaseHelper.CleanLoggingDB(validConnectionString);
            var logger = MockEventSourceOutProc.Logger;

            System.Data.DataTable eventsDataTable = null;
            var svcConfiguration = TraceEventServiceConfiguration.Load("Configurations\\SqlDatabase\\SqlDatabaseCustomTable.xml");
            using (var evtService = new TraceEventService(svcConfiguration))
            {
                evtService.Start();
                try
                {
                    for (int n = 0; n < 10; n++)
                    {
                        logger.LogSomeMessage("some message" + n.ToString());
                    }

                    eventsDataTable = DatabaseHelper.PollUntilEventsAreWritten(validConnectionString, 10);
                }
                finally
                {
                    evtService.Stop();
                }
            }

            Assert.AreEqual(10, eventsDataTable.Rows.Count);
        }

        [TestMethod]
        public void SqlDatabaseEmptyConnectionString()
        {
            var exc = ExceptionAssertHelper.Throws<EtwConfig.ConfigurationException>(() => EtwConfig.TraceEventServiceConfiguration.Load("Configurations\\SqlDatabase\\SqlDBEmptyConnStr.xml"));

            StringAssert.Contains(exc.ToString(), "The 'connectionString' attribute is invalid - The value '' is invalid according to its datatype ");
        }

        [TestMethod]
        public void SqlDatabaseMissingConnectionString()
        {
            var exc = ExceptionAssertHelper.Throws<EtwConfig.ConfigurationException>(() => EtwConfig.TraceEventServiceConfiguration.Load("Configurations\\SqlDatabase\\SqlDBMissingConnStr.xml"));

            StringAssert.Contains(exc.ToString(), "The required attribute 'connectionString' is missing.");
        }

        [TestMethod]
        public void SqlDatabaseEmptyInstanceName()
        {
            var exc = ExceptionAssertHelper.Throws<EtwConfig.ConfigurationException>(() => EtwConfig.TraceEventServiceConfiguration.Load("Configurations\\SqlDatabase\\SqlDBEmptyInstance.xml"));

            StringAssert.Contains(exc.ToString(), "The 'instanceName' attribute is invalid - The value '' is invalid according to its datatype");
        }

        [TestMethod]
        public void SqlDatabaseMissingInstanceName()
        {
            var exc = ExceptionAssertHelper.Throws<EtwConfig.ConfigurationException>(() => EtwConfig.TraceEventServiceConfiguration.Load("Configurations\\SqlDatabase\\SqlDBMissingInstance.xml"));

            StringAssert.Contains(exc.ToString(), "The required attribute 'instanceName' is missing.");
        }

        [TestMethod]
        public void SqlDatabaseEmptyDatabaseName()
        {
            var exc = ExceptionAssertHelper.Throws<EtwConfig.ConfigurationException>(() => EtwConfig.TraceEventServiceConfiguration.Load("Configurations\\SqlDatabase\\SqlDBEmptyName.xml"));

            StringAssert.Contains(exc.ToString(), "The 'name' attribute is invalid - The value '' is invalid according to its datatype ");
        }

        [TestMethod]
        public void SqlDatabaseMissingDatabaseName()
        {
            var exc = ExceptionAssertHelper.Throws<EtwConfig.ConfigurationException>(() => EtwConfig.TraceEventServiceConfiguration.Load("Configurations\\SqlDatabase\\SqlDBMissingName.xml"));

            StringAssert.Contains(exc.ToString(), "The required attribute 'name' is missing.");
        }

        [TestMethod]
        public void SqlDatabaseEmptyBufferingInterval()
        {
            var exc = ExceptionAssertHelper.Throws<EtwConfig.ConfigurationException>(() => EtwConfig.TraceEventServiceConfiguration.Load("Configurations\\SqlDatabase\\SqlBufferingIntervalEmpty.xml"));
            
            StringAssert.Contains(exc.ToString(), "The 'bufferingIntervalInSeconds' attribute is invalid - The value '' is invalid according to its datatype ");
        }

        [TestMethod]
        public void SqlDatabaseEmptyBufferingCount()
        {
            var exc = ExceptionAssertHelper.Throws<EtwConfig.ConfigurationException>(() => EtwConfig.TraceEventServiceConfiguration.Load("Configurations\\SqlDatabase\\SqlBufferingCountEmpty.xml"));
            
            StringAssert.Contains(exc.ToString(), "The 'bufferingCount' attribute is invalid - The value '' is invalid according to its datatype ");
        }

        [TestMethod]
        public void SqlDatabaseEmptyTableName()
        {
            var exc = ExceptionAssertHelper.Throws<EtwConfig.ConfigurationException>(() => EtwConfig.TraceEventServiceConfiguration.Load("Configurations\\SqlDatabase\\SqlDBTableEmpty.xml"));
            
            StringAssert.Contains(exc.ToString(), "The 'tableName' attribute is invalid - The value '' is invalid according to its datatype");
        }
    }
}
