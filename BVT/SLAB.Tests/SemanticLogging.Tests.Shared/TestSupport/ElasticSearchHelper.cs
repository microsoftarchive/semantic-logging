// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Threading.Tasks;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.Shared.TestSupport
{
    public static class ElasticSearchHelper
    {
        public static void DeleteIndex(string uri, string indexName = null)
        {
            indexName = indexName ?? "*";

            var client = new HttpClient { BaseAddress = new Uri(uri) };

            client.DeleteAsync(indexName).Wait();
        }

        public static QueryResult GetEvents(string uri, string indexName, string typeName, string match = null)
        {
            if (String.IsNullOrEmpty(indexName) || String.IsNullOrEmpty(typeName))
            {
                throw new ArgumentException(" index name and type name need to be supplied");
            }

            string query = "/" + indexName + "/" + typeName + "/" + "_search";
            if (!String.IsNullOrEmpty(match))
            {
                query += match;
            }

            try
            {
                var client = new HttpClient { BaseAddress = new Uri(uri) };
                var response = client.GetStringAsync(query).Result;
                var result = JsonConvert.DeserializeObject<QueryResult>(response);
                return result;
            }
            catch
            { }

            return new QueryResult
            {
                Hits = new QueryResultItemCollection(),
                Shards = new Dictionary<string, object>()
            };
        }

        public static QueryResult PollUntilEvents(string uri, string indexName, string typeName, int eventsToReceive, string match = null, TimeSpan? maxPollTime = null)
        {
            if (!maxPollTime.HasValue)
            {
                maxPollTime = TimeSpan.FromSeconds(10);
            }

            QueryResult results = null;
            DateTime pollUntil = DateTime.Now.Add(maxPollTime.Value);
            while (DateTime.Now < pollUntil)
            {
                results = GetEvents(uri, indexName, typeName, match);
                if (results.Hits.Total == eventsToReceive)
                {
                    return results;
                }

                Task.Delay(TimeSpan.FromMilliseconds(500)).Wait();
            }

            throw new TimeoutException(
                string.Format(CultureInfo.InvariantCulture, "The expected count of {0} events were not received on {1} seconds. Total events received: {2}.",
                eventsToReceive, 
                maxPollTime.Value.ToString(@"ss\.fff"),
                results.Hits.Total));
        }
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
}
