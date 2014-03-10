// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.Etw
{
    using System;
    using System.Xml.Linq;

    using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw.Configuration;
    using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.TestSupport;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    public abstract class given_elasticSearchSinkElement : ContextBase
    {
        protected ISinkElement sut;
        private XElement element;

        protected override void Given()
        {
            this.element = new XElement(XName.Get("elasticSearchSink", Constants.Namespace),
                                        new XAttribute("instanceName", "instanceName"),
                                        new XAttribute("connectionString", "http://localhost:9200"));

            this.sut = new ElasticSearchSinkElement();
        }

        [TestClass]
        public class when_query_for_canCreateSink : Etw.given_elasticSearchSinkElement
        {
            [TestMethod]
            public void then_instance_can_be_created()
            {
                Assert.IsTrue(this.sut.CanCreateSink(this.element));
            }
        }

        [TestClass]
        public class when_createSink_with_required_parameters : Etw.given_elasticSearchSinkElement
        {
            private IObserver<EventEntry> observer;

            protected override void When()
            {
                this.observer = this.sut.CreateSink(this.element);
            }

            [TestMethod]
            public void then_sink_is_created()
            {
                Assert.IsNotNull(this.observer);
            }
        }
    }
}
