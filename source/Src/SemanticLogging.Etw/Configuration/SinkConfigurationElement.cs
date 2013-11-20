// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Xml.Linq;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw.Configuration
{
    internal class SinkConfigurationElement
    {
        internal string Name { get; set; }

        internal Lazy<IObserver<EventEntry>> SinkPromise { get; set; }

        internal IEnumerable<EventSourceElement> EventSources { get; set; }

        internal string SinkConfiguration { get; set; }
    }
}
