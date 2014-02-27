// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.Sinks
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading;

    using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Sinks;
    using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.TestSupport;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    [TestClass]
    public class given_elasticsearch_configuration
    {
        private const string DevelopmentElasticSearchEndpoint = "http://localhost:9200";

        [TestMethod]
        public void when_creating_sink_for_null_connection_string_then_throws()
        {
            AssertEx.Throws<ArgumentNullException>(() => new ElasticSearchSink("instanceName", null, "logstash", "etw", TimeSpan.FromSeconds(1), 10000, Timeout.InfiniteTimeSpan));
        }

        [TestMethod]
        public void when_creating_sink_with_invalid_connection_string_then_throws()
        {
            AssertEx.Throws<UriFormatException>(() => new ElasticSearchSink("instanceName", "InvalidConnection", "logstash", "etw", TimeSpan.FromSeconds(1), 10000, Timeout.InfiniteTimeSpan));
        }

        [TestMethod]
        public void when_creating_sink_with_small_buffer_size_then_throws()
        {
            AssertEx.Throws<ArgumentException>(() => new ElasticSearchSink("instanceName", DevelopmentElasticSearchEndpoint, "logstash", "etw", TimeSpan.FromSeconds(1), 10, Timeout.InfiniteTimeSpan));
        }

        [TestMethod]
        public void when_creating_sink_with_invalid_character_in_index_then_throws()
        {
            // Invalid index name characters
            var testInvalidCharacters = new[] { '\\', '/', ' ', 'U', ',', '"', '*', '?', '|', '<', '>' };

            foreach (var invalidChar in testInvalidCharacters)
            {
                AssertEx.Throws<ArgumentException>(() => new ElasticSearchSink("instanceName", DevelopmentElasticSearchEndpoint, string.Format("{0}testindex", invalidChar), "etw", TimeSpan.FromSeconds(1), 10000, Timeout.InfiniteTimeSpan));
            }

            foreach (var invalidChar in testInvalidCharacters)
            {
                AssertEx.Throws<ArgumentException>(() => new ElasticSearchSink("instanceName", DevelopmentElasticSearchEndpoint, string.Format("test{0}index", invalidChar), "etw", TimeSpan.FromSeconds(1), 10000, Timeout.InfiniteTimeSpan));
            }

            foreach (var invalidChar in testInvalidCharacters)
            {
                AssertEx.Throws<ArgumentException>(() => new ElasticSearchSink("instanceName", DevelopmentElasticSearchEndpoint, string.Format("testindex{0}", invalidChar), "etw", TimeSpan.FromSeconds(1), 10000, Timeout.InfiniteTimeSpan));
            }
        }
    }

    [TestClass]
    public class given_elasticsearch_event_entry
    {
        [TestMethod]
        public void when_serializing_a_log_entry_then_object_can_serialize()
        {
            var payload = new Dictionary<string, object> { { "msg", "the message" }, { "date", DateTime.UtcNow } };
            var logObject = new JsonEventEntry
            {
                EventDate = DateTime.UtcNow,
                Payload = payload,
                InstanceName = "instance"
            };
            var logEntry = new ElasticSearchLogEntry { Index = "log", Type = "slab", LogEntry = logObject };

            var actual = JsonConvert.SerializeObject(logEntry);

            Assert.IsNotNull(actual);
            Assert.IsTrue(this.IsValidBulkMessage(actual));
        }

        [TestMethod]
        public void when_serializing_concatenating_serialized_entries_then_they_are_valid_bulk_message()
        {
            // Note: converting an array does not create valid message for use in elasticsearch bulk operation

            var bulkMessage = new StringBuilder();
            bulkMessage.Append(JsonConvert.SerializeObject(new ElasticSearchLogEntry { Index = "log", Type = "slab", LogEntry = CreateJsonEventEntry() }));
            bulkMessage.Append(JsonConvert.SerializeObject(new ElasticSearchLogEntry { Index = "log", Type = "slab", LogEntry = CreateJsonEventEntry() }));

            var messages = bulkMessage.ToString();

            Assert.IsNotNull(messages);
            Assert.IsTrue(this.IsValidBulkMessage(messages));
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

        private bool IsValidBulkMessage(string message)
        {
            // Ignores additional newlines when we split which is fine
            // Note: Except between the header/body documents which we may want to validate
            var entries = message.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

            // If we don't have at least two items then return false
            if (entries.Length < 2)
            {
                return false;
            }

            bool isHeader = true;
            foreach (var entry in entries)
            {
                var entryObject = JObject.Parse(entry);
                if (isHeader)
                {
                    // Check to see if this is an index header object
                    if (entryObject["index"]["_index"] == null)
                    {
                        return false;
                    }
                }
                else
                {
                    // Simple check to see if we have one of our common properties
                    // The body/document just needs to be valid json which we were able to successfully parse
                    if (entryObject["EventId"] == null)
                    {
                        return false;
                    }
                }

                //Every other entry separated by newline should be a header
                isHeader = !isHeader;
            }

            // Finally, the last item seen should not be header
            return isHeader;
        }
    }
}