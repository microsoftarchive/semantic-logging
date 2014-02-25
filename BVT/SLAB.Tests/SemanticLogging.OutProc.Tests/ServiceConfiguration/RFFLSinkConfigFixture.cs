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
    public class RFFLSinkConfigFixture
    {
        [TestMethod]
        public void WhenConfigIsValidAndComplete()
        {
            var svcConfiguration = TraceEventServiceConfiguration.Load("Configurations\\RollingFlatFile\\RollingFlatFile.xml");

            Assert.IsNotNull(svcConfiguration);
        }

        [TestMethod]
        public void WhenNoFormatterSpecified()
        {
            var svcConfiguration = TraceEventServiceConfiguration.Load("Configurations\\RollingFlatFile\\RollingFlatFileNoFormatter.xml");

            Assert.IsNotNull(svcConfiguration);
        }

        [TestMethod]
        public void WhenEmptyFileName()
        {
            var exc = ExceptionAssertHelper.Throws<ConfigurationException>(() => TraceEventServiceConfiguration.Load("Configurations\\RollingFlatFile\\RollingFlatFileEmptyFileName.xml"));

            StringAssert.Contains(exc.ToString(), "The 'fileName' attribute is invalid - The value '' is invalid according to its datatype ");
        }

        [TestMethod]
        public void WhenEmptyRollFileExistsBehavior()
        {
            var exc = ExceptionAssertHelper.Throws<ConfigurationException>(() => TraceEventServiceConfiguration.Load("Configurations\\RollingFlatFile\\RollingFlatFileEmptyRollFileExistsBehavior.xml"));

            string expectedMessage = "The 'rollFileExistsBehavior' attribute is invalid - The value '' is invalid according to its datatype";
            StringAssert.Contains(exc.ToString(), expectedMessage);
        }

        [TestMethod]
        public void WhenEmptyRollInterval()
        {
            var exc = ExceptionAssertHelper.Throws<ConfigurationException>(() => TraceEventServiceConfiguration.Load("Configurations\\RollingFlatFile\\RollingFlatFileEmptyRollInterval.xml"));

            string expectedMessage = "The 'rollInterval' attribute is invalid - The value '' is invalid according to its datatype";
            StringAssert.Contains(exc.ToString(), expectedMessage);
        }

        [TestMethod]
        public void WhenEmptyRollSizeKB()
        {
            var exc = ExceptionAssertHelper.Throws<ConfigurationException>(() => TraceEventServiceConfiguration.Load("Configurations\\RollingFlatFile\\RollingFlatFileEmptyRollSizeKB.xml"));

            string expectedMessage = "The 'rollSizeKB' attribute is invalid - The value '' is invalid according to its datatype";
            StringAssert.Contains(exc.ToString(), expectedMessage);
        }

        [TestMethod]
        public void WhenEmptyTimeStampPattern()
        {
            var exc = ExceptionAssertHelper.Throws<ConfigurationException>(() => TraceEventServiceConfiguration.Load("Configurations\\RollingFlatFile\\RollingFlatFileEmptyTimeStampPattern.xml"));
            
            string expectedMessage = "The 'timeStampPattern' attribute is invalid - The value '' is invalid according to its datatype";
            StringAssert.Contains(exc.ToString(), expectedMessage);
        }

        [TestMethod]
        public void WhenEmptyMaxArchiveFiles()
        {
            var exc = ExceptionAssertHelper.Throws<ConfigurationException>(() => TraceEventServiceConfiguration.Load("Configurations\\RollingFlatFile\\RollingFlatFileEmptyMaxArchiveFiles.xml"));

            string expectedMessage = "The 'maxArchivedFiles' attribute is invalid - The value '' is invalid according to its datatype";
            StringAssert.Contains(exc.ToString(), expectedMessage);
        }

        [TestMethod]
        public void WhenOnlyMandatoryProperties()
        {
            var svcConfiguration = TraceEventServiceConfiguration.Load("Configurations\\RollingFlatFile\\RollingFlatFileMissingParams.xml");

            Assert.IsNotNull(svcConfiguration);
        }
    }
}
