// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.Sinks
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Threading;

    using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Sinks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    [TestClass]
    public class given_empty_index
    {
        // These tests will delete data in the provided elasticsearch endpoint
        protected readonly string DevelopmentElasticSearchUrl = "http://localhost:9200";

        protected readonly string TestIndex = "slabtest";

        [TestInitialize]
        public void Setup()
        {
            // Delete data in the text index(s)
            DeleteIndex();
        }

        protected void DeleteIndex(string indexName = null)
        {
            indexName = indexName ?? TestIndex + "*";

            var client = new HttpClient { BaseAddress = new Uri(DevelopmentElasticSearchUrl) };

            client.DeleteAsync(indexName).Wait();
        }

        protected int GetIndexCount(string indexName = null)
        {
            var client = new HttpClient { BaseAddress = new Uri(DevelopmentElasticSearchUrl) };

            var operation = string.Format("{0}/_count", indexName ?? TestIndex + "*");

            var response = client.GetStringAsync(operation).Result;

            return JObject.Parse(response)["count"].Value<int>();
        }

        protected QueryResult QueryAllEntriesByIndex(string indexName = null)
        {
            var client = new HttpClient { BaseAddress = new Uri(DevelopmentElasticSearchUrl) };

            var operation = string.Format("{0}/_search?q=*", indexName ?? TestIndex + "*");

            var resultString = client.GetStringAsync(operation).Result;

            var result = JsonConvert.DeserializeObject<QueryResult>(resultString);

            return result;
        }

        protected JsonEventEntry CreateEventEntry(string msgPropertyValue)
        {
            var payload = new Dictionary<string, object> { { "msg", msgPropertyValue }, { "date", DateTime.UtcNow } };
            var logObject = new JsonEventEntry
            {
                EventDate = DateTime.UtcNow,
                Payload = payload,
                InstanceName = "instance"
            };
            return logObject;
        }

        [TestCleanup]
        protected void Cleanup()
        {
            DeleteIndex();
        }
    }

    [TestClass]
    public class when_writing_multiple_entries : given_empty_index
    {
        [TestMethod]
        public void then_correct_count_is_returned_and_all_entries_and_properties_are_written()
        {
            //These are writing to the property "msg"
            var msgPropValues = new[] { "1", "2", "3" };
            var eventEntries = msgPropValues.Select(this.CreateEventEntry);

            var sink = new ElasticSearchSink("instance", DevelopmentElasticSearchUrl, TestIndex, "etw", TimeSpan.FromSeconds(1), 600,
                TimeSpan.FromMinutes(1));

            var count = sink.PublishEventsAsync(eventEntries).Result;
            sink.FlushAsync().Wait(TimeSpan.FromSeconds(45));

            Assert.AreEqual(count, 3);

            // Check the index count until it's what we expect or we have checked too many times
            for (int i = 0; i < 6; i++)
            {
                Thread.Sleep(500);
                if (this.GetIndexCount() > 2)
                {
                    break;
                }
            }

            // Query across the _all index
            var results = this.QueryAllEntriesByIndex();

            // Compare the message property values to make sure they match
            var queryMsgPropValues = results.Hits.Hits.Select(hit => hit.Source["Payload_msg"].ToString()).ToArray();
            var areMsgPropertiesEqual = (queryMsgPropValues.Length == msgPropValues.Length && queryMsgPropValues.Intersect(msgPropValues).Count() == queryMsgPropValues.Length);

            Assert.IsTrue(GetIndexCount() == msgPropValues.Length);
            Assert.IsTrue(areMsgPropertiesEqual);
        }
    }

    #region MessageResponseTypes

    public class QueryResult
    {
        public int Took;

        [JsonProperty(PropertyName = "timed_out")]
        public bool TimedOut;

        [JsonProperty(PropertyName = "_shards")]
        public Dictionary<string, object> Shards { get; set; }

        public QueryResultItemCollection Hits { get; set; }
    }

    public class QueryResultItemCollection
    {
        [JsonProperty(PropertyName = "total")]
        public int Total { get; set; }

        public QueryResultItem[] Hits { get; set; }
    }

    public class QueryResultItem
    {
        [JsonProperty(PropertyName = "_index")]
        public string Index { get; set; }

        [JsonProperty(PropertyName = "_type")]
        public string Type { get; set; }

        [JsonProperty(PropertyName = "_id")]
        public string Id { get; set; }

        [JsonProperty(PropertyName = "_source")]
        public Dictionary<string, object> Source { get; set; }
    }

    #endregion
}
