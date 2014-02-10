using System;
using System.Collections.Generic;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Sinks
{
    public sealed class JsonEventEntry
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="JsonEventEntry"/> class.
        /// </summary>
        public JsonEventEntry()
        {
            Payload = new Dictionary<string, object>();
            EventDate = DateTime.UtcNow;
        }

        /// <summary>
        /// Gets or sets the event identifier.
        /// </summary>
        /// <value>
        /// The event id.
        /// </value>
        public int EventId { get; set; }

        /// <summary>
        /// Gets or sets the event date.
        /// </summary>
        /// <value>
        /// The event date.
        /// </value>
        public DateTime EventDate { get; set; }

        /// <summary>
        /// Gets or sets the keywords for the event.
        /// </summary>
        /// <value>
        /// The keywords.
        /// </value>
        public long Keywords { get; set; }

        /// <summary>
        /// Gets or sets the unique identifier for the provider, which is typically the class derived from <see cref="System.Diagnostics.Tracing.EventSource"/>.
        /// </summary>
        /// <value>
        /// The provider ID.
        /// </value>
        public Guid ProviderId { get; set; }

        /// <summary>
        /// Gets or sets the friendly name of the class that is derived from the event source.
        /// </summary>
        /// <value>
        /// The name of the event source.
        /// </value>
        public string ProviderName { get; set; }

        /// <summary>
        /// Gets or sets the instance name where the entries are generated from.
        /// </summary>
        /// <value>
        /// The name of the instance.
        /// </value>
        public string InstanceName { get; set; }

        /// <summary>
        /// Gets or sets the level of the event.
        /// </summary>
        /// <value>
        /// The level.
        /// </value>
        public int Level { get; set; }

        /// <summary>
        /// Gets or sets the message for the event.
        /// </summary>
        /// <value>
        /// The message.
        /// </value>
        public string Message { get; set; }

        /// <summary>
        /// Gets or sets the operation code for the event.
        /// </summary>
        /// <value>
        /// The operation code.
        /// </value>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming",
            "CA1702:CompoundWordsShouldBeCasedCorrectly", MessageId = "Opcode",
            Justification = "Uses casing from EventWrittenEventArgs.Opcode")]
        public int Opcode { get; set; }

        /// <summary>
        /// Gets or sets the task for the event.
        /// </summary>
        /// <value>
        /// The task code.
        /// </value>
        public int Task { get; set; }

        /// <summary>
        /// Gets or sets the version of the event.
        /// </summary>
        /// <value>
        /// The version.
        /// </value>
        public int Version { get; set; }

        /// <summary>
        /// Gets or sets the payload for the event.
        /// </summary>
        /// <value>
        /// The payload.
        /// </value>
        public Dictionary<string, object> Payload { get; set; }

        /// <summary>
        /// Gets or sets the activity id for the event.
        /// </summary>
        /// <value>
        /// The activity id.
        /// </value>
        public Guid ActivityId { get; set; }

        /// <summary>
        /// Gets or sets the related activity id for the event.
        /// </summary>
        /// <value>
        /// The related activity id.
        /// </value>
        public Guid RelatedActivityId { get; set; }
    }
}