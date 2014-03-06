// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw.Configuration;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.TestObjects;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.Etw
{
    [TestClass]
    public class given_sinkSettings
    {
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void when_creating_instance_with_null_name()
        {
            new SinkSettings(null, new InMemoryEventListener(), Enumerable.Empty<EventSourceSettings>());
        }

        [TestMethod]
        [ExpectedException(typeof(ConfigurationException))]
        public void when_creating_instance_with_null_sink()
        {
            new SinkSettings("name", sink: null, eventSources: Enumerable.Empty<EventSourceSettings>());
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void when_creating_instance_with_null_sources()
        {
            new SinkSettings("name", new InMemoryEventListener(), null);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void when_creating_instance_with_max_name_length()
        {
            new SinkSettings(new string('a', 201), new InMemoryEventListener(), Enumerable.Empty<EventSourceSettings>());
        }

        [TestMethod]
        [ExpectedException(typeof(ConfigurationException))]
        public void when_creating_instance_with_empty_sources()
        {
            new SinkSettings("test", new InMemoryEventListener(), Enumerable.Empty<EventSourceSettings>());
        }

        [TestMethod]
        [ExpectedException(typeof(ConfigurationException))]
        public void when_creating_instance_with_duplicate_sources_by_name()
        {
            var sources = new List<EventSourceSettings>() { new EventSourceSettings("test"), new EventSourceSettings("test") };
            new SinkSettings("test", new InMemoryEventListener(), sources);
        }

        [TestMethod]
        [ExpectedException(typeof(ConfigurationException))]
        public void when_creating_instance_with_duplicate_sources_by_id()
        {
            var sources = new List<EventSourceSettings>() { new EventSourceSettings(eventSourceId: MyCompanyEventSource.Log.Guid), new EventSourceSettings(eventSourceId: MyCompanyEventSource.Log.Guid) };
            new SinkSettings("test", new InMemoryEventListener(), sources);
        }

        [TestMethod]
        public void when_creating_instance_with_default_values()
        {
            var sources = new List<EventSourceSettings>() { new EventSourceSettings(MyCompanyEventSource.Log.Name) };
            var sink = new InMemoryEventListener();
            var sut = new SinkSettings("test", sink, sources);

            Assert.AreEqual("test", sut.Name);
            Assert.AreEqual(sink, sut.Sink);
            Assert.AreEqual(1, sut.EventSources.Count());
        }
    }
}
