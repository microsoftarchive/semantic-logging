// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Schema;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Sinks
{
    /// <summary>
    /// EventEntry formatted for use with Elasticsearch
    /// </summary>
    public sealed class JsonEventEntry
    {
        private readonly EventEntry eventEntry;

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonEventEntry"/> class.
        /// </summary>
        public JsonEventEntry(EventEntry eventEntry)
        {
            this.eventEntry = eventEntry;
            Payload = InitializePayload(eventEntry.Payload, eventEntry.Schema);
        }

        /// <summary>
        /// Gets or sets the event entry.
        /// </summary>
        /// <value>
        /// The event entry.
        /// </value>
        public EventEntry EventEntry
        {
            get { return this.eventEntry; }
        }

        /// <summary>
        /// Gets or sets the instance name where the entries are generated from.
        /// </summary>
        /// <value>
        /// The name of the instance.
        /// </value>
        public string InstanceName { get; set; }

        /// <summary>
        /// Gets or sets the payload for the event.
        /// </summary>
        /// <value>
        /// The payload.
        /// </value>
        public IReadOnlyDictionary<string, object> Payload { get; private set; }

        private static IReadOnlyDictionary<string, object> InitializePayload(IList<object> payload, EventSchema schema)
        {
            var payloadDictionary = new Dictionary<string, object>(payload.Count);

            try
            {
                for (int i = 0; i < payload.Count; i++)
                {
                    payloadDictionary.Add(schema.Payload[i], payload[i]);
                }
            }
            catch (Exception e)
            {
                SemanticLoggingEventSource.Log.ElasticsearchSinkEntityPayloadCreationFailed(e.ToString());
            }

            return payloadDictionary;
        }
    }
}