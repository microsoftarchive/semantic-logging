// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System.Xml.Linq;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Formatters;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Configuration
{
    /// <summary>
    /// Represents the contract for creating formatters from configuration elements.
    /// </summary>
    public interface IFormatterElement
    {
        /// <summary>
        /// Determines whether this instance can create the specified configuration element.
        /// </summary>
        /// <param name="element">The configuration element.</param>
        /// <returns>
        ///   <c>True</c> if this instance can create formatter the specified element; otherwise, <c>false</c>.
        /// </returns>
        bool CanCreateFormatter(XElement element);

        /// <summary>
        /// Creates the <see cref="IEventTextFormatter" /> instance.
        /// </summary>
        /// <param name="element">The configuration element.</param>
        /// <returns>
        /// The formatter instance.
        /// </returns>
        IEventTextFormatter CreateFormatter(XElement element);
    }
}
