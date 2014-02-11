using System;
using System.Collections.Generic;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Sinks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.Sinks
{
    [TestClass]
    public class DescribeElasticSearchSink
    {
        //[TestMethod]
        //public void ItShouldReturnInsertedItems()
        //{
        //    var sink = new ElasticSearchSink("instance", "localhost.", 9200, TimeSpan.FromSeconds(1), 600,
        //        TimeSpan.FromMinutes(1));

        //    var count = sink.PublishEventsAsync(new[] {CreateJson(), CreateJson(), CreateJson()}).Result;
        //    Assert.AreEqual(count, 3);
        //}


        [TestMethod]
        public void ItShouldSerializeLogEntryObject()
        {
            // g
            var payload = new Dictionary<string, object> { { "msg", "the message" }, { "date", DateTime.UtcNow } };
            var logObject = new JsonEventEntry
            {
                EventDate = DateTime.UtcNow,
                Payload = payload,
                InstanceName = "instance"
            };
            var logEntry = new ElasticSearchLogEntry { Index = "log", Type = "slab", LogEntry = logObject };

            // w
            var actual = JsonConvert.SerializeObject(logEntry);

            // t
            Assert.IsNotNull(actual);
        }

        [TestMethod]
        public void ItShouldSerializeLogEntryObjects()
        {
            // g
            var logObject = CreateJson();
            var logEntry1 = new ElasticSearchLogEntry { Index = "log", Type = "slab", LogEntry = logObject };
            var logEntry2 = new ElasticSearchLogEntry { Index = "log", Type = "slab", LogEntry = logObject };

            // w
            var actual = JsonConvert.SerializeObject(new[] { logEntry1, logEntry2 });

            // t
            Assert.IsNotNull(actual);
        }

        private static JsonEventEntry CreateJson()
        {
            var payload = new Dictionary<string, object> {{"msg", "the message"}, {"date", DateTime.UtcNow}};
            var logObject = new JsonEventEntry
            {
                EventDate = DateTime.UtcNow,
                Payload = payload,
                InstanceName = "instance"
            };
            return logObject;
        }
    }
}