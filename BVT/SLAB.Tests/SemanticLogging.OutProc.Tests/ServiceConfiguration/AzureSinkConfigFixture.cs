// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw.Configuration;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.Shared.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.OutProc.Tests.ServiceConfiguration
{
    [TestClass]
    public class AzureSinkConfigFixture
    {
        [ClassInitialize]
        public static void Setup(TestContext testContext)
        {
            AssemblyLoaderHelper.EnsureAllAssembliesAreLoadedForSinkTest();
        }

        [TestMethod]
        public void WhenMissingConnectionString()
        {
            var exc = ExceptionAssertHelper.Throws<ConfigurationException>(() => TraceEventServiceConfiguration.Load("Configurations\\AzureTables\\AzureTablesMissingConnectionString.xml"));

            StringAssert.Contains(exc.ToString(), "The required attribute 'connectionString' is missing.");
        }

        [TestMethod]
        public void WhenMissingInstanceName()
        {
            var exc = ExceptionAssertHelper.Throws<ConfigurationException>(() => TraceEventServiceConfiguration.Load("Configurations\\AzureTables\\AzureTablesMissingInstanceName.xml"));

            StringAssert.Contains(exc.ToString(), "The required attribute 'instanceName' is missing.");
        }

        [TestMethod]
        public void WhenMissingTableAddress()
        {
            var serviceConfiguration = TraceEventServiceConfiguration.Load("Configurations\\AzureTables\\AzureTablesMissingTableAddress.xml");

            Assert.IsNotNull(serviceConfiguration);
        }

        [TestMethod]
        public void WhenEmptyConnStr()
        {
            var exc = ExceptionAssertHelper.Throws<ConfigurationException>(() => TraceEventServiceConfiguration.Load("Configurations\\AzureTables\\AzureTablesEmptyConnectionString.xml"));

            StringAssert.Contains(exc.ToString(), "The 'connectionString' attribute is invalid - The value '' is invalid according to its datatype ");
        }

        [TestMethod]
        public void WhenEmptyInstanceName()
        {
            var exc = ExceptionAssertHelper.Throws<ConfigurationException>(() => TraceEventServiceConfiguration.Load("Configurations\\AzureTables\\AzureTablesEmptyInstanceName.xml"));

            StringAssert.Contains(exc.ToString(), "The 'instanceName' attribute is invalid - The value '' is invalid according to its datatype");
        }

        [TestMethod]
        public void WhenmptyTableAddress()
        {
            var exc = ExceptionAssertHelper.Throws<ConfigurationException>(() => TraceEventServiceConfiguration.Load("Configurations\\AzureTables\\AzureTablesEmptyTableAddress.xml"));

            StringAssert.Contains(exc.ToString(), "The 'tableAddress' attribute is invalid - The value '' is invalid according to its datatype");
        }

        [TestMethod]
        public void WhenMaxBufferSizeEmpty()
        {
            var exc = ExceptionAssertHelper.Throws<ConfigurationException>(() => TraceEventServiceConfiguration.Load("Configurations\\AzureTables\\AzureTablesMaxBufferSizeEmpty.xml"));

            StringAssert.Contains(exc.ToString(), "The 'maxBufferSize' attribute is invalid - The value '' is invalid according to its datatype");
        }

        [TestMethod]
        public void WhenMaxBufferSizeLessThan500()
        {
            var exc = ExceptionAssertHelper.Throws<ConfigurationException>(() => TraceEventServiceConfiguration.Load("Configurations\\AzureTables\\AzureTablesMaxBufferSizeValidation.xml"));

            StringAssert.Contains(exc.ToString(), "The size of 'maxBufferSize' should be greater or equal to '500'.");
            StringAssert.Contains(exc.ToString(), "Parameter name: maxBufferSize");
        }

        [TestMethod]
        public void WhenConfigValidAndComplete()
        {
            var serviceConfiguration = TraceEventServiceConfiguration.Load("Configurations\\AzureTables\\AzureTablesMaxBufferSize.xml");

            Assert.IsNotNull(serviceConfiguration);
        }
    }
}
