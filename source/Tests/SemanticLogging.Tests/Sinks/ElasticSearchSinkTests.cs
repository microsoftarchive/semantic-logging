// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.Sinks
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Sinks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;

    [TestClass]
    public class given_elasticsearch_configuration
    {
        private const string DevelopmentElasticSearchEndpoint = "http://localhost:9200";

        [TestMethod]
        public void when_serializing_a_log_entry_then_object_can_serialize()
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
        public void when_serializing_multiple_log_entries_then_objects_can_serialize()
        {
            // g
            var logObject = CreateJsonEventEntry();
            var logEntry1 = new ElasticSearchLogEntry { Index = "log", Type = "slab", LogEntry = logObject };
            var logEntry2 = new ElasticSearchLogEntry { Index = "log", Type = "slab", LogEntry = logObject };

            // w
            var actual = JsonConvert.SerializeObject(new[] { logEntry1, logEntry2 });

            // t
            Assert.IsNotNull(actual);
        }

        private static JsonEventEntry CreateJsonEventEntry()
        {
            var payload = new Dictionary<string, object> { { "msg", "the message" }, { "date", DateTime.UtcNow } };
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