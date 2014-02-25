// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw.Configuration;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.Shared.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.OutProc.Tests.ServiceConfiguration
{
    [TestClass]
    public class AzureTablesTraceEventServiceConfigurationFixture
    {
        [TestMethod]
        public void AzureTablesMissingConnectionString()
        {
            var exc = ExceptionAssertHelper.Throws<ConfigurationException>(() => TraceEventServiceConfiguration.Load("Configurations\\AzureTables\\AzureTablesMissingConnectionString.xml"));

            StringAssert.Contains(exc.ToString(), "The required attribute 'connectionString' is missing.");
        }

        [TestMethod]
        public void AzureTablesMissingInstanceName()
        {
            var exc = ExceptionAssertHelper.Throws<ConfigurationException>(() => TraceEventServiceConfiguration.Load("Configurations\\AzureTables\\AzureTablesMissingInstanceName.xml"));

            StringAssert.Contains(exc.ToString(), "The required attribute 'instanceName' is missing.");
        }

        [TestMethod]
        public void AzureTablesMissingTableAddress()
        {
            var serviceConfiguration = TraceEventServiceConfiguration.Load("Configurations\\AzureTables\\AzureTablesMissingTableAddress.xml");

            Assert.IsNotNull(serviceConfiguration);
        }

        [TestMethod]
        public void AzureTablesEmptyConnStr()
        {
            var exc = ExceptionAssertHelper.Throws<ConfigurationException>(() => TraceEventServiceConfiguration.Load("Configurations\\AzureTables\\AzureTablesEmptyConnectionString.xml"));

            StringAssert.Contains(exc.ToString(), "The 'connectionString' attribute is invalid - The value '' is invalid according to its datatype ");
        }

        [TestMethod]
        public void AzureTablesEmptyInstanceName()
        {
            var exc = ExceptionAssertHelper.Throws<ConfigurationException>(() => TraceEventServiceConfiguration.Load("Configurations\\AzureTables\\AzureTablesEmptyInstanceName.xml"));

            StringAssert.Contains(exc.ToString(), "The 'instanceName' attribute is invalid - The value '' is invalid according to its datatype");
        }

        [TestMethod]
        public void AzureTablesEmptyTableAddress()
        {
            var exc = ExceptionAssertHelper.Throws<ConfigurationException>(() => TraceEventServiceConfiguration.Load("Configurations\\AzureTables\\AzureTablesEmptyTableAddress.xml"));

            StringAssert.Contains(exc.ToString(), "The 'tableAddress' attribute is invalid - The value '' is invalid according to its datatype");
        }

        [TestMethod]
        public void AzureTablesMaxBufferSizeEmpty()
        {
            var exc = ExceptionAssertHelper.Throws<ConfigurationException>(() => TraceEventServiceConfiguration.Load("Configurations\\AzureTables\\AzureTablesMaxBufferSizeEmpty.xml"));

            StringAssert.Contains(exc.ToString(), "The 'maxBufferSize' attribute is invalid - The value '' is invalid according to its datatype");
        }

        [TestMethod]
        public void AzureTablesMaxBufferSizeLessThan500()
        {
            var exc = ExceptionAssertHelper.Throws<ConfigurationException>(() => TraceEventServiceConfiguration.Load("Configurations\\AzureTables\\AzureTablesMaxBufferSizeValidation.xml"));

            StringAssert.Contains(exc.ToString(), "The size of 'maxBufferSize' should be greater or equal to '500'.");
            StringAssert.Contains(exc.ToString(), "Parameter name: maxBufferSize");
        }

        [TestMethod]
        public void AzureTablesMaxBufferSize()
        {
            var serviceConfiguration = TraceEventServiceConfiguration.Load("Configurations\\AzureTables\\AzureTablesMaxBufferSize.xml");

            Assert.IsNotNull(serviceConfiguration);
        }
    }
}
