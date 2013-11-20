// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using System.Xml.Linq;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.Configuration
{
    [TestClass]
    public class given_configurationReaderInstance
    {
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void when_creating_instance_with_null_file()
        {
            new ConfigurationReader(null);
        }
    }

    [TestClass]
    public class when_reading_from_file_with_many_sinks
    {
        [TestMethod]
        public void then_instance_is_loaded_with_all_sinks()
        {
            var sut = new ConfigurationReader("Etw\\Configuration\\WithManySinks.xml");
            ConfigurationElement config = sut.Read();

            SinkConfigurationElement[] elements = config.SinkConfigurationElements.ToArray();
            Assert.AreEqual(4, elements.Length);
            Assert.AreEqual("Microsoft.SemanticLogging.Etw", config.TraceEventService.SessionNamePrefix);

            Assert.IsNotNull(elements[0].SinkPromise.Value);
            Assert.AreEqual(1, elements[0].EventSources.Count());

            Assert.IsNotNull(elements[1].SinkPromise.Value);
            Assert.AreEqual(1, elements[1].EventSources.Count());

            Assert.IsNotNull(elements[2].SinkPromise.Value);
            Assert.AreEqual(1, elements[2].EventSources.Count());
        }
    }

    [TestClass]
    public class when_reading_from_two_different_files
    {
        [TestMethod]
        public void then_differences_should_be_detected()
        {
            var sut1 = new ConfigurationReader("Etw\\Configuration\\WithDiff1.xml");
            var sut2 = new ConfigurationReader("Etw\\Configuration\\WithDiff2.xml");
            ConfigurationElement config1 = sut1.Read();
            ConfigurationElement config2 = sut2.Read();

            Assert.AreEqual("Microsoft.SemanticLogging.Etw", config1.TraceEventService.SessionNamePrefix);
            Assert.AreEqual("Microsoft.SemanticLogging.Etw.Diff1", config2.TraceEventService.SessionNamePrefix);

            Assert.AreEqual(1, config1.SinkConfigurationElements.First().EventSources.Count());
            Assert.AreEqual(2, config2.SinkConfigurationElements.First().EventSources.Count());

            Assert.AreNotEqual(
                config1.SinkConfigurationElements.First().SinkConfiguration,
                config2.SinkConfigurationElements.First().SinkConfiguration);
        }
    }

    [TestClass]
    public class when_reading_from_two_equal_files
    {
        [TestMethod]
        public void then_differences_should_not_be_detected()
        {
            var sut0 = new ConfigurationReader("Etw\\Configuration\\WithDiff0.xml");
            var sut1 = new ConfigurationReader("Etw\\Configuration\\WithDiff1.xml");
            ConfigurationElement config0 = sut0.Read();
            ConfigurationElement config1 = sut1.Read();

            Assert.AreEqual(
                config0.SinkConfigurationElements.First().SinkConfiguration,
                config1.SinkConfigurationElements.First().SinkConfiguration);
        }
    }
}
