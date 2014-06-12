// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Configuration;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw.Configuration;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Utility;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw.Utility
{
    internal static class XmlUtil
    {
        private static readonly XName ParametersName = XName.Get("parameters", Constants.Namespace);

        //// Recreates the element structure in a ordered way (attributes and child elements) to get accurate element comparisons 
        internal static XElement DeepNormalization(this XElement element)
        {
            if (element.HasElements)
            {
                return new XElement(
                    element.Name,
                    element.Attributes().OrderBy(a => a.Name.ToString()),
                    element.Elements().OrderBy(a => a.Name.ToString()).Select(e => DeepNormalization(e)));
            }

            return new XElement(element.Name, element.Attributes().OrderBy(a => a.Name.ToString()), element.IsEmpty ? null : element.Value);
        }

        internal static T CreateInstance<T>(XAttribute attribute)
        {
            string attributeValue = (string)attribute;
            if (!string.IsNullOrWhiteSpace(attributeValue))
            {
                return (T)Activator.CreateInstance(Type.GetType(attributeValue, true));
            }

            return default(T);
        }

        internal static T CreateInstance<T>(XElement element)
        {
            Guard.ArgumentNotNull(element, "element");
            string type = (string)element.Attribute("type");
            Guard.ArgumentNotNullOrEmpty(type, "type");

            try
            {
                return (T)Activator.CreateInstance(Type.GetType(type, true), BuildArgs(element));
            }
            catch (MissingMethodException e)
            {
                throw new ArgumentException(Properties.Resources.IncompleteArgumentsError, e);
            }
        }

        internal static object[] BuildArgs(XElement element)
        {
            List<ParameterElement> parameters = new List<ParameterElement>();

            foreach (var e in element.Elements(ParametersName).Elements())
            {
                parameters.Add(ParameterElement.Read(e));
            }

            return BuildArgs(parameters);
        }

        private static object[] BuildArgs(IEnumerable<ParameterElement> parameters)
        {
            var args = new List<object>();

            foreach (var parameter in parameters)
            {
                var type = System.Type.GetType(parameter.Type, true);

                if (parameter.Value != null)
                {
                    args.Add(TypeDescriptor.GetConverter(type).ConvertFromString(parameter.Value));
                    continue;
                }

                args.Add(parameter.Parameters.Count() > 0 ?
                    Activator.CreateInstance(type, BuildArgs(parameter.Parameters)) :
                    Activator.CreateInstance(type));
            }

            return args.ToArray();
        }
    }
}
