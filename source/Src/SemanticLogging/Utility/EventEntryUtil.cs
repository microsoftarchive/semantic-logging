// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Formatters;
using Newtonsoft.Json;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Utility
{
    internal static class EventEntryUtil
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2202:Do not dispose objects multiple times", Justification = "Opt out for closing output")]
        internal static string JsonSerializePayload(EventEntry entry)
        {
            try
            {
                using (var writer = new StringWriter(CultureInfo.InvariantCulture))
                using (var jsonWriter = new JsonTextWriter(writer) { Formatting = Newtonsoft.Json.Formatting.Indented, CloseOutput = false })
                {
                    EventEntryUtil.JsonWritePayload(jsonWriter, entry);
                    jsonWriter.Flush();
                    return writer.ToString();
                }
            }
            catch (JsonWriterException jwe)
            {
                SemanticLoggingEventSource.Log.EventEntrySerializePayloadFailed(jwe.ToString());

                var errorDictionary = new Dictionary<string, object>
                { 
                    {
                        "Error",
                        string.Format(CultureInfo.CurrentCulture, Properties.Resources.JsonSerializationError, jwe.Message)
                    }
                };

                return JsonConvert.SerializeObject(errorDictionary, Newtonsoft.Json.Formatting.Indented);
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2202:Do not dispose objects multiple times", Justification = "jsonWriter does not close output")]
        internal static string JsonSerializePayload(IEnumerable<KeyValuePair<string, object>> payload)
        {
            try
            {
                using (var writer = new StringWriter(CultureInfo.InvariantCulture))
                using (var jsonWriter = new JsonTextWriter(writer) { Formatting = Newtonsoft.Json.Formatting.Indented, CloseOutput = false })
                {
                    jsonWriter.WriteStartObject();

                    foreach (var item in payload)
                    {
                        JsonWriteProperty(jsonWriter, item.Key, item.Value);
                    }

                    jsonWriter.WriteEndObject();
                    jsonWriter.Flush();
                    return writer.ToString();
                }
            }
            catch (JsonWriterException jwe)
            {
                SemanticLoggingEventSource.Log.EventEntrySerializePayloadFailed(jwe.ToString());

                var errorDictionary = new Dictionary<string, object>
                { 
                    {
                        "Error",
                        string.Format(CultureInfo.CurrentCulture, Properties.Resources.JsonSerializationError, jwe.Message)
                    }
                };

                return JsonConvert.SerializeObject(errorDictionary, Newtonsoft.Json.Formatting.Indented);
            }
        }

        internal static void JsonWritePayload(JsonWriter writer, EventEntry entry)
        {
            writer.WriteStartObject();

            var eventSchema = entry.Schema;

            for (int i = 0; i < entry.Payload.Count; i++)
            {
                JsonWriteProperty(writer, eventSchema.Payload[i], entry.Payload[i]);
            }

            writer.WriteEndObject();
        }

        private static void JsonWriteProperty(JsonWriter writer, string propertyName, object value)
        {
            try
            {
                writer.WritePropertyName(propertyName);
                writer.WriteValue(value);
            }
            catch (JsonWriterException jwe)
            {
                SemanticLoggingEventSource.Log.EventEntryJsonWriterFailed(jwe.ToString());

                // We are in Error state so abort the write operation
                throw new InvalidOperationException(
                    string.Format(CultureInfo.CurrentCulture, Properties.Resources.JsonSerializationError, jwe.Message), jwe);
            }
        }
        internal static string XmlSerializePayload(EventEntry entry)
        {
            try
            {
                var settings = new XmlWriterSettings()
                {
                    OmitXmlDeclaration = true   // Do not add xml declaration
                };

                var writer = new StringBuilder();
                using (var xmlWriter = XmlWriter.Create(writer, settings))
                {
                    EventEntryUtil.XmlWritePayload(xmlWriter, entry);
                    xmlWriter.Flush();
                    return writer.ToString();
                }
            }
            catch (Exception e)
            {
                SemanticLoggingEventSource.Log.EventEntrySerializePayloadFailed(e.ToString());

                return string.Format("<Error>{0}</Error>", string.Format(CultureInfo.CurrentCulture, Properties.Resources.XmlSerializationError, e.Message));
            }
        }

        internal static void XmlWritePayload(XmlWriter writer, EventEntry entry)
        {
            writer.WriteStartElement("Payload");

            var eventSchema = entry.Schema;

            for (int i = 0; i < entry.Payload.Count; i++)
            {
                XmlWriteProperty(writer, eventSchema.Payload[i], entry.Payload[i]);
            }

            writer.WriteEndElement();
        }

        private static void XmlWriteProperty(XmlWriter writer, string propertyName, object value)
        {
            try
            {
                writer.WriteElementString(propertyName, SanitizeXml(value));
            }
            catch (Exception e)
            {
                SemanticLoggingEventSource.Log.EventEntryXmlWriterFailed(e.ToString());

                // We are in Error state so abort the write operation
                throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, Properties.Resources.XmlSerializationError, e.Message), e);
            }
        }

        internal static string SanitizeXml(object value)
        {
            var valueType = value.GetType();
            if (valueType == typeof(Guid))
            {
                return XmlConvert.ToString((Guid) value);
            }

            if (valueType.IsEnum)
            {
                return ((Enum)value).ToString("D");
            }

            return value.ToString();
        }
    }
}
