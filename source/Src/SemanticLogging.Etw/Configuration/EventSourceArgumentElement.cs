// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System.Diagnostics;
using System.Xml.Linq;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw.Configuration
{
    /// <summary>
    /// Represents an argument to enable a provider.
    /// </summary>
    [DebuggerDisplay("{Key}-{Value}")]
    internal class EventSourceArgumentElement
    {
        private const string KeyAttributeKey = "key";
        private const string ValueAttributeKey = "value";

        internal static EventSourceArgumentElement Read(XElement element)
        {
            var instance =
                new EventSourceArgumentElement
                {
                    Key = (string)element.Attribute(KeyAttributeKey),
                    Value = (string)element.Attribute(ValueAttributeKey),
                };

            return instance;
        }

        /// <summary>
        /// Gets or sets the key.
        /// </summary>
        /// <value>
        /// The key.
        /// </value>
        public string Key { get; set; }

        /// <summary>
        /// Gets or sets the value.
        /// </summary>
        /// <value>
        /// The value.
        /// </value>
        public string Value { get; set; }
    }
}