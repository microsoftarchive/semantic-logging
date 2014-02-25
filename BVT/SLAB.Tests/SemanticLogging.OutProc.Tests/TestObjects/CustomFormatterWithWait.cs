// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Formatters;
using System;
using System.Diagnostics.Tracing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.OutProc.Tests.TestObjects
{
    public class CustomFormatterWithWait : IEventTextFormatter, IDisposable
    {
        /// <summary>
        /// The dash separator
        /// </summary>
        public const string DashSeparator = "----------------------------------------";
        private ManualResetEventSlim waitEvents;
        private const string TextSerializationError = "Cannot serialize the payload: {0}";
        private bool disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="EventTextFormatter" /> class.
        /// </summary>
        public CustomFormatterWithWait()
            : this(null, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EventTextFormatter" /> class.
        /// </summary>
        /// <param name="header">The header.</param>
        public CustomFormatterWithWait(string header)
            : this(header, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EventTextFormatter" /> class.
        /// </summary>
        /// <param name="header">The header.</param>
        /// <param name="footer">The footer.</param>
        public CustomFormatterWithWait(string header, string footer)
        {
            waitEvents = new ManualResetEventSlim();

            this.Header = header;
            this.Footer = footer;
            this.Detailed = EventLevel.Informational;
        }

        /// <summary>
        /// Gets or sets the header
        /// </summary>
        public string Header { get; set; }

        /// <summary>
        /// Gets or sets the footer
        /// </summary>
        public string Footer { get; set; }

        /// <summary>
        /// Gets or sets the lowest <see cref="EventLevel" /> value where the formatted output provides all the event entry information.
        /// Otherwise a summarized content of the event entry will be written.
        /// </summary>
        public EventLevel Detailed { get; set; }

        /// <summary>
        /// Gets or sets the date time format used for timestamp value.
        /// </summary>
        /// <value>
        /// The date time format.
        /// </value>
        public string DateTimeFormat { get; set; }

        public ManualResetEventSlim WaitEvents
        {
            get { return this.waitEvents; }
        }

        /// <summary>
        /// Writes the event.
        /// </summary>
        /// <param name="eventData">The <see cref="EventWrittenEventArgs" /> instance containing the event data.</param>
        /// <param name="writer">The writer.</param>
        public void WriteEvent(EventEntry eventEntry, TextWriter writer)
        {
            // Write header
            if (!string.IsNullOrWhiteSpace(this.Header))
            {
                writer.WriteLine(this.Header); 
            }

            if (eventEntry.Payload.First().ToString() == "error")
            {
                writer.WriteLine("This is an entry containing and error and should not be logged");
                throw new InvalidOperationException("error");
            }

            if (eventEntry.Schema.Level >= this.Detailed)
            {
                // Write properties
                writer.WriteLine("Mock SourceId : {0}", eventEntry.ProviderId);
                writer.WriteLine("Mock EventId : {0}", eventEntry.EventId);
                writer.WriteLine("Keywords : {0}", eventEntry.Schema.Keywords);
                writer.WriteLine("Level : {0}", eventEntry.Schema.Level);
                writer.WriteLine("Message : {0}", eventEntry.FormattedMessage);
                writer.WriteLine("Opcode : {0}", eventEntry.Schema.Opcode);
                writer.WriteLine("Task : {0} {1}", eventEntry.Schema.Task, eventEntry.Schema.EventName);
                writer.WriteLine("Version : {0}", eventEntry.Schema.Version);
                writer.WriteLine("Payload :{0}", FormatPayload(eventEntry));
                writer.WriteLine("Timestamp : {0}", eventEntry.GetFormattedTimestamp(this.DateTimeFormat));
            }
            else
            {
                writer.WriteLine("EventId : {0}, Level : {1}, Message : {2}, Payload :{3}, Timestamp : {4}",
                    eventEntry.EventId,
                    eventEntry.Schema.Level,
                    eventEntry.FormattedMessage,
                    FormatPayload(eventEntry),
                    eventEntry.GetFormattedTimestamp(this.DateTimeFormat));
            }

            // Write footer
            if (!string.IsNullOrWhiteSpace(this.Footer))
            {
                writer.WriteLine(this.Footer); 
            }

            writer.WriteLine();

            if (this.waitEvents != null)
            {
                waitEvents.Reset(); 
            }
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                if (disposing)
                {
                    if (this.waitEvents != null)
                    {
                        this.waitEvents.Dispose();
                    }
                }

                this.waitEvents = null;
                this.disposed = true;
            }
        }

        private static string FormatPayload(EventEntry entry)
        {
            var eventSchema = entry.Schema;
            var sb = new StringBuilder();
            for (int i = 0; i < entry.Payload.Count; i++)
            {
                try
                {
                    sb.AppendFormat(" [{0} : {1}]", eventSchema.Payload[i], entry.Payload[i]);
                }
                catch (Exception e)
                {
                    SemanticLoggingEventSource.Log.EventEntryTextWriterFailed(e.ToString());
                    sb.AppendFormat(" [{0} : {1}]", "Exception", string.Format(CultureInfo.CurrentCulture, TextSerializationError, e.Message));
                }
            }

            return sb.ToString();
        }
    }
}
