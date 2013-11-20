// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw.Utility;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Utility;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw.Configuration
{
    internal class ConfigurationReader
    {
        private const string SchemaFileName = "SemanticLogging-svc.xsd";
        private static readonly XNamespace NamespaceName = Constants.Namespace;
        private readonly ExtensionsLoader loader;
        private readonly XName traceEventServiceName = NamespaceName + "traceEventService";
        private readonly XName sinksName = NamespaceName + "sinks";
        private readonly XName sourcesName = NamespaceName + "sources";
        private readonly XName eventSourceName = NamespaceName + "eventSource";

        internal ConfigurationReader(string file)
        {
            var fileInfo = FileUtil.ProcessFileNameForLogging(file);
            this.File = fileInfo.FullName;
            this.loader = ExtensionsLoader.GetOrCreateInstance(fileInfo.DirectoryName);
            FormatterElementFactory.FormatterElements = this.loader.FormatterElements;
        }

        internal string File { get; private set; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Exceptions aggregated")]
        internal ConfigurationElement Read()
        {
            XmlSchemaSet schemas = new XmlSchemaSet();

            // load buit-in schema from resource
            schemas.Add(GetSchemaFromResource());

            if (this.loader.SchemaFileNames.Any())
            {
                AddExtensionSchemas(schemas, this.loader);
            }

            var readerSettings = new XmlReaderSettings()
            {
                Schemas = schemas,
                CloseInput = true,
                XmlResolver = new SchemaConfigurationXmlResolver(),
                ValidationType = ValidationType.Schema,
                ValidationFlags = XmlSchemaValidationFlags.ProcessIdentityConstraints |
                                  XmlSchemaValidationFlags.ReportValidationWarnings |
                                  XmlSchemaValidationFlags.ProcessSchemaLocation |
                                  XmlSchemaValidationFlags.ProcessInlineSchema
            };

            var validationExceptions = new List<Exception>();
            readerSettings.ValidationEventHandler += (s, a) =>
                {
                    //// Filter out any missing schema warnings for custom elements and its child sub elements
                    if (a.Severity == XmlSeverityType.Warning)
                    {
                        var reader = (XmlReader)s;
                        if (!schemas.Contains(reader.NamespaceURI) || reader.NamespaceURI == Constants.Namespace)
                        {
                            return;
                        }
                    }

                    //// Collect all schema validation errors
                    validationExceptions.Add(a.Exception);
                };

            XDocument doc = null;
            using (var reader = XmlReader.Create(this.File, readerSettings))
            {
                try
                {
                    doc = XDocument.Load(reader, LoadOptions.SetLineInfo);
                }
                catch (Exception e)
                {
                    validationExceptions.Add(e);
                }
            }

            if (validationExceptions.Count > 0)
            {
                throw new ConfigurationException(validationExceptions) { ConfigurationFile = this.File };
            }

            var configuration = new ConfigurationElement();

            var tes = doc.Root.Element(this.traceEventServiceName);
            configuration.TraceEventService = (tes == null) ? new TraceEventServiceElement() : TraceEventServiceElement.Read(tes);

            var sinks = doc.Root.Elements(this.sinksName).Elements();
            if (sinks != null)
            {
                configuration.SinkConfigurationElements = this.LoadSinkConfigurationElements(sinks);
            }

            return configuration;
        }

        private static void AddExtensionSchemas(XmlSchemaSet schemas, ExtensionsLoader loader)
        {
            if (schemas == null)
            {
                schemas = new XmlSchemaSet();
            }

            var validationExceptions = new List<Exception>();

            foreach (var xsdFile in loader.SchemaFileNames)
            {
                using (var reader = XmlReader.Create(xsdFile, new XmlReaderSettings() { CloseInput = true }))
                {
                    var schema = XmlSchema.Read(reader, (s, a) => validationExceptions.Add(a.Exception));
                    if (schema.TargetNamespace != Constants.Namespace)
                    {
                        schemas.Add(schema);
                    }
                }
            }

            if (validationExceptions.Count > 0)
            {
                throw new ConfigurationException(validationExceptions);
            }
        }

        private static XmlSchema GetSchemaFromResource()
        {
            var thisAssembly = Assembly.GetExecutingAssembly();
            var baseNs = typeof(ConfigurationReader).Namespace;
            using (var reader = XmlReader.Create(thisAssembly.GetManifestResourceStream(baseNs + "." + SchemaFileName), new XmlReaderSettings() { CloseInput = true }))
            {
                return XmlSchema.Read(reader, (s, a) => { throw new ConfigurationException(a.Exception); });
            }
        }

        private static Exception ElementInfoException(XElement element)
        {
            string lineInfo = null;
            var info = element as IXmlLineInfo;
            if (info != null && info.HasLineInfo())
            {
                lineInfo = string.Format(CultureInfo.CurrentCulture, Properties.Resources.LineInfoMessage, Environment.NewLine, info.LineNumber, info.LinePosition);
            }

            return new Exception(string.Format(CultureInfo.CurrentCulture, Properties.Resources.ElementInfoErrorMessage, Environment.NewLine, element, lineInfo));
        }

        private IEnumerable<SinkConfigurationElement> LoadSinkConfigurationElements(IEnumerable<XElement> sinks)
        {
            var sinkConfigurationElements = new List<SinkConfigurationElement>();

            foreach (var @sink in sinks)
            {
                var instance = this.loader.SinkElements.FirstOrDefault(s => s.Value.CanCreateSink(@sink));
                if (instance == null)
                {
                    throw new ConfigurationException(string.Format(CultureInfo.CurrentCulture, Properties.Resources.SinkElementNotResolvedError, @sink.Name.LocalName));
                }

                sinkConfigurationElements.Add(new SinkConfigurationElement()
                {
                    Name = (string)@sink.Attribute("name"),
                    SinkPromise = new Lazy<IObserver<EventEntry>>(() =>
                    {
                        try
                        {
                            return instance.Value.CreateSink(@sink);
                        }
                        catch (Exception e)
                        {
                            throw new ConfigurationException(e, ElementInfoException(@sink));
                        }
                    }),
                    EventSources = this.ReadEventSources(@sink),
                    SinkConfiguration = this.CreateSinkConfiguration(@sink)
                });
            }

            return sinkConfigurationElements;
        }

        private string CreateSinkConfiguration(XElement sink)
        {
            var clone = new XElement(sink);

            // Remove EventSources elements
            clone.Elements(this.sourcesName).Remove();

            return clone.DeepNormalization().ToString(SaveOptions.DisableFormatting);
        }

        private IEnumerable<EventSourceElement> ReadEventSources(XElement element)
        {
            List<EventSourceElement> sources = new List<EventSourceElement>();
            foreach (var eventSource in element.Elements(this.sourcesName).Elements(this.eventSourceName))
            {
                sources.Add(EventSourceElement.Read(eventSource));
            }

            if (sources.Count == 0)
            {
                throw new ConfigurationException(string.Format(CultureInfo.CurrentCulture, Properties.Resources.NoEventSourcesError, element.Name.LocalName));
            }

            return sources;
        }

        private class SchemaConfigurationXmlResolver : XmlUrlResolver
        {
            private readonly byte[] emptySchema;

            public SchemaConfigurationXmlResolver()
            {
                this.emptySchema = Encoding.UTF8.GetBytes("<schema xmlns=\"" + XmlSchema.Namespace + "\"/>");
            }

            [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1062:Validate arguments of public methods", MessageId = "0", Justification = "Validated with Guard class")]
            public override object GetEntity(Uri absoluteUri, string role, Type objectToReturn)
            {
                Guard.ArgumentNotNull(absoluteUri, "absoluteUri");

                // If we are loading the built-in schema from schemaLocation, 
                // bail out with an empty schema to avoid duplicate validation errors since we already loaded the built-in schema
                if (absoluteUri.IsFile && Path.GetFileName(absoluteUri.AbsolutePath).Equals(ConfigurationReader.SchemaFileName, StringComparison.OrdinalIgnoreCase))
                {
                    return new MemoryStream(this.emptySchema);
                }

                return base.GetEntity(absoluteUri, role, objectToReturn);
            }
        }
    }
}
