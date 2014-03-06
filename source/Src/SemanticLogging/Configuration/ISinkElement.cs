// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Xml.Linq;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Configuration
{
    /// <summary>
    /// Represents the contract for a sink configuration element.
    /// </summary>
    public interface ISinkElement
    {
        /// <summary>
        /// Determines whether this instance can create the specified configuration element.
        /// </summary>
        /// <param name="element">The configuration element.</param>
        /// <returns>
        ///   <c>True</c> if this instance can create the specified element; otherwise, <c>false</c>.
        /// </returns>
        bool CanCreateSink(XElement element);

        /// <summary>
        /// Creates the <see cref="IObserver{EventEntry}" /> instance for this sink.
        /// </summary>
        /// <param name="element">The configuration element.</param>
        /// <returns>
        /// The observer instance.
        /// </returns>
        IObserver<EventEntry> CreateSink(XElement element);
    }
}
