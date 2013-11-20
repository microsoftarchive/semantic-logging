// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Utility;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw.Configuration
{
    /// <summary>
    /// Configuration settings for a Sink.
    /// </summary>
    public class SinkSettings
    {
        private IObserver<EventEntry> sink;
        private Lazy<IObserver<EventEntry>> sinkPromise;

        /// <summary>
        /// Initializes a new instance of the <see cref="SinkSettings" /> class.
        /// </summary>
        /// <param name="name">The event listener name.</param>
        /// <param name="sink">The sink instance.</param>
        /// <param name="eventSources">The event sources.</param>
        /// <exception cref="ConfigurationException">Validation exceptions.</exception>
        public SinkSettings(string name, IObserver<EventEntry> sink, IEnumerable<EventSourceSettings> eventSources)
            : this(name, eventSources)
        {
            Guard.ArgumentNotNull(sink, "sink");

            this.Sink = sink;
        }

        internal SinkSettings(string name, Lazy<IObserver<EventEntry>> sinkPromise, IEnumerable<EventSourceSettings> eventSources)
            : this(name, eventSources)
        {
            Guard.ArgumentNotNull(sinkPromise, "sinkPromise");

            this.sinkPromise = sinkPromise;
        }

        private SinkSettings(string name, IEnumerable<EventSourceSettings> eventSources)
        {
            Guard.ArgumentNotNullOrEmpty(name, "name");
            Guard.ArgumentLowerOrEqualThan<int>(200, name.Length, "name.Length");
            Guard.ArgumentNotNull(eventSources, "eventSources");

            // Do not allow an empty source list 
            if (eventSources.Count() == 0)
            {
                throw new ConfigurationException(string.Format(CultureInfo.CurrentCulture, Properties.Resources.NoEventSourcesError, name));
            }

            // Validate duplicate sources by name
            var duplicateByName = eventSources.GroupBy(l => l.Name).FirstOrDefault(g => g.Count() > 1);
            if (duplicateByName != null)
            {
                throw new ConfigurationException(string.Format(CultureInfo.CurrentCulture, Properties.Resources.DuplicateEventSourceNameError, duplicateByName.First().Name, name));
            }

            // Validate duplicate sources by id
            var duplicateById = eventSources.GroupBy(l => l.EventSourceId).FirstOrDefault(g => g.Count() > 1);
            if (duplicateById != null)
            {
                throw new ConfigurationException(string.Format(CultureInfo.CurrentCulture, Properties.Resources.DuplicateEventSourceIdError, duplicateById.First().Name, name));
            }

            this.Name = name;
            this.EventSources = eventSources;
        }

        /// <summary>
        /// Gets or sets the sink.
        /// </summary>
        /// <value>
        /// The event listener.
        /// </value>
        public IObserver<EventEntry> Sink
        {
            get { return this.sink ?? this.sinkPromise.Value; }
            set { this.sink = value; }
        }

        /// <summary>
        /// Gets or sets the name of the event listener.
        /// </summary>
        /// <value>
        /// The name identifier.
        /// </value>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the event sources.
        /// </summary>
        /// <value>
        /// The event sources.
        /// </value>
        public IEnumerable<EventSourceSettings> EventSources { get; set; }

        internal string SinkConfiguration { get; set; }
    }
}
