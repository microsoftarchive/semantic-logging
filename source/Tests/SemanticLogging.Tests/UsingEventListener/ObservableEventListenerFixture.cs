// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Schema;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.TestObjects;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Threading;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.EventListeners
{
    [TestClass]
    public class ObservableEventListenerFixture : ArrangeActAssert
    {
        private static readonly TestEventSource Logger = TestEventSource.Log;
        private ObservableEventListener listener;

        protected override void Arrange()
        {
            this.listener = new ObservableEventListener();
        }

        protected override void Teardown()
        {
            this.listener.Dispose();
        }

        [TestMethod]
        public void when_subscribing_then_receives_events()
        {
            var sink = new MockSink();
            listener.Subscribe(sink);
            listener.EnableEvents(Logger, EventLevel.LogAlways);

            Logger.Informational("Test");

            Assert.AreEqual(1, sink.OnNextCalls.Count());
            Assert.AreEqual(TestEventSource.InformationalEventId, sink.OnNextCalls.ElementAt(0).EventId);
            Assert.AreEqual(EventLevel.Informational, sink.OnNextCalls.ElementAt(0).Schema.Level);
        }

        [TestMethod]
        public void when_subscribing_then_receives_parsed_schema()
        {
            var sink = new MockSink();
            listener.Subscribe(sink);
            listener.EnableEvents(Logger, EventLevel.LogAlways);

            Logger.Informational("Test");

            var expectedSchema = EventSourceSchemaCache.Instance.GetSchema(TestEventSource.InformationalEventId, Logger);
            Assert.AreEqual(expectedSchema, sink.OnNextCalls.ElementAt(0).Schema);
        }

        [TestMethod]
        public void when_subscribing_then_receives_raw_payload()
        {
            var sink = new MockSink();
            listener.Subscribe(sink);
            listener.EnableEvents(Logger, EventLevel.LogAlways);

            Logger.Informational("Test");

            Assert.AreEqual(1, sink.OnNextCalls.ElementAt(0).Payload.Count);
            Assert.AreEqual("Test", sink.OnNextCalls.ElementAt(0).Payload[0]);
        }

        [TestMethod]
        public void when_subscribing_then_receives_activity_id()
        {
            var sink = new MockSink();
            listener.Subscribe(sink);
            listener.EnableEvents(Logger, EventLevel.LogAlways);

            var activityId = Guid.NewGuid();
            Guid previousActivityId;
            EventSource.SetCurrentThreadActivityId(activityId, out previousActivityId);

            try
            {
                Logger.Informational("Test");
            }
            finally
            {
                EventSource.SetCurrentThreadActivityId(previousActivityId);
            }

            Assert.AreEqual(activityId, sink.OnNextCalls.ElementAt(0).ActivityId);
            Assert.AreEqual(Guid.Empty, sink.OnNextCalls.ElementAt(0).RelatedActivityId);
        }

        [TestMethod]
        public void when_subscribing_then_receives_related_activity_id()
        {
            var sink = new MockSink();
            listener.Subscribe(sink);
            listener.EnableEvents(Logger, EventLevel.LogAlways);

            var activityId = Guid.NewGuid();
            var relatedActivityId = Guid.NewGuid();
            Guid previousActivityId;
            EventSource.SetCurrentThreadActivityId(activityId, out previousActivityId);

            try
            {
                Logger.EventWithPayloadAndMessageAndRelatedActivityId(relatedActivityId, "Test", 100);
            }
            finally
            {
                EventSource.SetCurrentThreadActivityId(previousActivityId);
            }

            Assert.AreEqual(activityId, sink.OnNextCalls.ElementAt(0).ActivityId);
            Assert.AreEqual(relatedActivityId, sink.OnNextCalls.ElementAt(0).RelatedActivityId);
        }

        [TestMethod]
        public void can_subscribe_multiple_sinks()
        {
            var sink1 = new MockSink();
            var sink2 = new MockSink();
            listener.Subscribe(sink1);
            listener.Subscribe(sink2);
            listener.EnableEvents(Logger, EventLevel.LogAlways);

            Logger.Informational("Test");

            Assert.AreEqual(1, sink1.OnNextCalls.Count());
            Assert.AreEqual(1, sink2.OnNextCalls.Count());
        }

        [TestMethod]
        public void when_disposing_listener_then_calls_OnCompleted_on_sinks()
        {
            var sink1 = new MockSink();
            var sink2 = new MockSink();
            listener.Subscribe(sink1);
            listener.Subscribe(sink2);

            listener.EnableEvents(Logger, EventLevel.LogAlways);

            Assert.AreEqual(0, sink1.OnCompletedCalls);
            Assert.AreEqual(0, sink2.OnCompletedCalls);

            listener.Dispose();

            Assert.AreEqual(1, sink1.OnCompletedCalls);
            Assert.AreEqual(1, sink2.OnCompletedCalls);
        }

        //TODO: Validate: is this the expected behavior?
        [TestMethod]
        public void when_disposing_listener_then_does_not_dispose_sinks()
        {
            var sink1 = new MockSink();
            var sink2 = new MockSink();
            listener.Subscribe(sink1);
            listener.Subscribe(sink2);

            listener.EnableEvents(Logger, EventLevel.LogAlways);

            Assert.IsFalse(sink1.DisposeCalled);
            Assert.IsFalse(sink2.DisposeCalled);

            listener.Dispose();

            Assert.IsFalse(sink1.DisposeCalled);
            Assert.IsFalse(sink2.DisposeCalled);
        }

        [TestMethod]
        public void when_unsubscribing_then_stops_receiving_events()
        {
            var sink = new MockSink();
            var subscription = listener.Subscribe(sink);
            listener.EnableEvents(Logger, EventLevel.LogAlways);

            subscription.Dispose();

            Logger.Informational("Test");

            Assert.AreEqual(0, sink.OnNextCalls.Count());
        }

        //TODO: Validate: is this the expected behavior?
        [TestMethod]
        public void when_unsubscribing_then_does_not_send_OnCompleted_to_sink()
        {
            var sink = new MockSink();
            var subscription = listener.Subscribe(sink);
            listener.EnableEvents(Logger, EventLevel.LogAlways);

            subscription.Dispose();

            Assert.AreEqual(0, sink.OnCompletedCalls);
        }

        [TestMethod]
        public void when_disposing_listener_then_stops_publishing_events()
        {
            var sink = new MockSink();
            listener.Subscribe(sink);
            listener.EnableEvents(Logger, EventLevel.LogAlways);
            listener.Dispose();

            Logger.Informational("Test");

            Assert.AreEqual(0, sink.OnNextCalls.Count());
        }

        [TestMethod]
        public void when_disposing_listener_then_sends_OnCompleted()
        {
            var sink = new MockSink();
            listener.Subscribe(sink);
            listener.EnableEvents(Logger, EventLevel.LogAlways);
            Assert.AreEqual(0, sink.OnCompletedCalls);

            listener.Dispose();

            Assert.AreEqual(1, sink.OnCompletedCalls);
        }

        // TODO: tests on ObservableProjection

        private sealed class MockSink : IObserver<EventEntry>, IDisposable
        {
            private int onCompletedCalls;
            private int onErrorCalls;
            private ConcurrentBag<EventEntry> onNextCalls = new ConcurrentBag<EventEntry>();

            public int OnCompletedCalls { get { return this.onCompletedCalls; } }
            public int OnErrorCalls { get { return this.onErrorCalls; } }
            public IEnumerable<EventEntry> OnNextCalls { get { return this.onNextCalls; } }
            public bool DisposeCalled { get; private set; }

            void IObserver<EventEntry>.OnCompleted()
            {
                Interlocked.Increment(ref this.onCompletedCalls);
            }

            void IObserver<EventEntry>.OnError(Exception error)
            {
                Interlocked.Increment(ref this.onErrorCalls);
            }

            void IObserver<EventEntry>.OnNext(EventEntry value)
            {
                this.onNextCalls.Add(value);
            }

            public void Dispose()
            {
                this.DisposeCalled = true;
            }
        }
    }
}
