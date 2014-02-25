// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw.Configuration;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.OutProc.Tests.TestObjects;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.Shared.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.OutProc.Tests.ServiceConfiguration
{
    [TestClass]
    public class FlatFileSinkConfigFixture
    {
        [TestMethod]
        public void WhenConfigValidAndComplete()
        {
            var svcConfiguration = TraceEventServiceConfiguration.Load("Configurations\\FlatFile\\FlatFile.xml");

            Assert.IsNotNull(svcConfiguration);
        }

        [TestMethod]
        public void WhenConfiguringCustomFormatter()
        {
            var svcConfiguration = TraceEventServiceConfiguration.Load("Configurations\\FlatFile\\FlatFileCustomFormatter.xml");

            Assert.IsNotNull(svcConfiguration);
        }

        [TestMethod]
        public void WhenConfiguringJsonFormatter()
        {
            var svcConfiguration = TraceEventServiceConfiguration.Load("Configurations\\FlatFile\\FlatFileJsonFormatter.xml");

            Assert.IsNotNull(svcConfiguration);
        }

        [TestMethod]
        public void WhenConfigurationJsonFormatterDefaultValuesForProperties()
        {
            var svcConfiguration = TraceEventServiceConfiguration.Load("Configurations\\FlatFile\\FlatFileJsonFormatterMissingParams.xml");

            Assert.IsNotNull(svcConfiguration);
        }

        [TestMethod]
        public void WhenConfiguringXmlFormatter()
        {
            var svcConfiguration = TraceEventServiceConfiguration.Load("Configurations\\FlatFile\\FlatFileXmlFormatter.xml");

            Assert.IsNotNull(svcConfiguration);
        }

        [TestMethod]
        public void WhenConfiguringXmlFormatterDefaultValuesForProperties()
        {
            var svcConfiguration = TraceEventServiceConfiguration.Load("Configurations\\FlatFile\\FlatFileXmlFormatterMissingParams.xml");

            Assert.IsNotNull(svcConfiguration);
        }

        [TestMethod]
        public void WhenConfiguringWithNoProperties()
        {
            var exc = ExceptionAssertHelper.Throws<ConfigurationException>(() => TraceEventServiceConfiguration.Load("Configurations\\FlatFile\\FlatFileNoParams.xml"));

            StringAssert.Contains(exc.ToString(), "The required attribute 'fileName' is missing.");
            StringAssert.Contains(exc.ToString(), "The required attribute 'name' is missing.");
        }

        [TestMethod]
        public void WhenEmptyFileName()
        {
            var exc = ExceptionAssertHelper.Throws<ConfigurationException>(() => TraceEventServiceConfiguration.Load("Configurations\\FlatFile\\FlatFileEmptyFileName.xml"));

            StringAssert.Contains(exc.ToString(), "The 'fileName' attribute is invalid - The value '' is invalid according to its datatype");
        }

        [TestMethod]
        public void WhenSinkWithName()
        {
            var exc = ExceptionAssertHelper.Throws<ConfigurationException>(() => TraceEventServiceConfiguration.Load("Configurations\\FlatFile\\FlatFileEmptyName.xml"));

            StringAssert.Contains(exc.ToString(), "The 'name' attribute is invalid - The value '' is invalid according to its datatype");
        }

        [TestMethod]
        public void WhenFilePathIsInvalid()
        {
            var exc = ExceptionAssertHelper.Throws<ConfigurationException>(() => TraceEventServiceConfiguration.Load("Configurations\\FlatFile\\FlatWrongFormatInFile.xml"));

            string fullExc = exc.ToString();
            StringAssert.Contains(fullExc, "The given path's format is not supported.");
        }

        [TestMethod]
        public void WhenNoFormatter()
        {
            var svcConfiguration = TraceEventServiceConfiguration.Load("Configurations\\FlatFile\\FlatFileNoFormatter.xml");

            Assert.IsNotNull(svcConfiguration);
        }

        [TestMethod]
        public void OutProcWhenFormatterWithValuesForProperties()
        {
            string fileName = "FlatFileFormatterOutProc.log";
            File.Delete(fileName);

            IEnumerable<string> entries = null;
            using (var svcConfiguration = TraceEventServiceConfiguration.Load("Configurations\\FlatFile\\FlatFileFormatterOutProc.xml"))
            using (var eventCollectorService = new TraceEventService(svcConfiguration))
            {
                eventCollectorService.Start();

                try
                {
                    MockEventSourceOutProc.Logger.LogSomeMessage("some message");
                    MockEventSourceOutProc.Logger.LogSomeMessage("some message2");
                    entries = FlatFileHelper.PollUntilTextEventsAreWritten(fileName, 2, "=======");
                }
                finally
                {
                    eventCollectorService.Stop();
                }
            }

            Assert.AreEqual(2, entries.Count());
            StringAssert.Contains(entries.First(), "some message");
            StringAssert.Contains(entries.Last(), "some message2");
        }
    }
}
