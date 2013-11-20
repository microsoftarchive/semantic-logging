// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System.Xml.Linq;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw.Configuration
{
    internal class TraceEventServiceElement
    {
        internal TraceEventServiceElement()
        {
            this.SessionNamePrefix = Constants.DefaultSessionNamePrefix;
        }

        internal string SessionNamePrefix { get; set; }

        internal static TraceEventServiceElement Read(XElement element)
        {
            var instance = new TraceEventServiceElement();

            var snpAttr = (string)element.Attribute("sessionNamePrefix");
            if (!string.IsNullOrWhiteSpace(snpAttr))
            {
                instance.SessionNamePrefix = snpAttr;
            }

            return instance;
        }
    }
}
