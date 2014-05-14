// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Session;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw.Configuration
{
    /// <summary>
    /// Represents the event source configuration settings.
    /// </summary>
    public class EventSourceSettings
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EventSourceSettings"/> class.
        /// </summary>
        /// <param name="name">The friendly event source name.</param>
        /// <param name="eventSourceId">The event source id.</param>
        /// <param name="level">The level.</param>
        /// <param name="matchAnyKeyword">The match any keyword.</param>
        /// <exception cref="ConfigurationException">A validation exception.</exception>
        public EventSourceSettings(string name = null, Guid? eventSourceId = null, EventLevel level = EventLevel.LogAlways, EventKeywords matchAnyKeyword = Keywords.All)
        {
            // If no Id, Name should not be optional so we may derive an Id from it.
            if (!eventSourceId.HasValue || eventSourceId == Guid.Empty)
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    throw new ConfigurationException(Properties.Resources.MissingEventSourceNameAndId);
                }

                eventSourceId = TraceEventProviders.GetEventSourceGuidFromName(name);
            }
            else if (!string.IsNullOrWhiteSpace(name))
            {
                // throw and both name & Id specified
                throw new ConfigurationException(Properties.Resources.EventSourceAmbiguityError);
            }

            this.EventSourceId = eventSourceId.Value;
            this.Name = name ?? eventSourceId.ToString(); // Set a not null value for later use
            this.Level = level;
            this.MatchAnyKeyword = matchAnyKeyword;
        }

        /// <summary>
        /// Gets or sets the event source ID to monitor traced events.
        /// </summary>
        /// <value>
        /// The name identifier.
        /// </value>
        public string Name { get; set; }

        /// <summary>
        /// Gets the event source ID to monitor traced events.
        /// </summary>
        /// <value>
        /// The event source id.
        /// </value>
        public Guid EventSourceId { get; internal set; }

        /// <summary>
        /// Gets or sets the <see cref="EventLevel" />.
        /// </summary>
        /// <value>
        /// The event level.
        /// </value>
        public EventLevel Level { get; set; }

        /// <summary>
        /// Gets or sets the keyword flags necessary to enable the events.
        /// </summary>
        /// <value>
        /// The <see cref="EventKeywords"/>.
        /// </value>
        public EventKeywords MatchAnyKeyword { get; set; }
    }
}
