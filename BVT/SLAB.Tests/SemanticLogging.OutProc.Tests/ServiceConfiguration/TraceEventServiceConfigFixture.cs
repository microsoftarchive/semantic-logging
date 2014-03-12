// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw.Configuration;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Observable;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.OutProc.Tests.TestObjects;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.Shared.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;
using System.Xml.Schema;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.OutProc.Tests.ServiceConfiguration
{
    [TestClass]
    public class TraceEventServiceConfigFixture
    {
        [TestMethod]
        public void WhenSessionNameIsTooLong()
        {
            // session name should not be more than 200 chars
            var exc = ExceptionAssertHelper.Throws<ConfigurationException>(() => TraceEventServiceConfiguration.Load("Configurations\\LongSessionPrefix.xml"));

            StringAssert.Contains(exc.ToString(), "The 'sessionNamePrefix' attribute is invalid - The value");
            StringAssert.Contains(exc.ToString(), "The actual length is greater than the MaxLength value.");
        }

        [TestMethod]
        public void WhenSinkNameIsTooLong()
        {
            // sink name should not be moe than 200 chars
            var exc = ExceptionAssertHelper.Throws<ConfigurationException>(() => TraceEventServiceConfiguration.Load("Configurations\\LongSinkName.xml"));

            StringAssert.Contains(exc.ToString(), "The 'name' attribute is invalid - The value");
            StringAssert.Contains(exc.ToString(), "The actual length is greater than the MaxLength value.");
        }

        [TestMethod]
        public void WhenSomeSessionNamesAreInvalid()
        {
            File.Delete("sessionLength.log");
            File.Delete("sessionLength2.log");

            var cfg = TraceEventServiceConfiguration.Load("Configurations\\SessionNameLongInOneSink.xml");
            using (TraceEventService svc = new TraceEventService(cfg))
            {
                svc.Start();

                Assert.IsTrue(TraceSessionHelper.WaitAndAssertCountOfSessions("ServiceReconfigService", 3));
                Assert.IsTrue(File.Exists("sessionLength.log"));
                Assert.IsTrue(File.Exists("sessionLength2.log"));
            }
        }

        [TestMethod]
        public void WhenCustomSinkAssemblyNotFound()
        {
            var exc = ExceptionAssertHelper.Throws<ConfigurationException>(() => TraceEventServiceConfiguration.Load("Configurations\\BasicConfigMissingAssembly.xml"));

            StringAssert.Contains(exc.ToString(), "One or more errors occurred when loading the TraceEventService configuration file.");
            StringAssert.Contains(exc.ToString(), "Could not load file or assembly 'Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests' or one of its dependencies. The system cannot find the file specified.");
        }

        [TestMethod]
        public void WhenCustomSinkTypeIsMissing()
        {
            var exc = ExceptionAssertHelper.Throws<ConfigurationException>(() => TraceEventServiceConfiguration.Load("Configurations\\BasicConfigWrongType.xml"));

            StringAssert.Contains(exc.ToString(), "One or more errors occurred when loading the TraceEventService configuration file.");
            StringAssert.Contains(exc.ToString(), "Could not load type 'Microsoft.Practices.EnterpriseLibrary.SemanticLogging.OutProc.Tests.TestObjects.Foo' from assembly 'Microsoft.Practices.EnterpriseLibrary.SemanticLogging.OutProc.Tests'.");
        }

        [TestMethod]
        public void WhenFormatterTypeNotFound()
        {
            var exc = ExceptionAssertHelper.Throws<ConfigurationException>(() => TraceEventServiceConfiguration.Load("Configurations\\WithWrongFormatterParameter2.xml"));

            StringAssert.Contains(exc.ToString(), "One or more errors occurred when loading the TraceEventService configuration file.");
            StringAssert.Contains(exc.ToString(), "Could not load type 'Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Formatters' from assembly 'Microsoft.Practices.EnterpriseLibrary.SemanticLogging'.");
        }    

        [TestMethod]
        public void WhenWrongTypeForFormatter()
        {
            var svcConfiguration = TraceEventServiceConfiguration.Load("Configurations\\WithWrongFormatterParameter.xml", createSinks: false);

            Assert.IsNotNull(svcConfiguration);
        }

        [TestMethod]
        public void WhenUsingWrongFormatter()
        {
            var exc = ExceptionAssertHelper.Throws<ConfigurationException>(() => TraceEventServiceConfiguration.Load("Configurations\\WithWrongFormatter.xml"));

            StringAssert.Contains(exc.ToString(), "One or more errors occurred when loading the TraceEventService configuration file.");
            StringAssert.Contains(exc.ToString(), "Could not load type 'Microsoft.Practices.EnterpriseLibrary.SemanticLogging.OutProc.Tests.CustomFormatterWithWait' from assembly 'Microsoft.Practices.EnterpriseLibrary.SemanticLogging.OutProc.Tests'.");
        }

        [TestMethod]
        public void WhenListenerMissing()
        {
            var exc = ExceptionAssertHelper.Throws<ConfigurationException>(() => TraceEventServiceConfiguration.Load("Configurations\\BasicConfigError.xml"));

            StringAssert.Contains(exc.Message, "One or more errors occurred when loading the TraceEventService configuration file.");
            StringAssert.Contains(exc.ToString(), "Could not load type 'Microsoft.Practices.EnterpriseLibrary.SemanticLogging.OutProc.Tests.TestObjects.Foo' from assembly 'Microsoft.Practices.EnterpriseLibrary.SemanticLogging.OutProc.Tests'.");
        }

        [TestMethod]
        public void WhenOnlyGuidForEventSource()
        {
            var svcConfig = TraceEventServiceConfiguration.Load("Configurations\\BasicConfigOnylGuid.xml");

            Assert.AreEqual(1, svcConfig.SinkSettings[0].EventSources.Count());
            Assert.AreEqual(new Guid("659518be-d338-564b-2759-c63c10ef82e2"), svcConfig.SinkSettings[0].EventSources.First().EventSourceId);
        }

        [TestMethod]
        public void WhenBothNameAndIDSpecified()
        {
            var exc = ExceptionAssertHelper.Throws<ConfigurationException>(() => TraceEventServiceConfiguration.Load("Configurations\\BasicConfigSourceNameAndId.xml"));

            StringAssert.Contains(exc.ToString(), "There is an ambiguity when both name and id are specified. Specify only one value.");
        }

        [TestMethod]
        public void WhenNoEventSourceProperties()
        {
            var exc = ExceptionAssertHelper.Throws<ConfigurationException>(() => TraceEventServiceConfiguration.Load("Configurations\\BasicConfigNoEventSourceIdentifier.xml"));

            StringAssert.Contains(exc.ToString(), "The EventSource 'name' and 'id' values are missing. Please provide either name or id.");
        }

        [TestMethod]
        public void WhenDefaultValues()
        {
            var logger = MockEventSourceOutProc.Logger;

            var svcConfiguration = TraceEventServiceConfiguration.Load("Configurations\\BasicConfig.xml");

            Assert.AreEqual(1, svcConfiguration.SinkSettings[0].EventSources.Count());
            Assert.AreEqual(new Guid("f150d8fb-960c-5e38-a69d-49bae6f97289"), svcConfiguration.SinkSettings[0].EventSources.First().EventSourceId);
            Assert.AreEqual("TestEventSource", svcConfiguration.SinkSettings[0].EventSources.First().Name);
            Assert.AreEqual("consoleSink", svcConfiguration.SinkSettings[0].Name);
            Assert.AreEqual(1, svcConfiguration.SinkSettings[0].EventSources.Count());
            Assert.AreEqual(1, svcConfiguration.SinkSettings.Count);
            Assert.AreEqual("Microsoft-SemanticLogging-Etw", svcConfiguration.Settings.SessionNamePrefix);
        }

        [TestMethod]
        public void WhenOnlyEventSourceName()
        {
            var logger = MockEventSourceOutProc.Logger;

            var svcConfiguration = TraceEventServiceConfiguration.Load("Configurations\\BasicConfigWithNoGuid.xml");

            Assert.AreEqual(1, svcConfiguration.SinkSettings[0].EventSources.Count());
            Assert.AreEqual(MockEventSource.Logger.Guid, svcConfiguration.SinkSettings[0].EventSources.First().EventSourceId);
            Assert.AreEqual("TestEventSource", svcConfiguration.SinkSettings[0].EventSources.First().Name);
            Assert.AreEqual("consoleSink", svcConfiguration.SinkSettings[0].Name);
            Assert.AreEqual(1, svcConfiguration.SinkSettings[0].EventSources.Count());
            Assert.AreEqual(1, svcConfiguration.SinkSettings.Count);
            Assert.AreEqual("Microsoft-SemanticLogging-Etw", svcConfiguration.Settings.SessionNamePrefix);
        }

        [TestMethod]
        public void WhenKeywordsISEmpty()
        {
            var exc = ExceptionAssertHelper.Throws<ConfigurationException>(() => TraceEventServiceConfiguration.Load("Configurations\\EmptyKeyword.xml"));

            StringAssert.Contains(exc.ToString(), "One or more errors occurred when loading the TraceEventService configuration file.");
            StringAssert.Contains(exc.ToString(), "The 'matchAnyKeyword' attribute is invalid - The value '' is invalid according to its datatype");
        }

        [TestMethod]
        public void WhenKeywordISInvalid()
        {
            var exc = ExceptionAssertHelper.Throws<ConfigurationException>(() => TraceEventServiceConfiguration.Load("Configurations\\StringKeyword.xml"));

            StringAssert.Contains(exc.ToString(), "One or more errors occurred when loading the TraceEventService configuration file.");
            StringAssert.Contains(exc.ToString(), "The 'matchAnyKeyword' attribute is invalid - The value 'Database' is invalid according to its datatype 'http://www.w3.org/2001/XMLSchema:long' - The string 'Database' is not a valid Int64 value.");
        }

        [TestMethod]
        public void WhenSessionPrefixIsEmpty()
        {
            var exc = ExceptionAssertHelper.Throws<ConfigurationException>(() => TraceEventServiceConfiguration.Load("Configurations\\ConfigSessionNameEmpty.xml"));

            StringAssert.Contains(exc.ToString(), "The 'sessionNamePrefix' attribute is invalid - The value '' is invalid according to its datatype ");
        }

        [TestMethod]
        public void WhenSourceNameIsEmpty()
        {
            var exc = ExceptionAssertHelper.Throws<ConfigurationException>(() => TraceEventServiceConfiguration.Load("Configurations\\NoEventSourceName.xml"));

            StringAssert.Contains(exc.ToString(), "The EventSource 'name' and 'id' values are missing. Please provide either name or id.");
        }

        [TestMethod]
        public void WhenDuplicateSinkNames()
        {
            var exc = ExceptionAssertHelper.Throws<ConfigurationException>(() => TraceEventServiceConfiguration.Load("Configurations\\TwoSinksSameName.xml"));

            string expectedMessage = "There is a duplicate key sequence 'listener1'";
            StringAssert.Contains(exc.ToString(), expectedMessage);
        }

        [TestMethod]
        public void WhenCustomSinkSchemHasInvalidProperty()
        {
            var exc = ExceptionAssertHelper.Throws<ConfigurationException>(() => TraceEventServiceConfiguration.Load("Configurations\\InnerSchemaValidation.xml"));

            string expectedMessage = "The 'foo' attribute is not declared.";
            StringAssert.Contains(exc.Message, "One or more errors occurred when loading the TraceEventService configuration file.");
            var ex = exc.InnerExceptions.FirstOrDefault(e => e.Message.Contains(expectedMessage));
            Assert.IsNotNull(ex);
            Assert.IsInstanceOfType(ex, typeof(XmlSchemaValidationException));
        }

        [TestMethod]
        public void WhenCustomSinkSchemaHasInvalidPropertyValue()
        {
            var exc = ExceptionAssertHelper.Throws<ConfigurationException>(() => TraceEventServiceConfiguration.Load("Configurations\\ExternalSchemaValidation.xml"));

            StringAssert.Contains(exc.ToString(), "The 'attr' attribute is invalid - The value 'foo' is invalid according to its datatype");
        }

        [TestMethod]
        public void WhenCustomSinkSchemaHasMissingAttribute()
        {
            var exc = ExceptionAssertHelper.Throws<ConfigurationException>(() => TraceEventServiceConfiguration.Load("Configurations\\InnerSchemaValidation2.xml"));

            string expectedMessage = "The required attribute 'name' is missing.";
            StringAssert.Contains(exc.ToString(), expectedMessage);
        }

        [TestMethod]
        public void WhenSinkNameIsDuplicated1()
        {
            var validConnectionString = System.Configuration.ConfigurationManager.ConnectionStrings["valid"].ConnectionString;
            DatabaseHelper.CleanLoggingDB(validConnectionString);
            var logger = MockEventSourceOutProc.Logger;

            EventSourceSettings settings = new EventSourceSettings("MockEventSourceOutProc", null, EventLevel.LogAlways);
            var subject = new EventEntrySubject();
            subject.LogToSqlDatabase("testInstance", validConnectionString, "Traces", TimeSpan.FromSeconds(10), 200);
            SinkSettings sinkSettings = new SinkSettings("dbSink", subject, new List<EventSourceSettings>() { { settings } });
            var subject2 = new EventEntrySubject();
            subject2.LogToSqlDatabase("testInstance", validConnectionString, "Traces", TimeSpan.FromSeconds(10), 200);
            SinkSettings sinkSettings2 = new SinkSettings("dbSink", subject2, new List<EventSourceSettings>() { { settings } });
            List<SinkSettings> sinks = new List<SinkSettings>() { sinkSettings, sinkSettings2 };
            var exc = ExceptionAssertHelper.
                Throws<Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw.Configuration.ConfigurationException>(
                            () => new TraceEventServiceConfiguration(sinks));

            StringAssert.Contains(exc.ToString(), "Duplicate sinks");
        }
    }
}
