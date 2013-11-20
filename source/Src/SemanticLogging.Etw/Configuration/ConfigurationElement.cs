// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw.Configuration
{
    internal class ConfigurationElement
    {
        internal TraceEventServiceElement TraceEventService { get; set; }

        internal IEnumerable<SinkConfigurationElement> SinkConfigurationElements { get; set; }
    }
}
