// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Tracing;
using System.Globalization;
using System.Linq;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Schema;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Utility;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging
{
    /// <summary>
    /// Represents a entry to log, with additional context information.
    /// </summary>
    public class EventEntry
    {
        /// <summary>
        /// The default date time format for a formatter date values. 
        /// Default as Round-trip value; "yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fffffffzzz".
        /// </summary>
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly", Justification = "Reviewed.")]
        internal const string DefaultDateTimeFormat = "O";

        private readonly Guid providerId;
        private readonly int eventId;
        private readonly string formattedMessage;
        private readonly ReadOnlyCollection<object> payload;
        private readonly DateTimeOffset timestamp;
        private readonly EventSchema schema;

        /// <summary>
        /// Initializes a new instance of the <see cref="EventEntry" /> class.
        /// </summary>
        /// <param name="sourceId">The source id.</param>
        /// <param name="eventId">The event id.</param>
        /// <param name="formattedMessage">The message.</param>
        /// <param name="payload">The payload.</param>
        /// <param name="timestamp">The timestamp.</param>
        /// <param name="schema">The schema.</param>
        public EventEntry(Guid sourceId, int eventId, string formattedMessage, ReadOnlyCollection<object> payload, DateTimeOffset timestamp, EventSchema schema)
        {
            this.providerId = sourceId;
            this.eventId = eventId;
            this.formattedMessage = formattedMessage;
            this.payload = payload;
            this.timestamp = timestamp;
            this.schema = schema;
        }

        /// <summary>
        /// Gets the id of the source originating the event.
        /// </summary>
        /// <value>The provider id.</value>
        public Guid ProviderId
        {
            get { return this.providerId; }
        }

        /// <summary>
        /// Gets the event id.
        /// </summary>
        /// <value>The event id.</value>
        public int EventId
        {
            get { return this.eventId; }
        }

        /// <summary>
        /// Gets the event payload.
        /// </summary>
        /// <value>The event payload.</value>
        public ReadOnlyCollection<object> Payload
        {
            get { return this.payload; }
        }

        /// <summary>
        /// Gets the timestamp of the event.
        /// </summary>
        /// <value>The timestamp of the event.</value>
        public DateTimeOffset Timestamp
        {
            get { return this.timestamp; }
        }

        /// <summary>
        /// Gets the event schema.
        /// </summary>
        /// <value>The event schema.</value>
        public EventSchema Schema
        {
            get { return this.schema; }
        }

        /// <summary>
        /// Gets the formatted message.
        /// </summary>
        /// <value>
        /// The formatted message.
        /// </value>
        public string FormattedMessage
        {
            get { return this.formattedMessage; }
        }

        /// <summary>
        /// Creates a new <see cref="EventEntry"/> instance based on the <paramref name="args"/> and the <paramref name="schema"/>.
        /// </summary>
        /// <param name="args">The <see cref="EventWrittenEventArgs"/> representing the event to log.</param>
        /// <param name="schema">The <see cref="EventSchema"/> for the source originating the event.</param>
        /// <returns>An entry describing the event.</returns>
        public static EventEntry Create(EventWrittenEventArgs args, EventSchema schema)
        {
            Guard.ArgumentNotNull(args, "args");
            Guard.ArgumentNotNull(schema, "schema");

            var timestamp = DateTimeOffset.Now;
            
            // TODO: validate whether we want to do this pro-actively or should we wait until the
            // last possible moment (as the formatted message might not be used in a sink).
            string formattedMessage = null;
            if (args.Message != null)
            {
                formattedMessage = string.Format(CultureInfo.InvariantCulture, args.Message, args.Payload.ToArray());
            }

            return new EventEntry(args.EventSource.Guid, args.EventId, formattedMessage, args.Payload, timestamp, schema);
        }

        /// <summary>
        /// Gets the formatted timestamp.
        /// </summary>
        /// <param name="format">The format.</param>
        /// <returns>The formatted string.</returns>
        public string GetFormattedTimestamp(string format)
        {
            return this.timestamp.UtcDateTime.ToString(string.IsNullOrWhiteSpace(format) ? DefaultDateTimeFormat : format, CultureInfo.InvariantCulture);
        }
    }
}
