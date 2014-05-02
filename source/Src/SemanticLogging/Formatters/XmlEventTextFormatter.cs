// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Diagnostics.Tracing;
using System.Globalization;
using System.IO;
using System.Xml;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Utility;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Formatters
{
    /// <summary>
    /// A <see cref="IEventTextFormatter"/> implementation that writes out text formatted as XML compliant with the 
    /// <a href="http://msdn.microsoft.com/en-us/library/windows/desktop/aa385201(v=vs.85).aspx">Event Schema</a>.
    /// </summary>
    /// <remarks>This class is not thread-safe.</remarks>
    public class XmlEventTextFormatter : IEventTextFormatter
    {
        /// <summary>
        /// The default event text formatting.
        /// </summary>
        public const EventTextFormatting DefaultEventTextFormatting = EventTextFormatting.None;

        private const string EventNS = "http://schemas.microsoft.com/win/2004/08/events/event";
        private readonly string machine = Environment.MachineName;
        private readonly string culture = CultureInfo.CurrentCulture.Name.ToLowerInvariant();
        private XmlWriterSettings settings;
        private string dateTimeFormat;

        /// <summary>
        /// Initializes a new instance of the <see cref="XmlEventTextFormatter" /> class.
        /// </summary>
        /// <param name="formatting">The <see cref="EventTextFormatting" /> formatting.</param>
        /// <param name="dateTimeFormat">The date time format used for timestamp value.</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Globalization", "CA1308:NormalizeStringsToUppercase", Justification = "Normalized string is standard locale code")]
        public XmlEventTextFormatter(EventTextFormatting formatting = DefaultEventTextFormatting, string dateTimeFormat = null)
        {
            this.Formatting = formatting;
            this.DateTimeFormat = dateTimeFormat;
            this.settings = new XmlWriterSettings()
            {
                Indent = formatting == EventTextFormatting.Indented,  // Indent on formatting setting
                OmitXmlDeclaration = true,                            // Do not add xml declaration
            };
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
        /// Gets the <see cref="EventTextFormatting"/>.
        /// </summary>
        /// <value>The <see cref="EventTextFormatting"/>.</value>
        public EventTextFormatting Formatting { get; private set; }

        /// <summary>
        /// Writes the event.
        /// </summary>
        /// <param name="eventEntry">The <see cref="EventWrittenEventArgs" /> instance containing the event data.</param>
        /// <param name="writer">The writer.</param>
        public void WriteEvent(EventEntry eventEntry, TextWriter writer)
        {
            Guard.ArgumentNotNull(eventEntry, "eventEntry");

            using (var xmlWriter = XmlWriter.Create(writer, this.settings))
            {
                xmlWriter.WriteStartElement("Event", EventNS);

                xmlWriter.WriteStartElement("System");
                xmlWriter.WriteStartElement("Provider");
                xmlWriter.WriteAttributeString("Guid", eventEntry.ProviderId.ToString("B"));
                xmlWriter.WriteEndElement();
                xmlWriter.WriteStartElement("EventID");
                xmlWriter.WriteValue(eventEntry.EventId);
                xmlWriter.WriteEndElement();
                xmlWriter.WriteStartElement("Version");
                xmlWriter.WriteValue(eventEntry.Schema.Version);
                xmlWriter.WriteEndElement();
                xmlWriter.WriteStartElement("Level");
                xmlWriter.WriteValue((int)eventEntry.Schema.Level);
                xmlWriter.WriteEndElement();
                xmlWriter.WriteStartElement("Task");
                xmlWriter.WriteValue((int)eventEntry.Schema.Task);
                xmlWriter.WriteEndElement();
                xmlWriter.WriteStartElement("Opcode");
                xmlWriter.WriteValue((int)eventEntry.Schema.Opcode);
                xmlWriter.WriteEndElement();
                xmlWriter.WriteElementString("Keywords", ToHex(eventEntry.Schema.Keywords));
                xmlWriter.WriteStartElement("TimeCreated");
                xmlWriter.WriteAttributeString("SystemTime", eventEntry.GetFormattedTimestamp(this.DateTimeFormat));
                xmlWriter.WriteEndElement();

                if (eventEntry.ActivityId != Guid.Empty || eventEntry.RelatedActivityId != Guid.Empty)
                {
                    xmlWriter.WriteStartElement("Correlation");
                    xmlWriter.WriteAttributeString("ActivityID", eventEntry.ActivityId.ToString("B"));
                    if (eventEntry.RelatedActivityId != Guid.Empty)
                    {
                        xmlWriter.WriteAttributeString("RelatedActivityID", eventEntry.RelatedActivityId.ToString("B"));
                    }

                    xmlWriter.WriteEndElement();
                }

                xmlWriter.WriteStartElement("Execution");
                xmlWriter.WriteAttributeString("ProcessID", eventEntry.ProcessId.ToString(CultureInfo.InvariantCulture));
                xmlWriter.WriteAttributeString("ThreadID", eventEntry.ThreadId.ToString(CultureInfo.InvariantCulture));
                xmlWriter.WriteEndElement();

                xmlWriter.WriteElementString("Computer", this.machine);
                xmlWriter.WriteEndElement(); // System

                xmlWriter.WriteStartElement("EventData");
                XmlWritePayload(xmlWriter, eventEntry);
                xmlWriter.WriteEndElement();

                xmlWriter.WriteStartElement("RenderingInfo");
                xmlWriter.WriteAttributeString("Culture", this.culture);
                xmlWriter.WriteElementString("Message", (eventEntry != null) ? eventEntry.FormattedMessage : null);
                xmlWriter.WriteEndElement();

                xmlWriter.WriteEndElement(); // Event                  

                // Writes out a new line when Indented because WriteEndElement
                // does not add it after closing the root element (Event)
                if (this.Formatting == EventTextFormatting.Indented)
                {
                    xmlWriter.WriteRaw(xmlWriter.Settings.NewLineChars);
                }
            }
        }

        private static string ToHex(EventKeywords value)
        {
            return string.Format(CultureInfo.InvariantCulture, "0x{0:X}", (long)value);
        }

        private static void XmlWritePayload(XmlWriter writer, EventEntry entry)
        {
            var eventSchema = entry.Schema;

            for (int i = 0; i < entry.Payload.Count; i++)
            {
                try
                {
                    writer.WriteStartElement("Data");
                    writer.WriteAttributeString("Name", eventSchema.Payload[i]);

                    if (entry.Payload[i] != null)
                    {
                        SanitizeAndWritePayload(entry.Payload[i], writer);
                    }

                    writer.WriteEndElement();
                }
                catch (Exception e)
                {
                    SemanticLoggingEventSource.Log.EventEntryXmlWriterFailed(e.ToString());

                    // We are in Error state so abort the write operation
                    throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, Properties.Resources.XmlSerializationError, e.Message), e);
                }
            }
        }

        private static void SanitizeAndWritePayload(object value, XmlWriter writer)
        {
            var valueType = value.GetType();
            if (valueType == typeof(Guid))
            {
                writer.WriteValue(XmlConvert.ToString((Guid)value));
            }
            else if (valueType.IsEnum)
            {
                writer.WriteValue(((Enum)value).ToString("D"));
            }
            else
            {
                writer.WriteValue(value);
            }
        }
    }
}
