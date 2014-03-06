// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw.Configuration
{
    using System.Xml.Linq;

    using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Configuration;
    using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw.Utility;
    using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Formatters;

    internal class CustomFormatterElement : IFormatterElement
    {
        private readonly XName formatterName = XName.Get("customEventTextFormatter", Constants.Namespace);

        public bool CanCreateFormatter(XElement element)
        {
            return this.GetFormatterElement(element) != null;
        }

        public IEventTextFormatter CreateFormatter(XElement element)
        {
            return XmlUtil.CreateInstance<IEventTextFormatter>(this.GetFormatterElement(element));
        }

        private XElement GetFormatterElement(XElement element)
        {
            return element.Element(this.formatterName);
        }
    }
}
