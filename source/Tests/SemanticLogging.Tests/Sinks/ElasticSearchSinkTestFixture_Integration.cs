// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Sinks;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.Sinks
{
    [TestClass]
    public class given_empty_index : ArrangeActAssert
    {
        // These tests will delete data in the provided elasticsearch endpoint
        protected string elasticsearchUrl;

        protected readonly string TestIndex = "slabtest";

        protected override void Arrange()
        {
            this.elasticsearchUrl = ConfigurationHelper.GetSetting("ElasticsearchUrl");

            if (string.IsNullOrEmpty(this.elasticsearchUrl))
            {
                Assert.Inconclusive("Cannot run tests because the Elastic Search URL is not configured in the app.config file. Uncomment the app setting for ElasticsearchUrl and update it if needed.");
            }

            // Delete data in the text index(s)
            DeleteIndex();
        }

        protected override void Teardown()
        {
            DeleteIndex();
        }

        protected void DeleteIndex(string indexName = null)
        {
            indexName = indexName ?? TestIndex + "*";

            var client = new HttpClient { BaseAddress = new Uri(this.elasticsearchUrl) };

            client.DeleteAsync(indexName).Wait();
        }

        protected int GetIndexCount(string indexName = null)
        {
            var client = new HttpClient { BaseAddress = new Uri(this.elasticsearchUrl) };

            var operation = string.Format("{0}/_count", indexName ?? TestIndex + "*");

            var response = client.GetStringAsync(operation).Result;

            return JObject.Parse(response)["count"].Value<int>();
        }

        protected QueryResult QueryAllEntriesByIndex(string indexName = null)
        {
            var client = new HttpClient { BaseAddress = new Uri(this.elasticsearchUrl) };

            var operation = string.Format("{0}/_search", indexName ?? TestIndex + "*");

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
        private ElasticsearchSink sink;
        private string[] msgPropValues;

        protected override void Arrange()
        {
            base.Arrange();

            this.sink = new ElasticsearchSink("instance", this.elasticsearchUrl, TestIndex, "etw", true, TimeSpan.FromSeconds(1), 100, 3000,
                TimeSpan.FromMinutes(1));
            this.msgPropValues = new[] { "1", "2", "3" };
        }

        protected override void Teardown()
        {
            base.Teardown();

            this.sink.OnCompleted();
        }

        protected override void Act()
        {
            foreach (var entry in this.msgPropValues.Select(this.CreateEventEntry))
            {
                this.sink.OnNext(entry);
            }
        }

        [TestMethod]
        public void then_all_entries_and_properties_are_written()
        {
            Assert.IsTrue(this.sink.FlushAsync().Wait(TimeSpan.FromSeconds(45)));

            // Check until it's what we expect or we have checked too many times
            QueryResult results = null;

            for (int i = 0; i < 12; i++)
            {
                results = this.QueryAllEntriesByIndex();

                Thread.Sleep(500);

                if (results != null && results.Hits.Hits.Length >= this.msgPropValues.Length)
                {
                    break;
                }
            }

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
