// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Xml.Linq;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Configuration;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw.Configuration
{
    internal class ParameterElement
    {
        private static readonly XName ParametersName = XName.Get("parameters", Constants.Namespace);

        public string Name { get; set; }

        public string Type { get; set; }

        public string Value { get; set; }

        public IEnumerable<ParameterElement> Parameters { get; set; }

        internal static ParameterElement Read(XElement element)
        {
            return new ParameterElement()
            {
                Name = (string)element.Attribute("name"),
                Type = (string)element.Attribute("type"),
                Value = (string)element.Attribute("value"),
                Parameters = GetChildParameters(element)
            };
        }

        private static IEnumerable<ParameterElement> GetChildParameters(XElement element)
        {
            foreach (var e in element.Elements(ParametersName).Elements())
            {
                yield return ParameterElement.Read(e);
            }
        }
    }
}
