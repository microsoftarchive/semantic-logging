// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw.Configuration;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.Shared.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ConfigException = Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw.Configuration.ConfigurationException;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.OutProc.Tests.ServiceConfiguration
{
    [TestClass]
    public class ElasticsearchSinkConfigFixture
    {
        [ClassInitialize]
        public static void Setup(TestContext testContext)
        {
            AssemblyLoaderHelper.EnsureAllAssembliesAreLoadedForSinkTest();
        }

        [TestMethod]
        public void WhenMissingConnectionString()
        {
            var exc = ExceptionAssertHelper.Throws<ConfigException>(() => TraceEventServiceConfiguration.Load("Configurations\\ElasticsearchSink\\ElasticSinkMissingConnectionString.xml"));

            StringAssert.Contains(exc.ToString(), "The required attribute 'connectionString' is missing.");
        }

        [TestMethod]
        public void WhenMissingInstanceName()
        {
            var exc = ExceptionAssertHelper.Throws<ConfigException>(() => TraceEventServiceConfiguration.Load("Configurations\\ElasticsearchSink\\ElasticSinkMissingInstanceName.xml"));

            StringAssert.Contains(exc.ToString(), "The required attribute 'instanceName' is missing.");
        }

        [TestMethod]
        public void WhenEmptyIndex()
        {
            var exc = ExceptionAssertHelper.Throws<ConfigException>(() => TraceEventServiceConfiguration.Load("Configurations\\ElasticsearchSink\\ElasticSinkEmptyIndex.xml"));

            StringAssert.Contains(exc.ToString(), "'index' attribute is invalid");
        }

        [TestMethod]
        public void WhenEmptyConnectionString()
        {
            var exc = ExceptionAssertHelper.Throws<ConfigException>(() => TraceEventServiceConfiguration.Load("Configurations\\ElasticsearchSink\\ElasticSinkEmptyConnectionString.xml"));

            StringAssert.Contains(exc.ToString(), "'connectionString' attribute is invalid");
        }

        [TestMethod]
        public void WhenEmptyInstanceName()
        {
            var exc = ExceptionAssertHelper.Throws<ConfigException>(() => TraceEventServiceConfiguration.Load("Configurations\\ElasticsearchSink\\ElasticSinkEmptyInstanceName.xml"));

            StringAssert.Contains(exc.ToString(), "The 'instanceName' attribute is invalid - The value '' is invalid according to its datatype");
        }

        [TestMethod]
        public void WhenEmptyTypeName()
        {
            var exc = ExceptionAssertHelper.Throws<ConfigException>(() => TraceEventServiceConfiguration.Load("Configurations\\ElasticsearchSink\\ElasticSinkEmptyTypeName.xml"));

            StringAssert.Contains(exc.ToString(), "The 'type' attribute is invalid - The value '' is invalid according to its datatype");
        }

        [TestMethod]
        public void WhenAllProperties()
        {
            var serviceConfiguration = TraceEventServiceConfiguration.Load("Configurations\\ElasticsearchSink\\ElasticSinkAllProperties.xml");
          
            Assert.IsNotNull(serviceConfiguration);
        }

        [TestMethod]
        public void WhenOnlyMandatoryProperties()
        {
            var serviceConfiguration = TraceEventServiceConfiguration.Load("Configurations\\ElasticsearchSink\\ElasticSinkMandatoryProperties.xml");
          
            Assert.IsNotNull(serviceConfiguration);
        }
    }
}
