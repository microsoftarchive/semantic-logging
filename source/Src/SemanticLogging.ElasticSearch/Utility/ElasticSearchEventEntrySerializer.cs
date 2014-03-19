// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Sinks;
using Newtonsoft.Json;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Utility
{
    /// <summary>
    /// Converts ElasticSearchLogEntry to JSON formatted ElasticSearch _bulk service index operation
    /// </summary>
    internal class ElasticSearchEventEntrySerializer : IDisposable
    {
        private const string PayloadFlattenFormatString = "Payload_{0}";

        private readonly string indexName;

        private readonly string entryType;

        private readonly bool flattenPayload;

        private JsonWriter writer;

        internal ElasticSearchEventEntrySerializer(string indexName, string entryType, bool flattenPayload)
        {
            this.indexName = indexName;
            this.entryType = entryType;
            this.flattenPayload = flattenPayload;
        }

        internal string Serialize(IEnumerable<JsonEventEntry> entries)
        {
            if (entries == null)
            {
                return null;
            }

            var sb = new StringBuilder();
            this.writer = new JsonTextWriter(new StringWriter(sb)) { CloseOutput = true };

            foreach (var entry in entries)
            {
                this.WriteJsonEntry(entry);
            }

            // Close the writer
            this.writer.Close();
            this.writer = null;

            return sb.ToString();
        }

        private void WriteJsonEntry(JsonEventEntry entry)
        {
            this.writer.WriteStartObject();

            this.writer.WritePropertyName("index");

            // Write the batch "index" operation header
            this.writer.WriteStartObject();
            // ES index names must be lower case and cannot contain whitespace or any of the following characters \/*?"<>|,
            WriteValue("_index", this.GetIndexName(entry.EventDate));
            WriteValue("_type", this.entryType);
            this.writer.WriteEndObject();
            this.writer.WriteEndObject();
            this.writer.WriteRaw("\n");  //ES requires this \n separator

            this.writer.WriteStartObject();
            WriteValue("EventId", entry.EventId);
            WriteValue("EventDate", entry.EventDate);
            WriteValue("Keywords", entry.Keywords);
            WriteValue("ProviderId", entry.ProviderId);
            WriteValue("ProviderName", entry.ProviderName);
            WriteValue("InstanceName", entry.InstanceName);
            WriteValue("Level", entry.Level);
            WriteValue("Message", entry.Message);
            WriteValue("Opcode", entry.Opcode);
            WriteValue("Task", entry.Task);
            WriteValue("Version", entry.Version);

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

            foreach (var payload in entry.Payload)
            {
                this.WriteValue(
                    this.flattenPayload
                        ? string.Format(CultureInfo.InvariantCulture, PayloadFlattenFormatString, payload.Key)
                        : payload.Key,
                    payload.Value);
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
