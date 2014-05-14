// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using Microsoft.Diagnostics.Tracing.Session;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw.Configuration;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.TestObjects;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.Etw
{
    public abstract class given_traceEventServiceWorker : ContextBase
    {
        protected SinkSettings sinkSettings;
        protected TraceEventServiceSettings traceEventServiceSettings;
        internal TraceEventServiceWorker Sut;

        protected override void Given()
        {
            this.sinkSettings = new SinkSettings("test", new InMemoryEventListener(), new EventSourceSettings[] { new EventSourceSettings("Test") });
            this.traceEventServiceSettings = new TraceEventServiceSettings();
        }

        protected override void OnCleanup()
        {
            if (this.Sut != null)
            {
                this.Sut.Dispose();
            }
        }

        [TestClass]
        public class when_creating_instance_with_null_arguments : given_traceEventServiceWorker
        {
            [TestMethod]
            [ExpectedException(typeof(ArgumentNullException))]
            public void then_exception_is_thrown_on_null_sinkSettings()
            {
                new TraceEventServiceWorker(null, this.traceEventServiceSettings);
            }

            [TestMethod]
            [ExpectedException(typeof(ArgumentNullException))]
            public void then_exception_is_thrown_on_null_traceEventServiceSettings()
            {
                new TraceEventServiceWorker(this.sinkSettings, null);
            }
        }

        [TestClass]
        public class when_creating_instance_with_valid_settings : given_traceEventServiceWorker
        {
            protected override void When()
            {
                try
                {
                    this.Sut = new TraceEventServiceWorker(this.sinkSettings, this.traceEventServiceSettings);
                }
                catch (UnauthorizedAccessException ex)
                {
                    Assert.Inconclusive("In order to run the tests, please run Visual Studio as Administrator.\r\n{0}", ex.ToString());
                }
            }

            [TestMethod]
            public void then_session_is_started()
            {
                bool sessionCreated = TraceEventSession.GetActiveSessionNames().
                    Any(s => s.StartsWith(this.traceEventServiceSettings.SessionNamePrefix, StringComparison.OrdinalIgnoreCase));

                Assert.IsTrue(sessionCreated);
            }
        }

        [TestClass]
        public class when_updating_session : given_traceEventServiceWorker
        {
            protected override void When()
            {
                try
                {
                    this.Sut = new TraceEventServiceWorker(this.sinkSettings, this.traceEventServiceSettings);
                }
                catch (UnauthorizedAccessException ex)
                {
                    Assert.Inconclusive("In order to run the tests, please run Visual Studio as Administrator.\r\n{0}", ex.ToString());
                }
            }

            [TestMethod]
            [ExpectedException(typeof(ArgumentNullException))]
            public void then_exception_is_thrown_with_null_eventSources()
            {
                this.Sut.UpdateSession(null);
            }

            [TestMethod]
            public void then_session_is_updated_with_new_eventSources()
            {
                var currentEventSource = this.sinkSettings.EventSources.First();
                var newEventSource = new EventSourceSettings(currentEventSource.Name, level: currentEventSource.Level, matchAnyKeyword: EventKeywords.AuditSuccess);

                this.Sut.UpdateSession(new List<EventSourceSettings>() { newEventSource });

                Assert.AreEqual(newEventSource.MatchAnyKeyword, currentEventSource.MatchAnyKeyword);
            }
        }
    }
}
