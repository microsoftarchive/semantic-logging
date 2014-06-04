// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Linq;
using Microsoft.Diagnostics.Tracing.Session;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Configuration;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw.Configuration;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.TestObjects;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.Etw
{
    [TestClass]
    public class given_traceEventService_instance
    {
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void when_creating_instance_with_null_configuration()
        {
            new TraceEventService(null);
        }
    }

    public abstract class given_traceEventService : ContextBase
    {
        protected readonly TimeSpan AsyncProcessTimeout = TimeSpan.FromSeconds(15);
        protected TraceEventService sut;
        protected List<EventSourceSettings> eventSources;
        protected List<SinkSettings> sinkSettings;
        protected EventSourceSettings sourceSettings;
        protected InMemoryEventListener inMemoryListener;
        protected MockFormatter formatter;
        protected TraceEventServiceConfiguration configuration;
        protected TraceEventServiceSettings serviceSettings;

        protected override void Given()
        {
            TraceEventManifestsCache.Clear();

            this.formatter = new MockFormatter();
            this.inMemoryListener = new InMemoryEventListener(this.formatter);
            var sink = new Lazy<IObserver<EventEntry>>(() => this.inMemoryListener);
            this.sourceSettings = this.sourceSettings ?? new EventSourceSettings(EventSource.GetName(typeof(MyCompanyEventSource)));
            this.eventSources = new List<EventSourceSettings> { this.sourceSettings };
            this.sinkSettings = new List<SinkSettings> { new SinkSettings("test", sink, this.eventSources) };
            this.configuration = new TraceEventServiceConfiguration(sinkSettings, this.serviceSettings);

            try
            {
                this.sut = new TraceEventService(configuration);
            }
            catch (UnauthorizedAccessException uae)
            {
                Assert.Inconclusive(uae.Message);
            }

            // Clean up any previous unclosed session to avoid collisions
            this.RemoveAnyExistingSession();
        }

        protected override void OnCleanup()
        {
            if (this.sut != null)
            {
                this.sut.Dispose();
            }
        }

        protected bool IsCreatedSessionAlive()
        {
            return TraceEventSession.GetActiveSessionNames().Any(s =>
                s.StartsWith(this.configuration.Settings.SessionNamePrefix, StringComparison.OrdinalIgnoreCase));
        }

        protected void RemoveAnyExistingSession(string sessionName = Constants.DefaultSessionNamePrefix)
        {
            TraceEventSession.GetActiveSessionNames().
                Where(s => s.StartsWith(sessionName, StringComparison.OrdinalIgnoreCase)).ToList().
                ForEach(n => new TraceEventSession(n) { StopOnDispose = true }.Dispose());
        }

        [TestClass]
        public class when_starting_session : given_traceEventService
        {
            protected override void When()
            {
                this.sut.Start();
            }

            [TestMethod]
            public void then_session_is_created_and_started()
            {
                Assert.AreEqual(ServiceStatus.Started, this.sut.Status);
                Assert.IsTrue(IsCreatedSessionAlive());
            }
        }

        [TestClass]
        public class when_stopping_session : given_traceEventService
        {
            protected override void Given()
            {
                base.Given();
                this.sut.Start();
            }

            protected override void When()
            {
                this.sut.Stop();
            }

            [TestMethod]
            public void then_session_is_stopped_and_deleted()
            {
                Assert.AreEqual(ServiceStatus.Stopped, this.sut.Status);
                Assert.IsFalse(IsCreatedSessionAlive());
            }
        }

        [TestClass]
        public class when_disposing_session : given_traceEventService
        {
            protected override void Given()
            {
                base.Given();
                this.sut.Start();
            }

            protected override void When()
            {
                this.sut.Dispose();
            }

            [TestMethod]
            [ExpectedException(typeof(ObjectDisposedException))]
            public void then_session_is_disposed_and_error_is_thrown_on_start()
            {
                Assert.AreEqual(ServiceStatus.Disposed, this.sut.Status);
                Assert.IsFalse(IsCreatedSessionAlive());
                this.sut.Start();
            }
        }

        [TestClass]
        public class when_starting_second_service_instance_with_same_config_other_sessionPrefix : given_traceEventService
        {
            private TraceEventService collector2;

            protected override void Given()
            {
                base.Given();
                this.sut.Start();
            }

            protected override void When()
            {
                this.configuration.Settings.SessionNamePrefix = "SecondInstance";
                collector2 = new TraceEventService(this.configuration);
            }

            [TestMethod]
            public void then_session_is_created()
            {
                collector2.Start();
            }

            protected override void OnCleanup()
            {
                collector2.Dispose();
                base.OnCleanup();
            }
        }

        [TestClass]
        public class when_logging_an_event : given_traceEventService
        {
            private int processId;
            private int threadId;

            protected override void Given()
            {
                base.Given();
                this.sut.Start();
            }

            protected override void When()
            {
                this.processId = System.Diagnostics.Process.GetCurrentProcess().Id;
                this.threadId = Utility.NativeMethods.GetCurrentThreadId();

                MyCompanyEventSource.Log.PageStart(10, "test");
            }

            [TestMethod]
            public void then_event_is_collected_and_processed()
            {
                // Wait for event to be processed
                inMemoryListener.WaitOnAsyncEvents.WaitOne(this.AsyncProcessTimeout);

                Assert.AreEqual(1, inMemoryListener.EventWrittenCount);
                var entry = formatter.WriteEventCalls.FirstOrDefault();
                Assert.IsNotNull(entry);

                Assert.AreEqual(MyCompanyEventSource.Log.Name, entry.Schema.ProviderName);
                Assert.AreEqual(sourceSettings.EventSourceId, entry.ProviderId);
                Assert.AreEqual("loading page test activityID=10", entry.FormattedMessage);
                Assert.AreEqual(EventOpcode.Start, entry.Schema.Opcode);
                Assert.AreEqual(3, entry.EventId);
                Assert.AreEqual(processId, entry.ProcessId);
                Assert.AreEqual(threadId, entry.ThreadId);
                Assert.AreEqual(Guid.Empty, entry.ActivityId);
                Assert.AreEqual(Guid.Empty, entry.RelatedActivityId);
            }
        }

        [TestClass]
        public class when_logging_an_event_with_activity_id : given_traceEventService
        {
            private Guid activityId;

            protected override void Given()
            {
                base.Given();
                this.sut.Start();

                this.activityId = Guid.NewGuid();
            }

            protected override void When()
            {
                Guid previousActivityId = Guid.Empty;

                try
                {
                    EventSource.SetCurrentThreadActivityId(this.activityId, out previousActivityId);

                    MyCompanyEventSource.Log.PageStart(10, "test");
                }
                finally
                {
                    EventSource.SetCurrentThreadActivityId(previousActivityId);
                }
            }

            [TestMethod]
            public void then_event_is_collected_and_processed()
            {
                // Wait for event to be processed
                inMemoryListener.WaitOnAsyncEvents.WaitOne(this.AsyncProcessTimeout);

                Assert.AreEqual(1, inMemoryListener.EventWrittenCount);
                var entry = formatter.WriteEventCalls.FirstOrDefault();
                Assert.IsNotNull(entry);

                Assert.AreEqual(MyCompanyEventSource.Log.Name, entry.Schema.ProviderName);
                Assert.AreEqual(sourceSettings.EventSourceId, entry.ProviderId);
                Assert.AreEqual("loading page test activityID=10", entry.FormattedMessage);
                Assert.AreEqual(EventOpcode.Start, entry.Schema.Opcode);
                Assert.AreEqual(3, entry.EventId);
                Assert.AreEqual(this.activityId, entry.ActivityId);
                Assert.AreEqual(Guid.Empty, entry.RelatedActivityId);
            }
        }

        [TestClass]
        public class when_logging_an_event_with_activity_id_and_related_activity_id : given_traceEventService
        {
            private Guid activityId;
            private Guid relatedActivityId;

            protected override void Given()
            {
                base.Given();
                this.sut.Start();

                this.activityId = Guid.NewGuid();
                this.relatedActivityId = Guid.NewGuid();
            }

            protected override void When()
            {
                Guid previousActivityId = Guid.Empty;

                try
                {
                    EventSource.SetCurrentThreadActivityId(this.activityId, out previousActivityId);

                    MyCompanyEventSource.Log.WithRelatedActivityId(this.relatedActivityId);
                }
                finally
                {
                    EventSource.SetCurrentThreadActivityId(previousActivityId);
                }
            }

            [TestMethod]
            public void then_event_is_collected_and_processed()
            {
                // Wait for event to be processed
                inMemoryListener.WaitOnAsyncEvents.WaitOne(this.AsyncProcessTimeout);

                Assert.AreEqual(1, inMemoryListener.EventWrittenCount);
                var entry = formatter.WriteEventCalls.FirstOrDefault();
                Assert.IsNotNull(entry);

                Assert.AreEqual(MyCompanyEventSource.Log.Name, entry.Schema.ProviderName);
                Assert.AreEqual(sourceSettings.EventSourceId, entry.ProviderId);
                Assert.AreEqual(EventOpcode.Send, entry.Schema.Opcode);
                Assert.AreEqual(11, entry.EventId);
                Assert.AreEqual(this.activityId, entry.ActivityId);
                Assert.AreEqual(this.relatedActivityId, entry.RelatedActivityId);
            }
        }

        [TestClass]
        public class when_logging_an_event_by_level : given_traceEventService
        {
            protected override void Given()
            {
                base.Given();
                var sink = new Lazy<IObserver<EventEntry>>(() => inMemoryListener);
                sourceSettings = new EventSourceSettings(EventSource.GetName(typeof(MyCompanyEventSource)), level: EventLevel.Warning);
                eventSources = new List<EventSourceSettings> { sourceSettings };
                sinkSettings = new List<SinkSettings> { new SinkSettings("test", sink, eventSources) };
                configuration = new TraceEventServiceConfiguration(sinkSettings);
                this.sut = new TraceEventService(configuration);
                this.sut.Start();
            }

            protected override void When()
            {
                MyCompanyEventSource.Log.PageStart(10, "test");
                MyCompanyEventSource.Log.Failure("failure");
            }

            [TestMethod]
            public void then_event_is_logged_by_level_or_filtered_out()
            {
                // Wait for event to be processed
                inMemoryListener.WaitOnAsyncEvents.WaitOne(this.AsyncProcessTimeout);

                // Only Info event is logged
                Assert.AreEqual(1, formatter.WriteEventCalls.Count);
                var entry = formatter.WriteEventCalls.FirstOrDefault();
                Assert.IsNotNull(entry);

                Assert.AreEqual(MyCompanyEventSource.Log.Name, entry.Schema.ProviderName);
                Assert.AreEqual(sourceSettings.EventSourceId, entry.ProviderId);
                Assert.AreEqual("Application Failure: failure", entry.FormattedMessage);
                Assert.AreEqual(EventOpcode.Info, entry.Schema.Opcode);
                Assert.AreEqual(1, entry.EventId);
            }
        }

        [TestClass]
        public class when_logging_an_event_by_keywords : given_traceEventService
        {
            protected override void Given()
            {
                base.Given();
                var sink = new Lazy<IObserver<EventEntry>>(() => inMemoryListener);
                sourceSettings = new EventSourceSettings(EventSource.GetName(typeof(MyCompanyEventSource)), level: EventLevel.Warning, matchAnyKeyword: MyCompanyEventSource.Keywords.Diagnostic);
                eventSources = new List<EventSourceSettings> { sourceSettings };
                sinkSettings = new List<SinkSettings> { new SinkSettings("test", sink, eventSources) };
                configuration = new TraceEventServiceConfiguration(sinkSettings);
                this.sut = new TraceEventService(configuration);
                this.sut.Start();
            }

            protected override void When()
            {
                MyCompanyEventSource.Log.PageStart(10, "test");
                MyCompanyEventSource.Log.Failure("failure");
            }

            [TestMethod]
            public void then_event_is_logged_by_level_or_filtered_out()
            {
                // Wait for event to be processed
                Assert.IsTrue(inMemoryListener.WaitOnAsyncEvents.WaitOne(this.AsyncProcessTimeout));

                // Only Info event is logged
                Assert.AreEqual(1, formatter.WriteEventCalls.Count);
                var entry = formatter.WriteEventCalls.FirstOrDefault();
                Assert.IsNotNull(entry);

                Assert.AreEqual(MyCompanyEventSource.Log.Name, entry.Schema.ProviderName);
                Assert.AreEqual(sourceSettings.EventSourceId, entry.ProviderId);
                Assert.AreEqual("Application Failure: failure", entry.FormattedMessage);
                Assert.AreEqual(EventOpcode.Info, entry.Schema.Opcode);
                Assert.AreEqual(1, entry.EventId);
            }
        }

        [TestClass]
        public class when_logging_many_events_from_same_eventsource : given_traceEventService
        {
            protected override void Given()
            {
                base.Given();
                inMemoryListener.WaitSignalCondition = () => inMemoryListener.EventWrittenCount == 2;
                this.sut.Start();
            }

            protected override void When()
            {
                MyCompanyEventSource.Log.PageStart(10, "test");
                MyCompanyEventSource.Log.PageStart(11, "test2");
            }

            [TestMethod]
            public void then_all_events_are_collected_and_processed()
            {
                // Wait for event to be processed
                inMemoryListener.WaitOnAsyncEvents.WaitOne(this.AsyncProcessTimeout);

                Assert.AreEqual(2, formatter.WriteEventCalls.Count);
                var entry = formatter.WriteEventCalls.FirstOrDefault();
                Assert.IsNotNull(entry);
                Assert.AreEqual(MyCompanyEventSource.Log.Name, entry.Schema.ProviderName);
                Assert.AreEqual(MyCompanyEventSource.Log.Guid, entry.ProviderId);

                entry = formatter.WriteEventCalls.LastOrDefault();
                Assert.IsNotNull(entry);
                Assert.AreEqual(MyCompanyEventSource.Log.Name, entry.Schema.ProviderName);
                Assert.AreEqual(MyCompanyEventSource.Log.Guid, entry.ProviderId);
            }
        }

        [TestClass]
        public class when_logging_from_two_different_eventSources : given_traceEventService
        {
            protected override void Given()
            {
                base.Given();
                inMemoryListener.WaitSignalCondition = () => inMemoryListener.EventWrittenCount == 2;
                var sink = new Lazy<IObserver<EventEntry>>(() => inMemoryListener);
                var localSourceSettings = new EventSourceSettings(EventSource.GetName(typeof(TestEventSource)));
                this.eventSources.Add(localSourceSettings);
                sinkSettings = new List<SinkSettings> { new SinkSettings("test", sink, eventSources) };
                configuration = new TraceEventServiceConfiguration(sinkSettings);
                this.sut = new TraceEventService(configuration);
                this.sut.Start();
            }

            protected override void When()
            {
                MyCompanyEventSource.Log.PageStart(10, "test");
                TestEventSource.Log.NonDefaultOpcodeNonDefaultVersionEvent(1, 2, 3);
            }

            [TestMethod]
            public void then_event_all_events_are_collected_and_processed()
            {
                // Wait for event to be processed
                inMemoryListener.WaitOnAsyncEvents.WaitOne(this.AsyncProcessTimeout);

                Assert.AreEqual(2, formatter.WriteEventCalls.Count);
                var entry = formatter.WriteEventCalls.FirstOrDefault(e => e.ProviderId == MyCompanyEventSource.Log.Guid);
                Assert.IsNotNull(entry);
                Assert.AreEqual(MyCompanyEventSource.Log.Name, entry.Schema.ProviderName);
                Assert.AreEqual(MyCompanyEventSource.Log.Guid, entry.ProviderId);
                Assert.AreEqual("PageStart", entry.Schema.EventName);

                entry = formatter.WriteEventCalls.FirstOrDefault(e => e.ProviderId == TestEventSource.Log.Guid);
                Assert.IsNotNull(entry);
                Assert.AreEqual(TestEventSource.Log.Name, entry.Schema.ProviderName);
                Assert.AreEqual(TestEventSource.Log.Guid, entry.ProviderId);
                Assert.AreEqual("DBQueryReply", entry.Schema.EventName);
            }
        }

        [TestClass]
        public class when_listener_throws : given_traceEventService
        {
            private InMemoryEventListener slabListener;

            protected override void Given()
            {
                base.Given();
                inMemoryListener = new InMemoryEventListener(new MockFormatter { BeforeWriteEventAction = f => { throw new Exception("unhandled_exception_test"); } });
                var sink = new Lazy<IObserver<EventEntry>>(() => inMemoryListener);
                sourceSettings = new EventSourceSettings(EventSource.GetName(typeof(MyCompanyEventSource)));
                eventSources = new List<EventSourceSettings> { sourceSettings };
                sinkSettings = new List<SinkSettings> { new SinkSettings("test", sink, eventSources) };
                configuration = new TraceEventServiceConfiguration(sinkSettings);
                this.sut = new TraceEventService(configuration);

                slabListener = new InMemoryEventListener();
                slabListener.EnableEvents(SemanticLoggingEventSource.Log, EventLevel.Error, SemanticLoggingEventSource.Keywords.TraceEvent);
                this.sut.Start();
            }

            protected override void When()
            {
                MyCompanyEventSource.Log.PageStart(10, "test");
            }

            [TestMethod]
            public void then_exception_is_handled_and_logged()
            {
                // Wait for event to be processed
                Assert.IsTrue(slabListener.WaitOnAsyncEvents.WaitOne(AsyncProcessTimeout));

                Assert.AreEqual(0, formatter.WriteEventCalls.Count);
                StringAssert.Contains(slabListener.ToString(), "unhandled_exception_test");
            }

            protected override void OnCleanup()
            {
                slabListener.DisableEvents(SemanticLoggingEventSource.Log);
                base.OnCleanup();
            }
        }

        [TestClass]
        public class when_logging_to_provider_created_in_two_different_sessions : given_traceEventService
        {
            private const string SessionName2 = "when_logging_to_provider_created_in_two_different_sessions";
            private TraceEventService sut2;
            private InMemoryEventListener inMemoryListener2;
            private MyCompanyEventSource logger;

            protected override void Given()
            {
                RemoveAnyExistingSession(SessionName2);
                base.Given();

                inMemoryListener2 = new InMemoryEventListener(formatter);
                var sink = new Lazy<IObserver<EventEntry>>(() => inMemoryListener2);
                var localSourceSettings = new EventSourceSettings(EventSource.GetName(typeof(MyCompanyEventSource)), level: EventLevel.Informational, matchAnyKeyword: MyCompanyEventSource.Keywords.Page);
                var localEventSources = new List<EventSourceSettings> { localSourceSettings };
                var localSinkSettings = new List<SinkSettings> { new SinkSettings("test", sink, localEventSources) };
                var localConfiguration = new TraceEventServiceConfiguration(localSinkSettings, new TraceEventServiceSettings { SessionNamePrefix = SessionName2 });
                this.sut2 = new TraceEventService(localConfiguration);
            }

            protected override void When()
            {
                // Start EventSource instance
                logger = MyCompanyEventSource.Log;

                // Start both instances
                this.sut.Start();
                this.sut2.Start();

                // log from single source to both sessions
                logger.PageStart(10, "test");
            }

            [TestMethod]
            public void then_event_is_collected_and_processed_in_first_session()
            {
                // Wait for event to be processed
                inMemoryListener.WaitOnAsyncEvents.WaitOne(this.AsyncProcessTimeout);
                inMemoryListener2.WaitOnAsyncEvents.WaitOne(this.AsyncProcessTimeout);

                Assert.AreEqual(1, inMemoryListener.EventWrittenCount, "No event get to inMemoryListener");
                Assert.AreEqual("loading page test activityID=1010,test", inMemoryListener.ToString());

                // In case the second listener got events because the cached manifest was already initialized...
                if (inMemoryListener2.EventWrittenCount > 0)
                {
                    Assert.AreEqual(1, inMemoryListener2.EventWrittenCount, "No event get to inMemoryListener2");
                    Assert.AreEqual("loading page test activityID=1010,test", inMemoryListener2.ToString());
                }
            }

            protected override void OnCleanup()
            {
                this.sut2.Dispose();
                base.OnCleanup();
            }
        }

        [TestClass]
        public class when_tryToUpdateConfiguration_with_new_level : given_traceEventService
        {
            protected override void Given()
            {
                base.Given();
                this.sut.Start();
            }

            protected override void When()
            {
                var current = this.configuration.SinkSettings[0];
                current.EventSources.First().Level = EventLevel.Warning;
                var newSink = new SinkSettings(current.Name, current.Sink, current.EventSources);

                // will trigger changed event
                this.configuration.SinkSettings.Remove(current);
                this.configuration.SinkSettings.Add(newSink);
            }

            [TestMethod]
            public void then_new_configuration_is_updated()
            {
                // This event should be filtered out
                MyCompanyEventSource.Log.PageStart(10, "test");
                // This event should be logged
                MyCompanyEventSource.Log.Failure("failure");

                // Wait for event to be processed
                inMemoryListener.WaitOnAsyncEvents.WaitOne(this.AsyncProcessTimeout);

                // Only Info event is logged
                Assert.AreEqual(1, formatter.WriteEventCalls.Count);
                var entry = formatter.WriteEventCalls.SingleOrDefault(e => e.FormattedMessage == "Application Failure: failure");
                Assert.IsNotNull(entry);
            }
        }

        [TestClass]
        public class when_tryToUpdateConfiguration_with_new_source : given_traceEventService
        {
            protected override void Given()
            {
                base.Given();
                this.sut.Start();
            }

            protected override void When()
            {
                var current = this.configuration.SinkSettings[0];
                var newSources = new List<EventSourceSettings>(current.EventSources);
                newSources.Add(new EventSourceSettings(EventSource.GetName(typeof(TestEventSource))));
                var newSink = new SinkSettings(current.Name, current.Sink, newSources);

                // will trigger changed event
                this.configuration.SinkSettings.Remove(current);
                this.configuration.SinkSettings.Add(newSink);

                // We expect 2 events
                this.inMemoryListener.WaitSignalCondition = () => inMemoryListener.EventWrittenCount == 2;
            }

            [TestMethod]
            public void then_new_configuration_is_updated()
            {
                // This event from former source should be logged
                MyCompanyEventSource.Log.PageStart(10, "test");
                // This event from new source should be logged
                TestEventSource.Log.UsingEnumArguments(MyLongEnum.Value2, MyIntEnum.Value3);

                // Wait for event to be processed
                inMemoryListener.WaitOnAsyncEvents.WaitOne(this.AsyncProcessTimeout);

                Assert.IsTrue(formatter.WriteEventCalls.Any(e => e.EventId == 3));
                Assert.IsTrue(formatter.WriteEventCalls.Any(e => e.EventId == 305));
            }
        }

        [TestClass]
        public class when_tryToUpdateConfiguration_with_new_listener : given_traceEventService
        {
            protected override void Given()
            {
                base.Given();
                this.sut.Start();
            }

            protected override void When()
            {
                // We expect 3 events, 2 for listener1(Level=LogAlways) and 1 for listener2 (Level=Warning)
                inMemoryListener.WaitSignalCondition = () => inMemoryListener.EventWrittenCount == 3;

                var sink = new Lazy<IObserver<EventEntry>>(() => inMemoryListener);
                var localSourceSettings = new EventSourceSettings(EventSource.GetName(typeof(MyCompanyEventSource)), level: EventLevel.Warning);
                var localEventSources = new List<EventSourceSettings> { localSourceSettings };
                this.configuration.SinkSettings.Add(new SinkSettings("test2", sink, localEventSources));
            }

            [TestMethod]
            public void then_new_configuration_is_updated()
            {
                // This event should be filtered out
                MyCompanyEventSource.Log.PageStart(10, "test");
                // This event should be logged
                MyCompanyEventSource.Log.Failure("failure");

                // Wait for event to be processed
                Assert.IsTrue(inMemoryListener.WaitOnAsyncEvents.WaitOne(this.AsyncProcessTimeout), "wait timed out");

                Assert.AreEqual(2, formatter.WriteEventCalls.Count(e => e.FormattedMessage == "Application Failure: failure"));
                Assert.AreEqual(1, formatter.WriteEventCalls.Count(e => e.FormattedMessage == "loading page test activityID=10"));
            }
        }

        [TestClass]
        public class when_updating_manifest_and_logging_a_new_event : given_traceEventService
        {
            private DisposableDomain domain1;
            private DisposableDomain domain2;

            protected override void Given()
            {
                this.domain1 = new DisposableDomain();
                this.domain2 = new DisposableDomain();

                var initialManifest = EventSource.GenerateManifest(typeof(MyNewCompanyEventSource), null);
                this.sourceSettings = new EventSourceSettings(EventSource.GetName(typeof(MyNewCompanyEventSource)));
                base.Given();

                // We expect 2 events
                this.inMemoryListener.WaitSignalCondition = () => inMemoryListener.EventWrittenCount == 2;

                this.sut.Start();
            }

            protected override void When()
            {
                this.domain1.DoCallBack(() =>
                {
                    MyNewCompanyEventSource.Logger.Event1(11);
                });

                this.domain2.DoCallBack(() =>
                {
                    //Send the new event to update the manifest
                    MyNewCompanyEventSource2.Logger.Event2(22);
                });
            }

            protected override void OnCleanup()
            {
                this.domain1.Dispose();
                this.domain2.Dispose();
                base.OnCleanup();
            }

            [TestMethod]
            public void then_manifest_is_updated_and_event_is_collected_and_processed()
            {
                // Wait for event to be processed
                bool signaled = inMemoryListener.WaitOnAsyncEvents.WaitOne(this.AsyncProcessTimeout);

                Assert.IsTrue(signaled);

                var entry1 = formatter.WriteEventCalls.FirstOrDefault();
                Assert.IsNotNull(entry1);
                var entry2 = formatter.WriteEventCalls.LastOrDefault();
                Assert.IsNotNull(entry2);

                Assert.AreEqual(MyNewCompanyEventSource.Logger.Name, entry1.Schema.ProviderName);
                Assert.AreEqual(sourceSettings.EventSourceId, entry1.ProviderId);
                Assert.AreEqual("Event1 ID=11", entry1.FormattedMessage);
                Assert.AreEqual(EventOpcode.Start, entry1.Schema.Opcode);
                Assert.AreEqual(1, entry1.EventId);

                Assert.AreEqual(MyNewCompanyEventSource2.Logger.Name, entry2.Schema.ProviderName);
                Assert.AreEqual(sourceSettings.EventSourceId, entry2.ProviderId);
                Assert.AreEqual("Event2 ID=22", entry2.FormattedMessage);
                Assert.AreEqual(EventOpcode.Stop, entry2.Schema.Opcode);
                Assert.AreEqual(2, entry2.EventId);
            }

            [EventSource(Name = "MyCompany1")]
            private sealed class MyNewCompanyEventSource : EventSource
            {
                public static class Tasks
                {
                    public const EventTask Custom = (EventTask)1;
                }

                [Event(1, Task = Tasks.Custom, Opcode = EventOpcode.Start, Message = "Event1 ID={0}")]
                public void Event1(int id)
                {
                    if (IsEnabled()) { WriteEvent(1, id); }
                }

                public static readonly MyNewCompanyEventSource Logger = new MyNewCompanyEventSource();
            }

            [EventSource(Name = "MyCompany1")]
            private sealed class MyNewCompanyEventSource2 : EventSource
            {
                public static class Tasks
                {
                    public const EventTask Custom = (EventTask)1;
                }

                [Event(1, Task = Tasks.Custom, Opcode = EventOpcode.Start, Message = "Event1 ID={0}")]
                public void Event1(int id)
                {
                    if (IsEnabled()) { WriteEvent(1, id); }
                }

                [Event(2, Task = Tasks.Custom, Opcode = EventOpcode.Stop, Message = "Event2 ID={0}")]
                public void Event2(int id)
                {
                    if (IsEnabled()) { WriteEvent(2, id); }
                }

                public static readonly MyNewCompanyEventSource2 Logger = new MyNewCompanyEventSource2();
            }
        }

        [TestClass]
        public class when_logging_an_event_with_multiple_payload_types : given_traceEventService
        {
            protected override void Given()
            {
                this.sourceSettings = new EventSourceSettings(EventSource.GetName(typeof(MultipleTypesEventSource)));
                base.Given();
                this.sut.Start();
            }

            protected override void When()
            {
                MultipleTypesEventSource.Log.ManyTypes(1, 2, 3, 4, 5, 6, 7, 8, 9, true, "test", Guid.NewGuid(), MultipleTypesEventSource.Color.Blue, 10);
            }

            [TestMethod]
            public void then_event_is_collected_and_processed()
            {
                // Wait for event to be processed
                inMemoryListener.WaitOnAsyncEvents.WaitOne(this.AsyncProcessTimeout);

                Assert.AreEqual(1, inMemoryListener.EventWrittenCount);
                var entry = formatter.WriteEventCalls.FirstOrDefault();
                Assert.IsNotNull(entry);

                Assert.AreEqual(MultipleTypesEventSource.Log.Name, entry.Schema.ProviderName);
                Assert.AreEqual(sourceSettings.EventSourceId, entry.ProviderId);
                Assert.AreEqual((int)MultipleTypesEventSource.Color.Blue, entry.Payload[entry.Schema.Payload.ToList().FindIndex(m => m == "arg16")]);
            }
        }

        [TestClass]
        public class when_logging_with_multiple_enum_types : given_traceEventService
        {
            private InMemoryEventListener slabListener;

            protected override void Given()
            {
                this.sourceSettings = new EventSourceSettings(EventSource.GetName(typeof(DifferentEnumsEventSource)));
                base.Given();

                this.slabListener = new InMemoryEventListener();
                this.slabListener.EnableEvents(SemanticLoggingEventSource.Log, EventLevel.Error, SemanticLoggingEventSource.Keywords.TraceEvent);

                this.sut.Start();
            }

            protected override void When()
            {
                DifferentEnumsEventSource.Log.UsingAllEnumArguments(MyLongEnum.Value1, MyIntEnum.Value2, MyShortEnum.Value3,
                    MyByteEnum.Value1, MySByteEnum.Value2, MyUShortEnum.Value3, MyUIntEnum.Value1, MyULongEnum.Value2);
            }

            [TestMethod]
            [Ignore]    // TraceEvent does not notify of manifest failures
            public void then_error_is_logged_to_slab_eventsource()
            {
                // Wait for event to be processed
                slabListener.WaitOnAsyncEvents.WaitOne(AsyncProcessTimeout);

                Assert.AreEqual(0, formatter.WriteEventCalls.Count);
                StringAssert.Contains(slabListener.ToString(), "EventId : 810");
            }

            protected override void OnCleanup()
            {
                slabListener.DisableEvents(SemanticLoggingEventSource.Log);
                base.OnCleanup();
            }
        }

        [TestClass]
        public class when_logging_with_a_provider_with_large_manifest : given_traceEventService
        {
            protected override void Given()
            {
                this.sourceSettings = new EventSourceSettings(EventSource.GetName(typeof(LargeManifestEventSource)));
                base.Given();
                this.sut.Start();
            }

            protected override void When()
            {
                LargeManifestEventSource.Log.MultipleKeywords11(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11);
            }

            [TestMethod]
            public void then_event_is_collected_and_processed()
            {
                // Wait for event to be processed
                Assert.IsTrue(inMemoryListener.WaitOnAsyncEvents.WaitOne(this.AsyncProcessTimeout));

                Assert.AreEqual(1, inMemoryListener.EventWrittenCount);
                var entry = formatter.WriteEventCalls.FirstOrDefault();
                Assert.IsNotNull(entry);

                Assert.AreEqual(LargeManifestEventSource.Log.Name, entry.Schema.ProviderName);
                Assert.AreEqual(sourceSettings.EventSourceId, entry.ProviderId);
            }
        }

        [TestClass]
        public class when_logging_with_a_provider_with_large_manifest_from_two_event_source_instances : given_traceEventService
        {
            private DisposableDomain domain1;
            private DisposableDomain domain2;

            protected override void Given()
            {
                this.domain1 = new DisposableDomain();
                this.domain2 = new DisposableDomain();
                this.sourceSettings = new EventSourceSettings(EventSource.GetName(typeof(LargeManifestEventSource)));

                base.Given();

                // We expect 2 events
                this.inMemoryListener.WaitSignalCondition = () => inMemoryListener.EventWrittenCount == 2;

                this.sut.Start();
            }

            protected override void When()
            {
                this.domain1.DoCallBack(() =>
                    {
                        LargeManifestEventSource.Log.MultipleKeywords11(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11);
                    });

                this.domain2.DoCallBack(() =>
                    {
                        LargeManifestEventSource.Log.MultipleKeywords11(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11);
                    });
            }

            protected override void OnCleanup()
            {
                this.domain1.Dispose();
                this.domain2.Dispose();
                base.OnCleanup();
            }

            [TestMethod]
            public void then_both_events_are_collected_and_processed()
            {
                // Wait for event to be processed
                Assert.IsTrue(inMemoryListener.WaitOnAsyncEvents.WaitOne(this.AsyncProcessTimeout));

                var entry0 = formatter.WriteEventCalls.ElementAt(0);
                var entry1 = formatter.WriteEventCalls.ElementAt(1);

                Assert.AreEqual(LargeManifestEventSource.Log.Name, entry0.Schema.ProviderName);
                Assert.AreEqual(sourceSettings.EventSourceId, entry0.ProviderId);

                Assert.AreEqual(LargeManifestEventSource.Log.Name, entry1.Schema.ProviderName);
                Assert.AreEqual(sourceSettings.EventSourceId, entry1.ProviderId);
            }
        }

        [TestClass]
        public class when_logging_an_event_from_a_process_process_included_in_filters : given_traceEventService
        {
            protected override void Given()
            {
                this.sourceSettings =
                    new EventSourceSettings(
                        EventSource.GetName(typeof(MyCompanyEventSource)),
                    processNameFilters: new[] { System.IO.Path.GetFileName(Process.GetCurrentProcess().MainModule.FileName) });
                base.Given();
                this.sut.Start();
            }

            protected override void When()
            {
                MyCompanyEventSource.Log.PageStart(10, "test");
            }

            [TestMethod]
            public void then_event_is_collected_and_processed()
            {
                // Wait for event to be processed
                inMemoryListener.WaitOnAsyncEvents.WaitOne(this.AsyncProcessTimeout);

                Assert.AreEqual(1, inMemoryListener.EventWrittenCount);
                var entry = formatter.WriteEventCalls.FirstOrDefault();
                Assert.IsNotNull(entry);

                Assert.AreEqual(MyCompanyEventSource.Log.Name, entry.Schema.ProviderName);
                Assert.AreEqual(sourceSettings.EventSourceId, entry.ProviderId);
                Assert.AreEqual("loading page test activityID=10", entry.FormattedMessage);
                Assert.AreEqual(EventOpcode.Start, entry.Schema.Opcode);
                Assert.AreEqual(3, entry.EventId);
                Assert.AreEqual(Guid.Empty, entry.ActivityId);
                Assert.AreEqual(Guid.Empty, entry.RelatedActivityId);
            }
        }

        [TestClass]
        public class when_logging_an_event_from_a_process_process_not_included_in_filters : given_traceEventService
        {
            protected override void Given()
            {
                this.sourceSettings =
                    new EventSourceSettings(
                        EventSource.GetName(typeof(MyCompanyEventSource)),
                    processNameFilters: new[] { Guid.NewGuid().ToString() });
                base.Given();
                this.sut.Start();
            }

            protected override void When()
            {
                MyCompanyEventSource.Log.PageStart(10, "test");
            }

            [TestMethod]
            public void then_event_is_not_collected()
            {
                // Wait for event to be processed
                Assert.IsFalse(inMemoryListener.WaitOnAsyncEvents.WaitOne(this.AsyncProcessTimeout));

                Assert.AreEqual(0, inMemoryListener.EventWrittenCount);
            }
        }

        [TestClass]
        public class when_logging_multiple_events_from_a_process_process_without_sampling : given_traceEventService
        {
            protected override void Given()
            {
                this.sourceSettings =
                    new EventSourceSettings(
                        EventSource.GetName(typeof(MyCompanyEventSource)));
                base.Given();
                this.inMemoryListener.WaitSignalCondition = () => inMemoryListener.EventWrittenCount == 15;
                this.sut.Start();
            }

            protected override void When()
            {
                Guid currentActivityGuid = EventSource.CurrentThreadActivityId;

                try
                {
                    for (int i = 0; i < 15; i++)
                    {
                        EventSource.SetCurrentThreadActivityId(Guid.NewGuid());
                        MyCompanyEventSource.Log.PageStart(i, "test");
                    }
                }
                finally
                {
                    EventSource.SetCurrentThreadActivityId(currentActivityGuid);
                }
            }

            [TestMethod]
            public void then_all_events_are_collected_and_processed()
            {
                // Wait for event to be processed
                inMemoryListener.WaitOnAsyncEvents.WaitOne(this.AsyncProcessTimeout);

                Assert.AreEqual(15, this.inMemoryListener.EventWrittenCount);
                Assert.AreEqual(0, this.formatter.WriteEventCalls[0].Payload[0]);
                Assert.AreEqual(1, this.formatter.WriteEventCalls[1].Payload[0]);
                Assert.AreEqual(2, this.formatter.WriteEventCalls[2].Payload[0]);
                Assert.AreEqual(3, this.formatter.WriteEventCalls[3].Payload[0]);
                Assert.AreEqual(4, this.formatter.WriteEventCalls[4].Payload[0]);
                Assert.AreEqual(5, this.formatter.WriteEventCalls[5].Payload[0]);
                Assert.AreEqual(6, this.formatter.WriteEventCalls[6].Payload[0]);
                Assert.AreEqual(7, this.formatter.WriteEventCalls[7].Payload[0]);
                Assert.AreEqual(8, this.formatter.WriteEventCalls[8].Payload[0]);
                Assert.AreEqual(9, this.formatter.WriteEventCalls[9].Payload[0]);
                Assert.AreEqual(10, this.formatter.WriteEventCalls[10].Payload[0]);
                Assert.AreEqual(11, this.formatter.WriteEventCalls[11].Payload[0]);
                Assert.AreEqual(12, this.formatter.WriteEventCalls[12].Payload[0]);
                Assert.AreEqual(13, this.formatter.WriteEventCalls[13].Payload[0]);
                Assert.AreEqual(14, this.formatter.WriteEventCalls[14].Payload[0]);
            }
        }

#if !EVENT_SOURCE_PACKAGE
        // sampling not supported when using the NuGet package
        [TestClass]
        public class when_logging_multiple_events_from_a_process_process_with_sampling : given_traceEventService
        {
            protected override void Given()
            {
                this.sourceSettings =
                    new EventSourceSettings(
                        EventSource.GetName(typeof(MyCompanyEventSource)),
                    arguments: new[] { new KeyValuePair<string, string>("ActivitySamplingStartEvent", "PageStart:5") });
                base.Given();
                this.inMemoryListener.WaitSignalCondition = () => inMemoryListener.EventWrittenCount == 3;
                this.sut.Start();
            }

            protected override void When()
            {
                Guid currentActivityGuid = EventSource.CurrentThreadActivityId;

                try
                {
                    for (int i = 0; i < 15; i++)
                    {
                        EventSource.SetCurrentThreadActivityId(Guid.NewGuid());
                        MyCompanyEventSource.Log.PageStart(i, "test");
                    }
                }
                finally
                {
                    EventSource.SetCurrentThreadActivityId(currentActivityGuid);
                }
            }

            [TestMethod]
            public void then_sampled_events_are_collected_and_processed()
            {
                // Wait for event to be processed
                inMemoryListener.WaitOnAsyncEvents.WaitOne(this.AsyncProcessTimeout);

                Assert.AreEqual(3, this.inMemoryListener.EventWrittenCount);
                Assert.AreEqual(0, this.formatter.WriteEventCalls[0].Payload[0]);
                Assert.AreEqual(5, this.formatter.WriteEventCalls[1].Payload[0]);
                Assert.AreEqual(10, this.formatter.WriteEventCalls[2].Payload[0]);
            }
        }
#endif
    }
}
