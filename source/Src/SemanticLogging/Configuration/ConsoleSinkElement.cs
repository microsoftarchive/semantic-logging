// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Xml.Linq;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw.Utility;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Formatters;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Observable;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Utility;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Configuration
{
    /// <summary>
    /// Represents a console configuration element that can create an instance of a console sink.
    /// </summary>
    internal class ConsoleSinkElement : ISinkElement
    {
        private readonly XName sinkName = XName.Get("consoleSink", Constants.Namespace);

        /// <summary>
        /// Determines whether this instance can create the specified configuration element.
        /// </summary>
        /// <param name="element">The configuration element.</param>
        /// <returns>
        ///   <c>True</c> if this instance can create the specified element; otherwise, <c>false</c>.
        /// </returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1062:Validate arguments of public methods", MessageId = "0", Justification = "Validated with Guard class")]
        public bool CanCreateSink(XElement element)
        {
            Guard.ArgumentNotNull(element, "element");

            return element.Name == this.sinkName;
        }

        /// <summary>
        /// Creates the <see cref="IObserver{EventEntry}" /> instance for this sink.
        /// </summary>
        /// <param name="element">The configuration element.</param>
        /// <returns>
        /// The sink instance.
        /// </returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1062:Validate arguments of public methods", MessageId = "0", Justification = "Validated with Guard class")]
        public IObserver<EventEntry> CreateSink(XElement element)
        {
            Guard.ArgumentNotNull(element, "element");

            var subject = new EventEntrySubject();
            subject.LogToConsole(FormatterElementFactory.Get(element), XmlUtil.CreateInstance<IConsoleColorMapper>(element.Attribute("colorMapperType")));
            return subject;
        }
    }
}
