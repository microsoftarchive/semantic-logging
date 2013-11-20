// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System.IO;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Formatters
{
    /// <summary>
    /// Provides a generic interface for an event text formatter used 
    /// whenever an event has been written by an event source for which the event listener has enabled events.
    /// </summary>
    public interface IEventTextFormatter
    {
        /// <summary>
        /// Writes the event.
        /// </summary>
        /// <param name="eventEntry">The event entry.</param>
        /// <param name="writer">The writer.</param>
        void WriteEvent(EventEntry eventEntry, TextWriter writer);
    }
}
