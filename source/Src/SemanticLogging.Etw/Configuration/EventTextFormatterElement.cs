// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Diagnostics.Tracing;
using System.Xml.Linq;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Configuration;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Formatters;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw.Configuration
{
    /// <summary>
    /// Represents a configuration element that can create an instance of <see cref="EventTextFormatter"/>.
    /// </summary>
    internal class EventTextFormatterElement : IFormatterElement
    {
        private readonly XName formatterName = XName.Get("eventTextFormatter", Constants.Namespace);

        /// <summary>
        /// Determines whether this instance [can create sink] the specified element.
        /// </summary>
        /// <param name="element">The element.</param>
        /// <returns>
        ///   <c>true</c> if this instance [can create sink] the specified element; otherwise, <c>false</c>.
        /// </returns>
        public bool CanCreateFormatter(XElement element)
        {
            return this.GetFormatterElement(element) != null;
        }

        /// <summary>
        /// Creates the <see cref="IEventTextFormatter" /> instance.
        /// </summary>
        /// <param name="element">The configuration element.</param>
        /// <returns>
        /// The formatter instance.
        /// </returns>
        public IEventTextFormatter CreateFormatter(XElement element)
        {
            var formatter = this.GetFormatterElement(element);

            EventLevel verbosityThreshold = (EventLevel)Enum.Parse(typeof(EventLevel), (string)formatter.Attribute("verbosityThreshold") ?? EventTextFormatter.DefaultVerbosityThreshold.ToString());

            return new EventTextFormatter(
                (string)formatter.Attribute("header"),
                (string)formatter.Attribute("footer"),
                verbosityThreshold,
                (string)formatter.Attribute("dateTimeFormat"));
        }

        private XElement GetFormatterElement(XElement element)
        {
            return element.Element(this.formatterName);
        }
    }
}
