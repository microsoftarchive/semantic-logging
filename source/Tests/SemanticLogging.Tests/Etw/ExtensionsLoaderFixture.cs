// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.Etw
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Configuration;
    using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw.Configuration;
    using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.TestSupport;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    public abstract class given_extensionsloader : ContextBase
    {
        internal ExtensionsLoader Sut;

        protected override void Given()
        {
            this.Sut = new ExtensionsLoader();
        }

        protected static IEnumerable<Type> FilterTypes(Type type)
        {
            return AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes().Where(t => !t.IsAbstract && type.IsAssignableFrom(t)));
        }

        protected static string FilterSchemas(IEnumerable<string> schemas, string schemaFile)
        {
            return schemas.SingleOrDefault(s => Path.GetFileName(s).Equals(schemaFile, StringComparison.OrdinalIgnoreCase));
        }

        [TestClass]
        public class when_creating_instance_and_query_for_sinkExtensions : given_extensionsloader
        {
            private IEnumerable<Lazy<ISinkElement>> sinkElements;

            protected override void When()
            {
                sinkElements = this.Sut.SinkElements;
            }

            [TestMethod]
            public void then_all_sinkExtensions_in_topDirectoryOnly_should_be_loaded()
            {
                var actualTypes = FilterTypes(typeof(ISinkElement));
                Assert.IsTrue(sinkElements.All(t => actualTypes.Contains(t.Value.GetType())));
            }
        }

        [TestClass]
        public class when_creating_instance_and_query_for_eventTextFormattersExtensions : given_extensionsloader
        {
            private IEnumerable<Lazy<IFormatterElement>> formatterElements;

            protected override void When()
            {
                formatterElements = this.Sut.FormatterElements;
            }

            [TestMethod]
            public void then_all_eventTextFormattersExtensions_in_topDirectoryOnly_should_be_loaded()
            {
                var actualTypes = FilterTypes(typeof(IFormatterElement));

                Assert.IsTrue(formatterElements.All(t => actualTypes.Contains(t.Value.GetType())));
            }
        }
        
        [TestClass]
        public class when_creating_instance_and_query_for_schemas : given_extensionsloader
        {
            private IEnumerable<string> schemas;

            protected override void When()
            {
                schemas = this.Sut.SchemaFileNames;
            }

            [TestMethod]
            public void then_all_schemas_in_allDirectories_should_be_loaded()
            {
                Assert.IsNotNull(FilterSchemas(schemas, "MySinkElement.xsd"));
                Assert.IsNotNull(FilterSchemas(schemas, "Event.xsd"));
            }
        }

        [TestClass]
        public class when_loading_custom_sinks_from_external_assemblies : given_extensionsloader
        {
            private string generatedAssemblyPath;  
            private const string Source =
@"using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Configuration;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Formatters;
using System;
using System.Xml.Linq;

namespace Test
{
    public class TestSinkElement : ISinkElement
    {
        public bool CanCreateSink(XElement element)
        {
            throw new NotImplementedException();
        }

        public IObserver<EventEntry> CreateSink(XElement element)
        {
            throw new NotImplementedException();
        }
    }
}
";
            protected override void Given()
            {
            }

            protected override void When()
            {
                var results = AssemblyBuilder.CompileFromSource(Source, generateInMemory: false);
                Assert.AreEqual(0, results.Errors.Count, AssemblyBuilder.DumpOnErrors(results));
                this.generatedAssemblyPath = results.PathToAssembly;
            }

            [TestMethod]
            public void then_all_custom_implementations_should_be_loaded()
            {
                using (var domain = new DisposableDomain())
                {
                    domain.DoCallBack(() =>
                    {
                        var sut = new ExtensionsLoader();
                        Assert.IsTrue(sut.SinkElements.Any(s => s.Value.GetType().FullName == "Test.TestSinkElement"));
                    });
                }
            }

            protected override void OnCleanup()
            {
                if (File.Exists(this.generatedAssemblyPath))
                {
                    File.Delete(this.generatedAssemblyPath);
                }

                base.OnCleanup();
            }
        }

        [TestClass]
        public class when_loading_custom_formatter_from_external_assemblies : given_extensionsloader
        {
            private string generatedAssemblyPath;
            private const string Source =
@"using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Configuration;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Formatters;
using System;
using System.Xml.Linq;

namespace Test
{
    public class TestFormatterElement : IFormatterElement
    {
        public bool CanCreateFormatter(XElement element)
        {
            throw new NotImplementedException();
        }

        public IEventTextFormatter CreateFormatter(XElement element)
        {
            throw new NotImplementedException();
        }
    }
}
";
            protected override void Given()
            {
            }

            protected override void When()
            {
                var results = AssemblyBuilder.CompileFromSource(Source, generateInMemory: false);
                Assert.AreEqual(0, results.Errors.Count, AssemblyBuilder.DumpOnErrors(results));
                this.generatedAssemblyPath = results.PathToAssembly;
            }

            [TestMethod]
            public void then_all_custom_implementations_should_be_loaded()
            {
                using (var domain = new DisposableDomain())
                {
                    domain.DoCallBack(() =>
                    {
                        var sut = new ExtensionsLoader();
                        Assert.IsTrue(sut.FormatterElements.Any(s => s.Value.GetType().FullName == "Test.TestFormatterElement"));
                    });
                }
            }

            protected override void OnCleanup()
            {
                if (File.Exists(this.generatedAssemblyPath))
                {
                    File.Delete(this.generatedAssemblyPath);
                }

                base.OnCleanup();
            }
        }
    }
}
