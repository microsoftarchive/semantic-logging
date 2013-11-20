// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw.Utility;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Utility;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw.Configuration
{
    /// <summary>
    /// Loads all the configuration extension elements.
    /// </summary>
    internal sealed class ExtensionsLoader
    {
        private static readonly Dictionary<string, ExtensionsLoader> Instances = new Dictionary<string, ExtensionsLoader>();       
        private IEnumerable<string> schemaFileNames;
        private IEnumerable<Lazy<ISinkElement>> sinkElements;
        private IEnumerable<Lazy<IFormatterElement>> formatterElements;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExtensionsLoader" /> class.
        /// </summary>
        internal ExtensionsLoader()
            : this(AppDomain.CurrentDomain.BaseDirectory)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ExtensionsLoader" /> class.
        /// </summary>
        /// <param name="probingPath">The probing path.</param>
        internal ExtensionsLoader(string probingPath)
        {
            Guard.ArgumentNotNullOrEmpty(probingPath, "probingPath");
            this.Initialize(probingPath);
        }

        /// <summary>
        /// Gets the event listener elements that derives from base class <see cref="SinkElement"/>.
        /// </summary>
        internal IEnumerable<Lazy<ISinkElement>> SinkElements
        {
            get { return this.sinkElements; }
        }

        /// <summary>
        /// Gets the event text formatter elements that derives from type <see cref="FormatterElement"/>.
        /// </summary>
        internal IEnumerable<Lazy<IFormatterElement>> FormatterElements
        {
            get { return this.formatterElements; }
        }

        /// <summary>
        /// Gets the schema file names.
        /// </summary>
        internal IEnumerable<string> SchemaFileNames
        {
            get { return this.schemaFileNames; }
        }

        internal static ExtensionsLoader GetOrCreateInstance(string probingPath)
        {
            ExtensionsLoader loader;
            if (!Instances.TryGetValue(probingPath, out loader))
            {
                loader = new ExtensionsLoader(probingPath);
                Instances.Add(probingPath, loader);
            }

            return loader;
        }

        private static void ExtractTypes(Assembly assembly, ISet<Lazy<ISinkElement>> sinks, ISet<Lazy<IFormatterElement>> formatters)
        {
            if (assembly.IsFrameworkAssembly())
            {
                return;
            }

            foreach (Type type in assembly.GetTypes().Where(t => !t.IsAbstract))
            {
                if (typeof(ISinkElement).IsAssignableFrom(type))
                {
                    sinks.Add(new Lazy<ISinkElement>(() => (ISinkElement)Activator.CreateInstance(type)));
                }
                else if (typeof(IFormatterElement).IsAssignableFrom(type))
                {
                    formatters.Add(new Lazy<IFormatterElement>(() => (IFormatterElement)Activator.CreateInstance(type)));
                }
            }
        }

        private void Initialize(string probingPath)
        {
            var sinks = new HashSet<Lazy<ISinkElement>>();
            var formatters = new HashSet<Lazy<IFormatterElement>>();
            var currentAssemblies = AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic);

            // Load custom extensions in current domain
            foreach (Assembly assembly in currentAssemblies)
            {
                ExtractTypes(assembly, sinks, formatters);
            }

            // inspect all dlls which are not already loaded in the current domain.
            var filesToInspect = Directory.EnumerateFiles(probingPath, "*.dll", SearchOption.TopDirectoryOnly).
                                           Except(currentAssemblies.Select(a => a.Location), StringComparer.OrdinalIgnoreCase);

            // This will load external dll extensions in a separate domain so they can be unloaded
            var inspector = ExtensionsInspector.CreateInstance(filesToInspect);

            // Load extension in external domains
            foreach (var assembly in inspector.ExtensionFiles.Select(f => Assembly.LoadFrom(f)))
            {
                ExtractTypes(assembly, sinks, formatters);
            }

            this.sinkElements = sinks;
            this.formatterElements = formatters;
            this.schemaFileNames = Directory.EnumerateFiles(probingPath, "*.xsd", SearchOption.AllDirectories);
        }
    }
}
