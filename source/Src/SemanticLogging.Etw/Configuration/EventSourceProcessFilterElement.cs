// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System.Diagnostics;
using System.Xml.Linq;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw.Configuration
{
    /// <summary>
    /// Represents a process name to filter.
    /// </summary>
    [DebuggerDisplay("Process {Name}")]
    internal class EventSourceProcessFilterElement
    {
        private const string NameAttributeKey = "name";

        internal static EventSourceProcessFilterElement Read(XElement element)
        {
            var instance =
                new EventSourceProcessFilterElement
                {
                    Name = (string)element.Attribute(NameAttributeKey),
                };

            return instance;
        }

        /// <summary>
        /// Gets or sets the process name.
        /// </summary>
        /// <value>
        /// The process name.
        /// </value>
        public string Name { get; set; }
    }
}