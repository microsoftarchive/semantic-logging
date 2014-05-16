// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using Microsoft.Diagnostics.Tracing.Session;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw.Configuration
{
    /// <summary>
    /// Represents the event source configuration settings.
    /// </summary>
    public class EventSourceSettings
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EventSourceSettings" /> class.
        /// </summary>
        /// <param name="name">The friendly event source name.</param>
        /// <param name="eventSourceId">The event source id.</param>
        /// <param name="level">The level.</param>
        /// <param name="matchAnyKeyword">The match any keyword.</param>
        /// <param name="arguments">The arguments for the event source.</param>
        /// <param name="processNameFilters">The the process filters.</param>
        /// <exception cref="ConfigurationException">A validation exception.</exception>
        public EventSourceSettings(
            string name = null,
            Guid? eventSourceId = null,
            EventLevel level = EventLevel.LogAlways,
            EventKeywords matchAnyKeyword = Keywords.All,
            IEnumerable<KeyValuePair<string, string>> arguments = null,
            IEnumerable<string> processNameFilters = null)
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
            this.Arguments = arguments ?? Enumerable.Empty<KeyValuePair<string, string>>();
            this.ProcessNamesToFilter = processNameFilters ?? Enumerable.Empty<string>();
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

        /// <summary>
        /// Gets the arguments to use when enabling the ETW provider.
        /// </summary>
        /// <value>
        /// The arguments.
        /// </value>
        public IEnumerable<KeyValuePair<string, string>> Arguments { get; private set; }

        /// <summary>
        /// Gets the process names to filter when enabling the ETW provider.
        /// </summary>
        /// <value>
        /// The process names to filter.
        /// </value>
        public IEnumerable<string> ProcessNamesToFilter { get; private set; }

        internal void CopyValuesFrom(EventSourceSettings settings)
        {
            this.Level = settings.Level;
            this.MatchAnyKeyword = settings.MatchAnyKeyword;
            this.Arguments = settings.Arguments;
            this.ProcessNamesToFilter = settings.ProcessNamesToFilter;
        }
    }
}