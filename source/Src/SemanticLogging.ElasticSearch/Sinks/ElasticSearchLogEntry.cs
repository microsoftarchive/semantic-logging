// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Utility;
using Newtonsoft.Json;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Sinks
{
    /// <summary>
    /// ElasticSearch Log Entry
    /// </summary>
    [JsonConverter(typeof(ElasticSearchConverter))]
    public class ElasticSearchLogEntry
    {
        /// <summary>
        /// Event log entry data
        /// </summary>
        public JsonEventEntry LogEntry { get; set; }

        /// <summary>
        /// ElasticSearch log entry type
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// ElasticSearch log index.  This is formatted as {IndexName}-{EventDateTime:yyyy.MM.dd}
        /// </summary>
        public string Index { get; set; }
    }
}
