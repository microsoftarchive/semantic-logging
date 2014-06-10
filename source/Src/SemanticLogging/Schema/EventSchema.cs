// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Schema
{
    /// <summary>
    /// Represents an <see cref="EventSource"/> schema.
    /// </summary>
    public sealed class EventSchema
    {
        private readonly int id;
        private readonly Guid providerId;
        private readonly string providerName;
        private readonly string[] payload;
        private readonly EventTask task;
        private readonly string taskName;
        private readonly EventLevel level;
        private readonly int version;
        private readonly EventKeywords keywords;
        private readonly string keywordsDescription;
        private readonly EventOpcode opcode;
        private readonly string opcodeName;

        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Schema.EventSchema"/> class with the specified values.
        /// </summary>
        /// <param name="id">The event id.</param>
        /// <param name="providerId">The provider GUID.</param>
        /// <param name="providerName">The provider name.</param>
        /// <param name="level">The event level.</param>
        /// <param name="task">The event task.</param>
        /// <param name="taskName">The event task name.</param>
        /// <param name="opcode">The event operation code.</param>
        /// <param name="opcodeName">The event operation code name.</param>
        /// <param name="keywords">The event keywords.</param>
        /// <param name="keywordsDescription">The event keywords description.</param>
        /// <param name="version">The event version.</param>
        /// <param name="payload">The event payload.</param>
        public EventSchema(int id, Guid providerId, string providerName, EventLevel level, EventTask task, string taskName, EventOpcode opcode, string opcodeName, EventKeywords keywords, string keywordsDescription, int version, IEnumerable<string> payload)
        {
            this.id = id;
            this.providerId = providerId;
            this.providerName = providerName;
            this.level = level;
            this.task = task;
            this.taskName = taskName;
            this.opcode = opcode;
            this.opcodeName = opcodeName;
            this.keywords = keywords;
            this.keywordsDescription = keywordsDescription;
            this.version = version;
            this.payload = payload.ToArray();
        }

        /// <summary>
        /// Gets the event ID.
        /// </summary>
        /// <value>The event ID.</value>
        public int Id
        {
            get { return this.id; }
        }

        /// <summary>
        /// Gets the provider id.
        /// </summary>        
        /// <remarks>
        /// Provider GUID can be <see cref="Guid.Empty"/> for pre-Vista ETW providers.  
        /// </remarks>
        /// <value>The provider id.</value>
        public Guid ProviderId
        {
            get { return this.providerId; }
        }

        /// <summary>
        /// Gets the provider name.
        /// </summary>
        /// <value>The provider name.</value>
        public string ProviderName
        {
            get { return this.providerName; }
        }

        /// <summary>
        /// Gets the event task.
        /// </summary>
        /// <remarks>
        /// Events for a given provider can be given a group identifier called a Task that indicates the
        /// broad area within the provider that the event pertains to (for example the Kernel provider has
        /// Tasks for Process, Threads, etc). 
        /// </remarks>
        /// <value>The event task.</value>
        public EventTask Task
        {
            get { return this.task; }
        } // TODO: ushort?

        /// <summary>
        /// Gets the task name.
        /// </summary>
        /// <value>The task name.</value>
        public string TaskName
        {
            get { return this.taskName; }
        }

        /// <summary>
        /// Gets the payload names that maps to the event signature parameter names.
        /// </summary>
        /// <value>The event payload.</value>
        public string[] Payload
        {
            get { return this.payload; }
        }

        /// <summary>
        /// Gets the operation code.
        /// </summary>
        /// <remarks>
        /// Each event has a Type identifier that indicates what kind of an event is being logged. Note that
        /// providers are free to extend this set, so the id may not be just the value in <see cref="Opcode"/>.
        /// </remarks>
        /// <value>The operation code.</value>
        public EventOpcode Opcode
        {
            get { return this.opcode; }
        }

        /// <summary>
        /// Gets the human-readable string name for the <see cref="EventSchema.Opcode"/> property. 
        /// </summary>
        /// <value>The operation code name.</value>
        public string OpcodeName
        {
            get { return this.opcodeName; }
        }

        /// <summary>
        /// Gets the event level.
        /// </summary>
        /// <value>The event level.</value>
        public EventLevel Level
        {
            get { return this.level; }
        }

        /// <summary>
        /// Gets the event version.
        /// </summary>
        /// <value>The event version.</value>
        public int Version
        {
            get { return this.version; }
        }

        /// <summary>
        /// Gets the event keywords.
        /// </summary>
        /// <value>The event keywords.</value>
        public EventKeywords Keywords
        {
            get { return this.keywords; }
        }

        /// <summary>
        /// Gets the human-readable string name for the <see cref="EventSchema.Keywords"/> property. 
        /// </summary>
        /// <value>The keyword description.</value>
        public string KeywordsDescription
        {
            get { return this.keywordsDescription; }
        }

        /// <summary>
        /// Gets the name for the event.
        /// </summary>
        /// <remarks>
        /// This is simply the concatenation of the task and operation code names.
        /// </remarks>
        /// <value>The event name.</value>
        public string EventName
        {
            get { return this.TaskName + this.OpcodeName; }
        }
    }
}
