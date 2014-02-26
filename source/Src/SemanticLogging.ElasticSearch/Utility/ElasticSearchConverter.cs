// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Sinks;
using Newtonsoft.Json;
using System.Globalization;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Utility
{
    /// <summary>
    /// Converts ElasticSearchLogEntry to JSON formatted ElasticSearch _bulk service index operation
    /// </summary>
    public class ElasticSearchConverter : JsonConverter
    {
        private const string PayloadFlattenFormatString = "Payload_{0}";

        /// <summary>
        /// Writes the JSON representation of the object.
        /// </summary>
        /// <param name="writer">The <see cref="JsonWriter"/> to write to.</param>
        /// <param name="value">The value.</param>
        /// <param name="serializer">The calling serializer.</param>
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var logEntry = value as ElasticSearchLogEntry;
            if (logEntry == null)
            {
                return;
            }

            writer.WriteStartObject();

            writer.WritePropertyName("index");
            
            // Write the batch "index" operation header
            writer.WriteStartObject();
            // ES index names must be lower case and cannot contain whitespace or any of the following characters \/*?"<>|,
            WriteValue(writer, "_index", logEntry.Index.ToLower(CultureInfo.InvariantCulture));
            WriteValue(writer, "_type", logEntry.Type);
            writer.WriteEndObject();
            writer.WriteEndObject();
            writer.WriteRaw("\n");  //ES requires this \n separator

            writer.WriteStartObject();
            WriteValue(writer, "EventId", logEntry.LogEntry.EventId);
            WriteValue(writer, "EventDate", logEntry.LogEntry.EventDate);
            WriteValue(writer, "Keywords", logEntry.LogEntry.Keywords);
            WriteValue(writer, "ProviderId", logEntry.LogEntry.ProviderId);
            WriteValue(writer, "ProviderName", logEntry.LogEntry.ProviderName);
            WriteValue(writer, "InstanceName", logEntry.LogEntry.InstanceName);
            WriteValue(writer, "Level", logEntry.LogEntry.Level);
            WriteValue(writer, "Message", logEntry.LogEntry.Message);
            WriteValue(writer, "Opcode", logEntry.LogEntry.Opcode);
            WriteValue(writer, "Task", logEntry.LogEntry.Task);
            WriteValue(writer, "Version", logEntry.LogEntry.Version);
            
            if (logEntry.LogEntry.ActivityId != Guid.Empty)
            {
                WriteValue(writer, "ActivityId", logEntry.LogEntry.ActivityId);
            }

            if (logEntry.LogEntry.RelatedActivityId != Guid.Empty)
            {
                WriteValue(writer, "RelatedActivityId", logEntry.LogEntry.RelatedActivityId);
            }

            //Alternatively we should consider option to preserve structure and not flatten the payload
            foreach (var payload in logEntry.LogEntry.Payload)
            {
                WriteValue(writer, string.Format(CultureInfo.InvariantCulture, PayloadFlattenFormatString, payload.Key), payload.Value);
            }

            writer.WriteEndObject();
            writer.WriteRaw("\n");
        }

        private static void WriteValue(JsonWriter writer, string key, object valueObj)
        {
            writer.WritePropertyName(key);
            writer.WriteValue(valueObj);
        }

        /// <summary>
        /// Reads the JSON representation of the object.
        /// </summary>
        /// <param name="reader">The <see cref="JsonReader"/> to read from.</param>
        /// <param name="objectType">Type of the object.</param>
        /// <param name="existingValue">The existing value of object being read.</param>
        /// <param name="serializer">The calling serializer.</param>
        /// <returns>Throws NotImplementedException.</returns>
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Determines whether this instance can convert the specified object type.
        /// </summary>
        /// <param name="objectType">Type of the object.</param>
        /// <returns>
        /// Throws NotImplementedException.
        /// </returns>
        public override bool CanConvert(Type objectType)
        {
            throw new NotImplementedException();
        }
    }
}
