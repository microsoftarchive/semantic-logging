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

    [TestClass]
    public class given_empty_index
    {
        //These tests will delete EVERYTHING in the supplied elasticsearch endpoint
        protected readonly string DevelopmentElasticSearchUrl = "http://localhost:9200";

        [TestInitialize]
        public void Setup()
        {
            //Delete all indexes
            DeleteIndex();
        }

        protected void DeleteIndex(string indexName = null)
        {
            indexName = indexName ?? "*";

            var client = new HttpClient { BaseAddress = new Uri(DevelopmentElasticSearchUrl) };

            client.DeleteAsync(indexName).Wait();
        }

        protected int GetIndexCount(string indexName = null)
        {
            var client = new HttpClient { BaseAddress = new Uri(DevelopmentElasticSearchUrl) };

            var operation = string.Format("{0}/_count", indexName ?? "_all");

            var result = JsonConvert.DeserializeObject<CountResult>(client.GetStringAsync(operation).Result);

            return result.Count;
        }

        protected QueryResult QueryAllEntriesByIndex(string indexName = null)
        {
            var client = new HttpClient { BaseAddress = new Uri(DevelopmentElasticSearchUrl) };

            var operation = string.Format("{0}/_search?q=*", indexName ?? "_all");

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

            var sink = new ElasticSearchSink("instance", DevelopmentElasticSearchUrl, "logstash", "etw", TimeSpan.FromSeconds(1), 600,
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

            //Query across the _all index
            var results = this.QueryAllEntriesByIndex();

            //Compare the message property values to make sure they match
            var queryMsgPropValues = results.Hits.Hits.Select(hit => hit.Source["Payload_msg"].ToString()).ToArray();
            var areMsgPropertiesEqual = (queryMsgPropValues.Length == msgPropValues.Length && queryMsgPropValues.Intersect(msgPropValues).Count() == queryMsgPropValues.Length);

            Assert.IsTrue(GetIndexCount() == msgPropValues.Length);
            Assert.IsTrue(areMsgPropertiesEqual);
        }
    }

    #region MessageResponsTypes
    public class CountResult
    {
        public int Count { get; set; }

        [JsonProperty(PropertyName = "_shards")]
        public Dictionary<string, object> Shards { get; set; }
    }

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
