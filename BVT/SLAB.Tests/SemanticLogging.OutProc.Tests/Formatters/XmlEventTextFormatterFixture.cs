// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw.Configuration;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.OutProc.Tests.TestObjects;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.Shared.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.OutProc.Tests.Formatters
{
    [TestClass]
    public class XmlEventTextFormatterFixture
    {
        [TestMethod]
        public void WhenUsingXmlFormatterInIntended()
        {
            string fileName = "FlatFileXmlFormatterIndentedOutProc.log";
            File.Delete(fileName);
            var logger = MockEventSourceOutProc.Logger;

            IEnumerable<XElement> entries;
            using (var svcConfiguration = TraceEventServiceConfiguration.Load("Configurations\\WithFormatter\\FlatFileXmlFormatterIndentedOutProc.xml"))
            using (var evtService = new TraceEventService(svcConfiguration))
            {
                evtService.Start();
                try
                {
                    logger.LogSomeMessage("logging using xml Formatter indented");
                    entries = FlatFileHelper.PollUntilXmlEventsAreWritten(fileName, 1);
                }
                finally
                {
                    evtService.Stop();
                }
            }

            XmlFormattedEntry.Fill(entries.Single());
            Assert.AreEqual<Guid>(MockEventSourceOutProc.Logger.Guid, Guid.Parse(XmlFormattedEntry.Provider.Attribute("Guid").Value));
            Assert.AreEqual<int>(8, Convert.ToInt32(XmlFormattedEntry.EventId.Value));
            Assert.AreEqual<byte>(0, Convert.ToByte(XmlFormattedEntry.Version.Value));
            Assert.AreEqual<int>((int)EventLevel.Informational, Int32.Parse(XmlFormattedEntry.Level.Value));
            Assert.AreEqual<int>(65526, Int32.Parse(XmlFormattedEntry.Task.Value));
            Assert.AreEqual<long>(0, Int64.Parse(XmlFormattedEntry.Keywords.Value.Replace("0x", string.Empty)));
            Assert.AreEqual<int>(0, Int32.Parse(XmlFormattedEntry.Opcode.Value));
            Assert.AreEqual(1, XmlFormattedEntry.Payload.Elements().Count());
            Assert.AreEqual("message", XmlFormattedEntry.Payload.Elements().First().Attribute("Name").Value);
            Assert.AreEqual("logging using xml Formatter indented", XmlFormattedEntry.Payload.Elements().First().Value);
        }

        [TestMethod]
        public void WhenUsingXmlFormatterWithInvalidDateTimeFormate()
        {
            var exc = ExceptionAssertHelper.Throws<ConfigurationException>(() => TraceEventServiceConfiguration.Load("Configurations\\WithFormatter\\FlatFileXmlWrongDateTime.xml"));

            StringAssert.Contains(exc.InnerException.Message, "The date time format is invalid.");
        }

        [TestMethod]
        public void WhenEventWithGuidPramLoggedInXml()
        {
            string fileName = "FlatFileXmlFormatterAndGuids.log";
            File.Delete(fileName);
            var logger = MockEventSourceOutProcEnum.Logger;

            var testGuid = Guid.NewGuid();
            IEnumerable<XElement> entries;
            using (var svcConfiguration = TraceEventServiceConfiguration.Load("Configurations\\WithFormatter\\FlatFileXmlFormatterAndGuids.xml"))
            using (var evtService = new TraceEventService(svcConfiguration))
            {
                evtService.Start();
                try
                {
                    logger.SaveExpenseStarted(testGuid);
                    entries = FlatFileHelper.PollUntilXmlEventsAreWritten(fileName, 1);
                }
                finally
                {
                    evtService.Stop();
                }
            }

            Assert.AreEqual(1, entries.Count());
            XmlFormattedEntry.Fill(entries.Single());
            Assert.AreEqual<Guid>(MockEventSourceOutProcEnum.Logger.Guid, Guid.Parse(XmlFormattedEntry.Provider.Attribute("Guid").Value));
            Assert.AreEqual<int>(4, Convert.ToInt32(XmlFormattedEntry.EventId.Value));
            StringAssert.Contains(XmlFormattedEntry.Payload.ToString(), testGuid.ToString());
        }

        [TestMethod]
        public void WhenEnumsInPayloadInXml()
        {
            string fileName = "FlatFileXmlFormatterAndEnums.log";
            File.Delete(fileName);
            var logger = MockEventSourceOutProcEnum.Logger;

            IEnumerable<XElement> entries;
            using (var svcConfiguration = TraceEventServiceConfiguration.Load("Configurations\\WithFormatter\\FlatFileXmlFormatterAndEnums.xml"))
            using (var evtService = new TraceEventService(svcConfiguration))
            {
                evtService.Start();
                try
                {
                    logger.SendEnumsEvent16(MockEventSourceOutProcEnum.MyColor.Red, MockEventSourceOutProcEnum.MyFlags.Flag3);
                    entries = FlatFileHelper.PollUntilXmlEventsAreWritten(fileName, 1);
                }
                finally
                {
                    evtService.Stop();
                }
            }

            Assert.AreEqual(1, entries.Count());
            XmlFormattedEntry.Fill(entries.Single());
            Assert.AreEqual<Guid>(MockEventSourceOutProcEnum.Logger.Guid, Guid.Parse(XmlFormattedEntry.Provider.Attribute("Guid").Value));
            Assert.AreEqual<int>(3, Convert.ToInt32(XmlFormattedEntry.EventId.Value));
            Assert.AreEqual<byte>(0, Convert.ToByte(XmlFormattedEntry.Version.Value));
            Assert.AreEqual<int>((int)EventLevel.Informational, Int32.Parse(XmlFormattedEntry.Level.Value));
            Assert.AreEqual<int>((int)EventTask.None, Int32.Parse(XmlFormattedEntry.Task.Value));
            Assert.AreEqual<long>(0, Int64.Parse(XmlFormattedEntry.Keywords.Value.Replace("0x", string.Empty)));
            Assert.AreEqual<int>((int)EventOpcode.Resume, Int32.Parse(XmlFormattedEntry.Opcode.Value));
            Assert.AreEqual(2, XmlFormattedEntry.Payload.Elements().Count());
            StringAssert.Contains(XmlFormattedEntry.Payload.ToString(), @"<Data Name=""a"">" + ((int)MockEventSourceOutProcEnum.MyColor.Red).ToString() + "</Data>");
            StringAssert.Contains(XmlFormattedEntry.Payload.ToString(), @"<Data Name=""b"">" + ((int)MockEventSourceOutProcEnum.MyFlags.Flag3).ToString() + "</Data>");
        }

        [TestMethod]
        public void WhenNotIntendedInXml()
        {
            string fileName = "FlatFileXmlFormatterOutProc.log";
            File.Delete(fileName);
            var logger = MockEventSourceOutProc.Logger;

            IEnumerable<XElement> entries;
            using (var svcConfiguration = TraceEventServiceConfiguration.Load("Configurations\\WithFormatter\\FlatFileXmlFormatterOutProc.xml"))
            using (var evtService = new TraceEventService(svcConfiguration))
            {
                evtService.Start();
                try
                {
                    logger.LogSomeMessage("logging using xml Formatter not indented");
                    entries = FlatFileHelper.PollUntilXmlEventsAreWritten(fileName, 1);
                }
                finally
                {
                    evtService.Stop();
                }
            }

            Assert.AreEqual(1, entries.Count());
            XmlFormattedEntry.Fill(entries.Single());
            Assert.AreEqual<Guid>(MockEventSourceOutProc.Logger.Guid, Guid.Parse(XmlFormattedEntry.Provider.Attribute("Guid").Value));
            Assert.AreEqual<int>(8, Convert.ToInt32(XmlFormattedEntry.EventId.Value));
            Assert.AreEqual<byte>(0, Convert.ToByte(XmlFormattedEntry.Version.Value));
            Assert.AreEqual<int>((int)EventLevel.Informational, Int32.Parse(XmlFormattedEntry.Level.Value));
            Assert.AreEqual<int>(65526, Int32.Parse(XmlFormattedEntry.Task.Value));
            Assert.AreEqual<long>(0, Int64.Parse(XmlFormattedEntry.Keywords.Value.Replace("0x", string.Empty)));
            Assert.AreEqual<int>(0, Int32.Parse(XmlFormattedEntry.Opcode.Value));
            Assert.AreEqual(1, XmlFormattedEntry.Payload.Elements().Count());
            Assert.AreEqual("message", XmlFormattedEntry.Payload.Elements().First().Attribute("Name").Value);
            Assert.AreEqual("logging using xml Formatter not indented", XmlFormattedEntry.Payload.Elements().First().Value);
        }

        [TestMethod]
        public void WhenCustomDateTimeFormatInXml()
        {
            string fileName = "FlatFileXmlFormatterDateTimeFormat.log";
            File.Delete(fileName);
            var logger = MockEventSourceOutProc.Logger;

            IEnumerable<XElement> entries;
            using (var svcConfiguration = TraceEventServiceConfiguration.Load("Configurations\\WithFormatter\\FlatFileXmlFormatterDateTimeFormat.xml"))
            using (var evtService = new TraceEventService(svcConfiguration))
            {
                evtService.Start();
                try
                {
                    logger.LogSomeMessage("logging using xml Formatter not indented");
                    entries = FlatFileHelper.PollUntilXmlEventsAreWritten(fileName, 1);
                }
                finally
                {
                    evtService.Stop();
                }
            }

            Assert.AreEqual(1, entries.Count());
            XmlFormattedEntry.Fill(entries.Single());
            var dt = DateTime.UtcNow;
            string expectedTimestamp = dt.Day.ToString() + dt.Month.ToString() + dt.Year.ToString();
            StringAssert.Contains(XmlFormattedEntry.TimeCreated.ToString(), @"SystemTime=""" + expectedTimestamp + @"""");
        }
    }
}
