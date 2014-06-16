// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Utility;
using Newtonsoft.Json;
using System;
using System.IO;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Formatters
{
    /// <summary>
    /// A <see cref="IEventTextFormatter"/> implementation that writes out text formatted as JSON.
    /// </summary>
    /// <remarks>This class is not thread-safe.</remarks>
    public class JsonEventTextFormatter : IEventTextFormatter
    {
        /// <summary>
        /// The default event text formatting.
        /// </summary>
        public const EventTextFormatting DefaultEventTextFormatting = EventTextFormatting.None;

        private const string EntrySeparator = ",";
        private Newtonsoft.Json.Formatting formatting;
        private string dateTimeFormat;

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonEventTextFormatter" /> class.
        /// </summary>
        /// <param name="formatting">The <see cref="EventTextFormatting" /> formatting.</param>
        /// <param name="dateTimeFormat">The date time format used for timestamp value.</param>
        public JsonEventTextFormatter(EventTextFormatting formatting = DefaultEventTextFormatting, string dateTimeFormat = null)
        {
            this.formatting = (Newtonsoft.Json.Formatting)formatting;
            this.DateTimeFormat = dateTimeFormat;
            this.IncludeEntrySeparator = true;
        }

        /// <summary>
        /// Gets or sets the date time format used for timestamp value.
        /// </summary>
        /// <value>
        /// The date time format.
        /// </value>
        public string DateTimeFormat
        {
            get
            {
                return this.dateTimeFormat;
            }

            set
            {
                Guard.ValidDateTimeFormat(value, "DateTimeFormat");
                this.dateTimeFormat = value;
            }
        }

        /// <summary>
        /// Gets or sets a flag indicating whether the <see cref="JsonEventTextFormatter"/> will append a separator (",") at the end of each event
        /// </summary>
        /// <value>The flag indicating whether to append a separator.</value>
        public bool IncludeEntrySeparator { get; set; }

        /// <summary>
        /// Gets the <see cref="EventTextFormatting"/>.
        /// </summary>
        /// <value>The <see cref="EventTextFormatting"/>.</value>
        public EventTextFormatting Formatting
        {
            get { return (EventTextFormatting)this.formatting; }
        }

        /// <summary>
        /// Write the formatted event output.
        /// </summary>
        /// <param name="eventEntry">The event data to be formatted.</param>
        /// <param name="writer">The writer to receive the formatted output.</param>
        public void WriteEvent(EventEntry eventEntry, TextWriter writer)
        {
            Guard.ArgumentNotNull(eventEntry, "eventEntry");

            using (var jsonWriter = new JsonTextWriter(writer) { CloseOutput = false, Formatting = this.formatting })
            {
                jsonWriter.WriteStartObject();
                jsonWriter.WritePropertyName(PropertyNames.ProviderId);
                jsonWriter.WriteValue(eventEntry.ProviderId);
                jsonWriter.WritePropertyName(PropertyNames.EventId);
                jsonWriter.WriteValue(eventEntry.EventId);
                jsonWriter.WritePropertyName(PropertyNames.Keywords);
                jsonWriter.WriteValue((long)eventEntry.Schema.Keywords);
                jsonWriter.WritePropertyName(PropertyNames.Level);
                jsonWriter.WriteValue((int)eventEntry.Schema.Level);
                jsonWriter.WritePropertyName(PropertyNames.Message);
                jsonWriter.WriteValue(eventEntry.FormattedMessage);
                jsonWriter.WritePropertyName(PropertyNames.Opcode);
                jsonWriter.WriteValue((int)eventEntry.Schema.Opcode);
                jsonWriter.WritePropertyName(PropertyNames.Task);
                jsonWriter.WriteValue((int)eventEntry.Schema.Task);
                jsonWriter.WritePropertyName(PropertyNames.Version);
                jsonWriter.WriteValue(eventEntry.Schema.Version);
                jsonWriter.WritePropertyName(PropertyNames.Payload);
                EventEntryUtil.JsonWritePayload(jsonWriter, eventEntry);
                jsonWriter.WritePropertyName(PropertyNames.EventName);
                jsonWriter.WriteValue(eventEntry.Schema.EventName);
                jsonWriter.WritePropertyName(PropertyNames.Timestamp);
                jsonWriter.WriteValue(eventEntry.GetFormattedTimestamp(this.DateTimeFormat));
                jsonWriter.WritePropertyName(PropertyNames.ProcessId);
                jsonWriter.WriteValue(eventEntry.ProcessId);
                jsonWriter.WritePropertyName(PropertyNames.ThreadId);
                jsonWriter.WriteValue(eventEntry.ThreadId);

                if (eventEntry.ActivityId != Guid.Empty)
                {
                    jsonWriter.WritePropertyName(PropertyNames.ActivityId);
                    jsonWriter.WriteValue(eventEntry.ActivityId);
                }

                if (eventEntry.RelatedActivityId != Guid.Empty)
                {
                    jsonWriter.WritePropertyName(PropertyNames.RelatedActivityId);
                    jsonWriter.WriteValue(eventEntry.RelatedActivityId);
                }

                jsonWriter.WriteEndObject();

                if (IncludeEntrySeparator)
                {
                    // Write an entry separator so all the logs can be read as an array, 
                    // adding the [] chars to the raw written data ( i.e: "[" + raw + "]" )
                    // where raw = {log1},{log2}, ... {logN},
                    jsonWriter.WriteRaw(EntrySeparator);
                }

                // Writes new line when indented
                if (jsonWriter.Formatting == Newtonsoft.Json.Formatting.Indented)
                {
                    jsonWriter.WriteRaw("\r\n");
                }
            }
        }
    }
}
