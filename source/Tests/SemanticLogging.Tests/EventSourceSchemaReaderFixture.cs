// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System.Diagnostics.Tracing;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Schema;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.TestObjects;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.Schema
{
    [TestClass]
    public class EventSourceSchemaReaderFixture
    {
        [TestMethod]
        public void when_parsing_schema_then_can_read_payload_argument_names()
        {
            var reader = new EventSourceSchemaReader();

            var schema = reader.GetSchema(SimpleEventSource.Log);

            Assert.AreEqual("event1Arg0", schema[1].Payload[0]);
            Assert.AreEqual("event1Arg1", schema[1].Payload[1]);

            Assert.AreEqual("event2Arg0", schema[2].Payload[0]);
            Assert.AreEqual("event2Arg1", schema[2].Payload[1]);

            Assert.AreEqual("event3Arg0", schema[3].Payload[0]);
            Assert.AreEqual("event3Arg1", schema[3].Payload[1]);
            Assert.AreEqual("event3Arg2", schema[3].Payload[2]);
        }

        [TestMethod]
        public void when_parsing_schema_then_can_read_task_names()
        {
            var reader = new EventSourceSchemaReader();

            var schema = reader.GetSchema(SimpleEventSource.Log);

            Assert.AreEqual("MyEvent1", schema[1].TaskName);
            Assert.AreEqual("MyEvent2", schema[2].TaskName);
            Assert.IsNull(schema[3].TaskName);
        }

        [TestMethod]
        public void when_parsing_schema_then_reads_provider_name()
        {
            var reader = new EventSourceSchemaReader();

            var schema = reader.GetSchema(SimpleEventSource.Log);

            Assert.AreEqual("SimpleEventSource-CustomName", schema[1].ProviderName);
            Assert.AreEqual("SimpleEventSource-CustomName", schema[2].ProviderName);
        }

        [TestMethod]
        public void when_parsing_schema_then_can_read_events_with_and_without_tasks()
        {
            var reader = new EventSourceSchemaReader();
            var schema = reader.GetSchema(MyCompanyEventSource.Log);

            Assert.AreEqual<string>("Failure", schema[1].TaskName);

            Assert.AreEqual<string>("Startup", schema[2].TaskName);

            Assert.AreEqual<string>("Page", schema[3].TaskName);
            Assert.AreEqual<EventTask>(MyCompanyEventSource.Tasks.Page, schema[3].Task);

            Assert.AreEqual<string>("Page", schema[4].TaskName);
            Assert.AreEqual<EventTask>(MyCompanyEventSource.Tasks.Page, schema[4].Task);

            Assert.AreEqual<string>("DBQuery", schema[5].TaskName);
            Assert.AreEqual<EventTask>(MyCompanyEventSource.Tasks.DBQuery, schema[5].Task);

            Assert.AreEqual<string>("DBQuery", schema[6].TaskName);
            Assert.AreEqual<EventTask>(MyCompanyEventSource.Tasks.DBQuery, schema[6].Task);

            Assert.AreEqual<string>("Mark", schema[7].TaskName);

            Assert.AreEqual<string>("LogColor", schema[8].TaskName);
            Assert.AreEqual<EventTask>((EventTask)65526, schema[8].Task);

            Assert.IsNull(null, schema[9].TaskName);
            Assert.AreEqual<EventTask>((EventTask)0, schema[9].Task);
        }

        [TestMethod]
        public void when_parsing_schema_then_can_read_events_with_and_without_opcodes()
        {
            var reader = new EventSourceSchemaReader();
            var schema = reader.GetSchema(SimpleEventSource.Log);

            Assert.AreEqual<string>("Info", schema[1].OpcodeName);
            Assert.AreEqual<EventOpcode>(EventOpcode.Info, schema[1].Opcode);

            Assert.AreEqual<string>("Info", schema[4].OpcodeName);
            Assert.AreEqual<EventOpcode>(EventOpcode.Info, schema[4].Opcode);

            Assert.AreEqual<string>("Start", schema[5].OpcodeName);
            Assert.AreEqual<EventOpcode>(EventOpcode.Start, schema[5].Opcode);

            Assert.AreEqual<string>("Stop", schema[6].OpcodeName);
            Assert.AreEqual<EventOpcode>(EventOpcode.Stop, schema[6].Opcode);

            Assert.AreEqual<string>("DC_Start", schema[7].OpcodeName);
            Assert.AreEqual<EventOpcode>(EventOpcode.DataCollectionStart, schema[7].Opcode);

            Assert.AreEqual<string>("DC_Stop", schema[8].OpcodeName);
            Assert.AreEqual<EventOpcode>(EventOpcode.DataCollectionStop, schema[8].Opcode);

            Assert.AreEqual<string>("Extension", schema[9].OpcodeName);
            Assert.AreEqual<EventOpcode>(EventOpcode.Extension, schema[9].Opcode);

            Assert.AreEqual<string>("Reply", schema[10].OpcodeName);
            Assert.AreEqual<EventOpcode>(EventOpcode.Reply, schema[10].Opcode);

            Assert.AreEqual<string>("Resume", schema[11].OpcodeName);
            Assert.AreEqual<EventOpcode>(EventOpcode.Resume, schema[11].Opcode);

            Assert.AreEqual<string>("Suspend", schema[12].OpcodeName);
            Assert.AreEqual<EventOpcode>(EventOpcode.Suspend, schema[12].Opcode);

            Assert.AreEqual<string>("Send", schema[13].OpcodeName);
            Assert.AreEqual<EventOpcode>(EventOpcode.Send, schema[13].Opcode);

            Assert.AreEqual<string>("Receive", schema[14].OpcodeName);
            Assert.AreEqual<EventOpcode>(EventOpcode.Receive, schema[14].Opcode);
        }

        [TestMethod]
        public void when_parsing_schema_then_can_read_complex_payloads()
        {
            var reader = new EventSourceSchemaReader();
            var schema = reader.GetSchema(MyCompanyEventSource.Log);

            // Failure
            Assert.AreEqual<string>("message", schema[1].Payload[0]);

            // PageStart
            Assert.AreEqual<string>("id", schema[3].Payload[0]);
            Assert.AreEqual<string>("url", schema[3].Payload[1]);

            // PageStop
            Assert.AreEqual<string>("id", schema[4].Payload[0]);

            // DBQueryStart
            Assert.AreEqual<string>("sqlQuery", schema[5].Payload[0]);

            // Mark
            Assert.AreEqual<string>("id", schema[7].Payload[0]);

            // LogColor
            Assert.AreEqual<string>("color", schema[8].Payload[0]);
        }

        [TestMethod]
        public void can_parse_custom_opcodes()
        {
            var reader = new EventSourceSchemaReader();
            var schema = reader.GetSchema(SimpleEventSource.Log);

            Assert.AreEqual<string>("CustomOpcode1", schema[15].OpcodeName);
            Assert.AreEqual<EventOpcode>((EventOpcode)100, schema[15].Opcode);

            Assert.AreEqual<string>("CustomOpcode2", schema[16].OpcodeName);
            Assert.AreEqual<EventOpcode>((EventOpcode)101, schema[16].Opcode);
        }

        //[TestMethod]
        //public void can_parse_messages()
        //{
        //    var reader = new EventSourceSchemaReader();
        //    var schema = reader.GetSchema(MyCompanyEventSource.Log);

        //    Assert.AreEqual("Application Failure: {0}", schema[1].MessageFormat);
        //    Assert.AreEqual("Starting up.", schema[2].MessageFormat);
        //    Assert.AreEqual("loading page {1} activityID={0}", schema[3].MessageFormat);
        //    Assert.IsNull(schema[4].MessageFormat);
        //}

        [TestMethod]
        public void can_parse_keywords()
        {
            var reader = new EventSourceSchemaReader();
            var schema = reader.GetSchema(MyCompanyEventSource.Log);

            Assert.AreEqual(MyCompanyEventSource.Keywords.Diagnostic, schema[1].Keywords);
            Assert.AreEqual("Diagnostic", schema[1].KeywordsDescription);

            Assert.AreEqual(MyCompanyEventSource.Keywords.Perf, schema[2].Keywords);
            Assert.AreEqual("Perf", schema[2].KeywordsDescription);

            Assert.AreEqual(MyCompanyEventSource.Keywords.Page, schema[3].Keywords);
            Assert.AreEqual("Page", schema[3].KeywordsDescription);

            Assert.AreEqual(MyCompanyEventSource.Keywords.DataBase, schema[5].Keywords);
            Assert.AreEqual("DataBase", schema[5].KeywordsDescription);

            Assert.AreEqual((EventKeywords)0, schema[8].Keywords);
            Assert.IsNull(schema[8].KeywordsDescription);

            Assert.AreEqual(MyCompanyEventSource.Keywords.DataBase | MyCompanyEventSource.Keywords.Perf, schema[10].Keywords);
            Assert.AreEqual("DataBase Perf", schema[10].KeywordsDescription);
        }

        [TestMethod]
        public void can_parse_level()
        {
            var reader = new EventSourceSchemaReader();
            var schema = reader.GetSchema(SimpleEventSource.Log);

            Assert.AreEqual(EventLevel.LogAlways, schema[21].Level);
            Assert.AreEqual(EventLevel.Critical, schema[22].Level);
            Assert.AreEqual(EventLevel.Error, schema[23].Level);
            Assert.AreEqual(EventLevel.Warning, schema[24].Level);
            Assert.AreEqual(EventLevel.Informational, schema[25].Level);
            Assert.AreEqual(EventLevel.Verbose, schema[26].Level);
        }

        [TestMethod]
        public void can_parse_version()
        {
            var reader = new EventSourceSchemaReader();
            var schema = reader.GetSchema(SimpleEventSource.Log);

            Assert.AreEqual(1, schema[1].Version);
            Assert.AreEqual(0, schema[2].Version);
        }

        [TestMethod]
        [ExpectedException(typeof(System.ArgumentNullException))]
        public void when_receiving_null_source_throws()
        {
            new EventSourceSchemaReader().GetSchema((EventSource)null);
        }

        [TestMethod]
        [ExpectedException(typeof(System.ArgumentNullException))]
        public void when_receiving_null_manifest_throws()
        {
            new EventSourceSchemaReader().GetSchema((string)null);
        }
    }
}
