// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Utility;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Sinks.WindowsAzure
{
    internal static class CloudEventEntryExtensions
    {
        private const int MaxStringLength = 30000;
        private const int MaxPayloadItems = 200;

        public static DynamicTableEntity CreateTableEntity(this CloudEventEntry entry)
        {
            var dictionary = new Dictionary<string, EntityProperty>();
            dictionary.Add("EventId", new EntityProperty(entry.EventId));
            dictionary.Add("EventDate", new EntityProperty(entry.EventDate));
            dictionary.Add("Keywords", new EntityProperty(entry.Keywords));
            dictionary.Add("ProviderId", new EntityProperty(entry.ProviderId));
            dictionary.Add("ProviderName", new EntityProperty(entry.ProviderName));
            dictionary.Add("InstanceName", new EntityProperty(entry.InstanceName));
            dictionary.Add("Level", new EntityProperty(entry.Level));
            if (entry.Message != null)
            {
                dictionary.Add("Message", new EntityProperty(Normalize(entry.Message)));
            }

            dictionary.Add("Opcode", new EntityProperty(entry.Opcode));
            dictionary.Add("Task", new EntityProperty(entry.Task));
            dictionary.Add("Version", new EntityProperty(entry.Version));

            // Create a "Payload"
            if (entry.Payload != null && entry.Payload.Count > 0)
            {
                var json = EventEntryUtil.JsonSerializePayload(entry.Payload);
                if (json.Length > MaxStringLength)
                {
                    dictionary.Add("Payload", new EntityProperty("{ 'payload_serialization_error':'The payload is too big to serialize.' }"));
                }
                else
                {
                    dictionary.Add("Payload", new EntityProperty(json));

                    foreach (var item in entry.Payload.Take(MaxPayloadItems))
                    {
                        var value = item.Value;
                        if (value != null)
                        {
                            EntityProperty property = null;
                            var type = value.GetType();

                            if (type == typeof(string))
                            {
                                property = new EntityProperty((string)value);
                            }
                            else if (type == typeof(int))
                            {
                                property = new EntityProperty((int)value);
                            }
                            else if (type == typeof(long))
                            {
                                property = new EntityProperty((long)value);
                            }
                            else if (type == typeof(double))
                            {
                                property = new EntityProperty((double)value);
                            }
                            else if (type == typeof(Guid))
                            {
                                property = new EntityProperty((Guid)value);
                            }
                            else if (type == typeof(bool))
                            {
                                property = new EntityProperty((bool)value);
                            }
                            else if (type.IsEnum)
                            {
                                var typeCode = ((Enum)value).GetTypeCode();
                                if (typeCode <= TypeCode.Int32)
                                {
                                    property = new EntityProperty(Convert.ToInt32(value, CultureInfo.InvariantCulture));
                                }
                                else
                                {
                                    property = new EntityProperty(Convert.ToInt64(value, CultureInfo.InvariantCulture));
                                }
                            }
                            else if (type == typeof(byte[]))
                            {
                                property = new EntityProperty((byte[])value);
                            }

                            //// TODO: add & review DateTimeOffset if it's supported

                            if (property != null)
                            {
                                dictionary.Add(string.Format(CultureInfo.InvariantCulture, "Payload_{0}", item.Key), property);
                            }
                        }
                    }
                }
            }

            return new DynamicTableEntity(entry.PartitionKey, entry.RowKey, null, dictionary);
        }

        private static string Normalize(string value)
        {
            if (value.Length > MaxStringLength)
            {
                return value.Substring(0, MaxStringLength) + @"--TRUNCATED--";
            }

            return value;
        }
    }
}
