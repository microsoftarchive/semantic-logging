// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Xml.Linq;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Configuration;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Formatters;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw.Configuration
{
    /// <summary>
    /// Represents a configuration element that can create an instance of <see cref="JsonEventTextFormatter"/>.
    /// </summary>
    internal class JsonEventTextFormatterElement : IFormatterElement
    {
        private readonly XName formatterName = XName.Get("jsonEventTextFormatter", Constants.Namespace);

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

            EventTextFormatting formatting = (EventTextFormatting)Enum.Parse(typeof(EventTextFormatting), (string)formatter.Attribute("formatting") ?? JsonEventTextFormatter.DefaultEventTextFormatting.ToString());

            var includeSeparator = formatter.Attribute("includeEntrySeparator");

            var jsonFormatter = new JsonEventTextFormatter(formatting, (string)formatter.Attribute("dateTimeFormat"));
            if (includeSeparator != null)
            {
                jsonFormatter.IncludeEntrySeparator = (bool)includeSeparator;
            }

            return jsonFormatter;
        }

        private XElement GetFormatterElement(XElement element)
        {
            return element.Element(this.formatterName);
        }
    }
}
