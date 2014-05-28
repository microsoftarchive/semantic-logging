// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Utility;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Sinks.Database
{
    /// <summary>
    /// Represents an event entry to be persisted in a database.
    /// </summary>
    public class EventRecord
    {
        private readonly EventEntry eventEntry;

        /// <summary>
        /// Initializes a new instance of the <see cref="EventRecord"/> class.
        /// </summary>
        /// <param name="eventEntry">The event entry.</param>
        public EventRecord(EventEntry eventEntry)
        {
            this.eventEntry = eventEntry;
            this.Payload = EventEntryUtil.JsonSerializePayload(eventEntry);
        }

        /// <summary>
        /// Gets or sets the instance name where the entries are generated from.
        /// </summary>
        /// <value>The instance name.</value>
        public string InstanceName { get; set; }

        /// <summary>
        /// Gets or sets the event payload.
        /// </summary>
        /// <value>The event payload.</value>
        public string Payload { get; private set; }

        /// <summary>
        /// Gets or sets the id of the source originating the event.
        /// </summary>
        /// <value>The event source id.</value>
        public Guid ProviderId
        {
            get { return this.eventEntry.ProviderId; }
        }

        /// <summary>
        /// Gets or sets the friendly name of the class that is derived from the event source.
        /// </summary>
        /// <value>The provider name.</value>
        public string ProviderName
        {
            get { return this.eventEntry.Schema.ProviderName; }
        }

        /// <summary>
        /// Gets or sets the event id.
        /// </summary>
        /// <value>The event id.</value>
        public int EventId
        {
            get { return this.eventEntry.EventId; }
        }

        /// <summary>
        /// Gets or sets the event keywords.
        /// </summary>
        /// <value>The event keywords.</value>
        public long EventKeywords
        {
            get { return (long)this.eventEntry.Schema.Keywords; }
        }

        /// <summary>
        /// Gets or sets the event level.
        /// </summary>
        /// <value>The event level.</value>
        public int Level
        {
            get { return (int)this.eventEntry.Schema.Level; }
        }

        /// <summary>
        /// Gets or sets the operation code.
        /// </summary>
        /// <value>The operation code.</value>
        public int Opcode
        {
            get { return (int)this.eventEntry.Schema.Opcode; }
        }

        /// <summary>
        /// Gets or sets the task for the event.
        /// </summary>
        /// <value>The task for the event.</value>
        public int Task
        {
            get { return (int)this.eventEntry.Schema.Task; }
        }

        /// <summary>
        /// Gets or sets the timestamp of the event.
        /// </summary>
        /// <value>The timestamp of the event.</value>
        public DateTimeOffset Timestamp
        {
            get { return this.eventEntry.Timestamp; }
        }

        /// <summary>
        /// Gets or sets the event version.
        /// </summary>
        /// <value>The event version.</value>
        public int Version
        {
            get { return this.eventEntry.Schema.Version; }
        }

        /// <summary>
        /// Gets or sets the formatted message.
        /// </summary>
        /// <value>The formatted message.</value>
        public string FormattedMessage
        {
            get { return this.eventEntry.FormattedMessage; }
        }

        /// <summary>
        /// Gets or sets the process id.
        /// </summary>
        /// <value>The process id.</value>
        public int ProcessId
        {
            get { return this.eventEntry.ProcessId; }
        }

        /// <summary>
        /// Gets or sets the thread id.
        /// </summary>
        /// <value>The thread id.</value>
        public int ThreadId
        {
            get { return this.eventEntry.ThreadId; }
        }

        /// <summary>
        /// Gets or sets the activity id.
        /// </summary>
        /// <value>The activity id.</value>
        public Guid ActivityId
        {
            get { return this.eventEntry.ActivityId; }
        }

        /// <summary>
        /// Gets or sets the related activity id.
        /// </summary>
        /// <value>The related activity id.</value>
        public Guid RelatedActivityId
        {
            get { return this.eventEntry.RelatedActivityId; }
        }
    }
}
