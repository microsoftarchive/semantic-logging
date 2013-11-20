// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Xml.Linq;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw.Configuration;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Sinks;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.Etw
{
    public abstract class given_sqlDatabaseSinkElement : ContextBase
    {
        protected ISinkElement sut;
        private XElement element;

        protected override void Given()
        {
            this.element = new XElement(XName.Get("sqlDatabaseSink", Constants.Namespace),
                                        new XAttribute("instanceName", "instanceName"),
                                        new XAttribute("connectionString", "Data Source=(localdb)\v11.0;Initial Catalog=SemanticLoggingTests;Integrated Security=True"));

            this.sut = new SqlDatabaseSinkElement();
        }

        [TestClass]
        public class when_query_for_canCreateSink : given_sqlDatabaseSinkElement
        {
            [TestMethod]
            public void then_instance_can_be_created()
            {
                Assert.IsTrue(this.sut.CanCreateSink(this.element));
            }
        }

        [TestClass]
        public class when_createSink_with_required_parameters : given_sqlDatabaseSinkElement
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
