using System;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Sinks;
using Newtonsoft.Json;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Utility
{
    public class ElasticSearchConverter : JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var logEntry = value as ElasticSearchLogEntry;
            if (logEntry == null) return;

            writer.WriteStartObject();

            // { "index" :  {"_index":"testanders","_type":"risk","_id":"2"} }
            writer.WritePropertyName("index");
            
            writer.WriteStartObject();
            writer.WritePropertyName("_index");
            writer.WriteValue(logEntry.Index.ToLower());
            writer.WritePropertyName("_type");
            writer.WriteValue(logEntry.Type);
            writer.WriteEndObject();

            writer.WriteEndObject();
            writer.WriteRaw("\n");
            writer.WriteStartObject();
            GetValue(writer, "EventId", logEntry.LogEntry.EventId);
            GetValue(writer, "EventDate", logEntry.LogEntry.EventDate);
            GetValue(writer, "Keywords", logEntry.LogEntry.Keywords);
            GetValue(writer, "ProviderId", logEntry.LogEntry.ProviderId);
            GetValue(writer, "ProviderName", logEntry.LogEntry.ProviderName);
            GetValue(writer, "InstanceName", logEntry.LogEntry.InstanceName);
            GetValue(writer, "Level", logEntry.LogEntry.Level);
            GetValue(writer, "Message", logEntry.LogEntry.Message);
            GetValue(writer, "Opcode", logEntry.LogEntry.Opcode);
            GetValue(writer, "Task", logEntry.LogEntry.Task);
            GetValue(writer, "Version", logEntry.LogEntry.Version);
            GetValue(writer, "ActivityId", logEntry.LogEntry.ActivityId);
            GetValue(writer, "RelatedActivityId", logEntry.LogEntry.RelatedActivityId);

            foreach (var payload in logEntry.LogEntry.Payload)
                GetValue(writer, "payload_" + payload.Key, payload.Value);
            writer.WriteEndObject();
            writer.WriteRaw("\n");
        }

        private static void GetValue(JsonWriter writer, string key, object valueObj)
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
