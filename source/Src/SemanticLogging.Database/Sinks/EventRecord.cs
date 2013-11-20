// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Sinks.Database
{
    /// <summary>
    /// Represents an event entry to be persisted in a database.
    /// </summary>
    public class EventRecord
    {
        /// <summary>
        /// Gets or sets the instance name where the entries are generated from.
        /// </summary>
        /// <value>The instance name.</value>
        public string InstanceName { get; set; }

        /// <summary>
        /// Gets or sets the id of the source originating the event.
        /// </summary>
        /// <value>The event source id.</value>
        public Guid ProviderId { get; set; }

        /// <summary>
        /// Gets or sets the friendly name of the class that is derived from the event source.
        /// </summary>
        /// <value>The provider name.</value>
        public string ProviderName { get; set; }

        /// <summary>
        /// Gets or sets the event id.
        /// </summary>
        /// <value>The event id.</value>
        public int EventId { get; set; }

        /// <summary>
        /// Gets or sets the event keywords.
        /// </summary>
        /// <value>The event keywords.</value>
        public long EventKeywords { get; set; }

        /// <summary>
        /// Gets or sets the event level.
        /// </summary>
        /// <value>The event level.</value>
        public int Level { get; set; }

        /// <summary>
        /// Gets or sets the operation code.
        /// </summary>
        /// <value>The operation code.</value>
        public int Opcode { get; set; }

        /// <summary>
        /// Gets or sets the task for the event.
        /// </summary>
        /// <value>The task for the event.</value>
        public int Task { get; set; }

        /// <summary>
        /// Gets or sets the timestamp of the event.
        /// </summary>
        /// <value>The timestamp of the event.</value>
        public DateTimeOffset Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the event version.
        /// </summary>
        /// <value>The event version.</value>
        public int Version { get; set; }

        /// <summary>
        /// Gets or sets the formatted message.
        /// </summary>
        /// <value>The formatted message.</value>
        public string FormattedMessage { get; set; }

        /// <summary>
        /// Gets or sets the event payload.
        /// </summary>
        /// <value>The event payload.</value>
        public string Payload { get; set; }
    }
}
