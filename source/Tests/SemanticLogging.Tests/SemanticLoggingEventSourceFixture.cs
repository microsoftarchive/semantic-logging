// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System.Diagnostics.Tracing;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.TestObjects;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Utility;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests
{
    [TestClass]
    public class SemanticLoggingEventSourceFixture
    {
        [TestMethod]
        public void ShouldValidateEventSource()
        {
            EventSourceAnalyzer.InspectAll(SemanticLoggingEventSource.Log);
        }

        [TestMethod]
        public void ShouldWriteWithNoFiltering()
        {
            using (var listener = new InMemoryEventListener())
            {
                listener.EnableEvents(SemanticLoggingEventSource.Log, EventLevel.LogAlways, Keywords.All);

                SemanticLoggingEventSource.Log.DatabaseSinkPublishEventsFailed("test");

                listener.DisableEvents(SemanticLoggingEventSource.Log);

                StringAssert.Contains(listener.ToString(), "EventId : 101");
                StringAssert.Contains(listener.ToString(), "Level : Error");
                StringAssert.Contains(listener.ToString(), "Payload : [message : test]");
            }
        }

        [TestMethod]
        public void ShouldWriteByErrorLevel()
        {
            using (var listener = new InMemoryEventListener())
            {
                listener.EnableEvents(SemanticLoggingEventSource.Log, EventLevel.Error, Keywords.All);

                SemanticLoggingEventSource.Log.ConsoleSinkWriteFailed("test");

                listener.DisableEvents(SemanticLoggingEventSource.Log);

                StringAssert.Contains(listener.ToString(), "EventId : 200");
                StringAssert.Contains(listener.ToString(), "Level : Critical");
                StringAssert.Contains(listener.ToString(), "Payload : [message : test]");
            }
        }

        [TestMethod]
        public void ShouldWriteByEventKeywords()
        {
            using (var listener = new InMemoryEventListener())
            {
                listener.EnableEvents(SemanticLoggingEventSource.Log, EventLevel.LogAlways, SemanticLoggingEventSource.Keywords.Sink);

                SemanticLoggingEventSource.Log.ConsoleSinkWriteFailed("test");

                listener.DisableEvents(SemanticLoggingEventSource.Log);

                StringAssert.Contains(listener.ToString(), "EventId : 200");
                StringAssert.Contains(listener.ToString(), "Level : Critical");
                StringAssert.Contains(listener.ToString(), "Payload : [message : test]");
            }
        }

        [TestMethod]
        public void ShouldFilterByEventKeywords()
        {
            using (var listener = new InMemoryEventListener())
            {
                listener.EnableEvents(SemanticLoggingEventSource.Log, EventLevel.LogAlways, SemanticLoggingEventSource.Keywords.Formatting);

                SemanticLoggingEventSource.Log.ConsoleSinkWriteFailed("test");

                listener.DisableEvents(SemanticLoggingEventSource.Log);

                Assert.AreEqual(string.Empty, listener.ToString());
            }
        }
    }
}
