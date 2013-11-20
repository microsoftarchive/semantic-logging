// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Diagnostics.Tracing;
using System.Globalization;
using System.Xml.Linq;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw.Configuration
{
    /// <summary>
    /// Represents the configuration class for an event source element.
    /// </summary>
    internal class EventSourceElement
    {
        private const string NameAttributeKey = "name";
        private const string EventIdAttributeKey = "id";
        private const string LevelAttributeKey = "level";
        private const string MatchAnyKeywordAttributeKey = "matchAnyKeyword";

        /// <summary>
        /// Gets or sets the name of the event source.
        /// </summary>
        /// <value>
        /// The name identifier.
        /// </value>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the event id of the event source.
        /// </summary>
        /// <value>
        /// The event id.
        /// </value>
        public Guid EventId { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="EventLevel" /> to enable events.
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

        internal static EventSourceElement Read(XElement element)
        {
            var instance = new EventSourceElement()
            {
                Name = (string)element.Attribute(NameAttributeKey),
                EventId = (Guid?)element.Attribute(EventIdAttributeKey) ?? Guid.Empty,
                Level = (EventLevel)Enum.Parse(typeof(EventLevel), (string)element.Attribute(LevelAttributeKey) ?? default(EventLevel).ToString()),
                MatchAnyKeyword = (EventKeywords)long.Parse((string)element.Attribute(MatchAnyKeywordAttributeKey) ?? ((long)default(EventKeywords)).ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture)
            };

            return instance;
        }
    }
}
