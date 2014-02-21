using System;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Sinks;
using Newtonsoft.Json;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Utility
{
    public class ElasticSearchConverter : JsonConverter
    {
        public readonly string PayloadFlattenFormatString = "Payload_{0}";
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
            //Ensure index is lower case - ES requires this
            WriteValue(writer, "_index", logEntry.Index.ToLower());
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
                WriteValue(writer, string.Format(this.PayloadFlattenFormatString, payload.Key), payload.Value);
            }

            writer.WriteEndObject();
            writer.WriteRaw("\n");
        }

        private static void WriteValue(JsonWriter writer, string key, object valueObj)
        {
            writer.WritePropertyName(key);
            writer.WriteValue(valueObj);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override bool CanConvert(Type objectType)
        {
            throw new NotImplementedException();
        }
    }
}
