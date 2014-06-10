// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Utility
{
    /// <summary>
    /// Converts ElasticsearchLogEntry to JSON formatted Elasticsearch _bulk service index operation
    /// </summary>
    internal class ElasticsearchEventEntrySerializer : IDisposable
    {
        private const string PayloadFlattenFormatString = "Payload_{0}";

        private readonly string indexName;
        private readonly string entryType;
        private readonly string instanceName;
        private readonly bool flattenPayload;

        private JsonWriter writer;

        internal ElasticsearchEventEntrySerializer(string indexName, string entryType, string instanceName, bool flattenPayload)
        {
            this.indexName = indexName;
            this.entryType = entryType;
            this.instanceName = instanceName;
            this.flattenPayload = flattenPayload;
        }

        internal string Serialize(IEnumerable<EventEntry> entries)
        {
            if (entries == null)
            {
                return null;
            }

            var sb = new StringBuilder();
            this.writer = new JsonTextWriter(new StringWriter(sb, CultureInfo.InvariantCulture)) { CloseOutput = true };

            foreach (var entry in entries)
            {
                this.WriteJsonEntry(entry);
            }

            // Close the writer
            this.writer.Close();
            this.writer = null;

            return sb.ToString();
        }

        private void WriteJsonEntry(EventEntry entry)
        {
            this.writer.WriteStartObject();

            this.writer.WritePropertyName("index");

            // Write the batch "index" operation header
            this.writer.WriteStartObject();
            // ES index names must be lower case and cannot contain whitespace or any of the following characters \/*?"<>|,
            WriteValue("_index", this.GetIndexName(entry.Timestamp.UtcDateTime));
            WriteValue("_type", this.entryType);
            this.writer.WriteEndObject();
            this.writer.WriteEndObject();
            this.writer.WriteRaw("\n");  //ES requires this \n separator

            this.writer.WriteStartObject();
            WriteValue("EventId", entry.EventId);
            WriteValue("EventDate", entry.Timestamp.UtcDateTime);
            WriteValue("Keywords", (long)entry.Schema.Keywords);
            WriteValue("ProviderId", entry.Schema.ProviderId);
            WriteValue("ProviderName", entry.Schema.ProviderName);
            WriteValue("InstanceName", this.instanceName);
            WriteValue("Level", (int)entry.Schema.Level);
            WriteValue("Message", entry.FormattedMessage);
            WriteValue("Opcode", (int)entry.Schema.Opcode);
            WriteValue("Task", (int)entry.Schema.Task);
            WriteValue("Version", entry.Schema.Version);
            WriteValue("ProcessId", entry.ProcessId);
            WriteValue("ThreadId", entry.ThreadId);

            if (entry.ActivityId != Guid.Empty)
            {
                WriteValue("ActivityId", entry.ActivityId);
            }

            if (entry.RelatedActivityId != Guid.Empty)
            {
                WriteValue("RelatedActivityId", entry.RelatedActivityId);
            }

            //If we are not going to flatten the payload then write opening
            if (!flattenPayload)
            {
                writer.WritePropertyName("Payload");
                writer.WriteStartObject();
            }

            foreach (var payload in entry.Schema.Payload.Zip(entry.Payload, Tuple.Create))
            {
                this.WriteValue(
                    this.flattenPayload
                        ? string.Format(CultureInfo.InvariantCulture, PayloadFlattenFormatString, payload.Item1)
                        : payload.Item1,
                    payload.Item2);
            }

            //If we are not going to flatten the payload then write closing
            if (!flattenPayload)
            {
                writer.WriteEndObject();
            }

            this.writer.WriteEndObject();
            this.writer.WriteRaw("\n");
        }

        private void WriteValue(string key, object valueObj)
        {
            this.writer.WritePropertyName(key);
            this.writer.WriteValue(valueObj);
        }

        private string GetIndexName(DateTime entryDateTime)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}-{1:yyyy.MM.dd}", this.indexName, entryDateTime);
        }

        public void Dispose()
        {
            if (writer != null)
            {
                this.writer.Close();
                this.writer = null;
            }
        }
    }
}
