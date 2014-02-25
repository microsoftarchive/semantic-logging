// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw.Configuration;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.OutProc.Tests.TestObjects;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.Shared.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.OutProc.Tests.ServiceConfiguration
{
    [TestClass]
    public class ConsoleTraceEventServiceConfigurationFixture
    {
        [TestMethod]
        public void OutProcConsole()
        {
            var logger = MockEventSourceOutProc.Logger;
            MockConsoleOutput mockConsole = new MockConsoleOutput();

            var svcConfiguration = TraceEventServiceConfiguration.Load("Configurations\\Console\\Console.xml");
            using (TraceEventService collector = new TraceEventService(svcConfiguration))
            {
                collector.Start();

                for (int n = 0; n < 10; n++)
                {
                    logger.LogSomeMessage("some message to console " + n.ToString() + ".");
                }
            }
        }

        [TestMethod]
        public void OutProcConsoleFormatterWithEmptyDateTime()
        {
            MockConsoleOutput mockConsole = new MockConsoleOutput();

            var exc = ExceptionAssertHelper.Throws<ConfigurationException>(() => TraceEventServiceConfiguration.Load("Configurations\\Console\\ConsoleEmptyDateTimeFormat.xml"));

            StringAssert.Contains(exc.ToString(), "The 'dateTimeFormat' attribute is invalid - The value '' is invalid according to its datatype ");
        }

        [TestMethod]
        public void OutProcConsoleFormatterNoParam()
        {
            var logger = MockEventSourceOutProc.Logger;
            MockConsoleOutput mockConsole = new MockConsoleOutput();            

            var svcConfiguration = TraceEventServiceConfiguration.Load("Configurations\\Console\\ConsoleFormatterNoParam.xml");

            Assert.IsNotNull(svcConfiguration);
        }

        [TestMethod]
        public void OutProcConsoleNoParams()
        {
            var logger = MockEventSourceOutProc.Logger;
            MockConsoleOutput mockConsole = new MockConsoleOutput();

            var svcConfiguration = TraceEventServiceConfiguration.Load("Configurations\\Console\\ConsoleNoParams.xml");

            Assert.IsNotNull(svcConfiguration);
        }
    }
}
