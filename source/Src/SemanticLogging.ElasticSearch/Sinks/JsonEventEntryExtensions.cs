using System;
using System.Globalization;
using System.Linq;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Utility;
using Newtonsoft.Json.Linq;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Sinks
{
    internal static class JsonEventEntryExtensions
    {
        private const int MaxPayloadItems = 200;

        public static JObject CreateTableEntity(this JsonEventEntry entry)
        {
            var dictionary = new JObject
            {
                { "EventId", entry.EventId },
                { "EventDate", entry.EventDate },
                { "Keywords", entry.Keywords },
                { "ProviderId", entry.ProviderId },
                { "ProviderName", entry.ProviderName },
                { "InstanceName", entry.InstanceName },
                { "Level", entry.Level }
            };

            if (entry.Message != null)
            {
                dictionary.Add("Message", entry.Message);
            }

            dictionary.Add("Opcode", entry.Opcode);
            dictionary.Add("Task", entry.Task);
            dictionary.Add("Version", entry.Version);

            if (entry.ActivityId != Guid.Empty)
            {
                dictionary.Add("ActivityId", entry.ActivityId);
            }

            if (entry.RelatedActivityId != Guid.Empty)
            {
                dictionary.Add("RelatedActivityId", entry.RelatedActivityId);
            }

            // Create a "Payload"
            if (entry.Payload != null && entry.Payload.Count > 0)
            {
                var json = EventEntryUtil.JsonSerializePayload(entry.Payload);

                dictionary.Add("Payload", json);

                foreach (var item in entry.Payload.Take(MaxPayloadItems))
                {
                    var value = item.Value;
                    if (value == null)
                    {
                        continue;
                    }

                    JToken property = null;
                    var type = value.GetType();

                    if (type == typeof(string))
                    {
                        property = (string)value;
                    }
                    else if (type == typeof(int))
                    {
                        property = (int)value;
                    }
                    else if (type == typeof(long))
                    {
                        property = (long)value;
                    }
                    else if (type == typeof(double))
                    {
                        property = (double)value;
                    }
                    else if (type == typeof(Guid))
                    {
                        property = (Guid)value;
                    }
                    else if (type == typeof(bool))
                    {
                        property = (bool)value;
                    }
                    else if (type.IsEnum)
                    {
                        var typeCode = ((Enum)value).GetTypeCode();
                        property = typeCode <= TypeCode.Int32
                            ? Convert.ToInt32(value, CultureInfo.InvariantCulture)
                            : Convert.ToInt64(value, CultureInfo.InvariantCulture);
                    }
                    else if (type == typeof(byte[]))
                    {
                        // TODO: Handle as best as we can
                        property = (byte[])value;
                    }

                    if (property != null)
                    {
                        dictionary.Add(string.Format(CultureInfo.InvariantCulture, "Payload_{0}", item.Key),
                            property);
                    }
                }
            }

            return dictionary;
        }
    }
}