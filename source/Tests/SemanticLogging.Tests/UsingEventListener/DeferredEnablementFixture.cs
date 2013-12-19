// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.UsingEventListener
{
    [TestClass]
    public class DeferredEnablementFixture
    {
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void EnablingNullEventSourceNameThrows()
        {
            using (var listener = new ObservableEventListener())
            {
                listener.EnableEvents((string)null, EventLevel.LogAlways);
            }
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void EnablingEmptyEventSourceNameThrows()
        {
            using (var listener = new ObservableEventListener())
            {
                listener.EnableEvents(string.Empty, EventLevel.LogAlways);
            }
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void EnablingNonObservableEventListenerThrows()
        {
            using (var listener = new NotObservableEventListener())
            {
                listener.EnableEvents("some source name", EventLevel.LogAlways);
            }
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void DisablingNonObservableEventListenerThrows()
        {
            using (var listener = new NotObservableEventListener())
            {
                listener.DisableEvents("some source name");
            }
        }

        [TestMethod]
        public void EnablingOnExistingEventSourceIsImmediateUsingSuppliedParameters()
        {
            ExecuteWithHelperInAppDomain(
                helper => helper.EnablingOnExistingEventSourceIsImmediateUsingSuppliedParameters());
        }

        [TestMethod]
        public void EnablingWithoutKeywordsDefaultsToNone()
        {
            ExecuteWithHelperInAppDomain(
                helper => helper.EnablingWithoutKeywordsDefaultsToNone());
        }

        [TestMethod]
        public void EnablingOnNonExistingEventSourceIsDeferredUsingSuppliedParameters()
        {
            ExecuteWithHelperInAppDomain(
                helper => helper.EnablingOnNonExistingEventSourceIsDeferredUsingSuppliedParameters());
        }

        [TestMethod]
        public void OnlyLastDeferredEnableRequestIsHonored()
        {
            ExecuteWithHelperInAppDomain(
                helper => helper.OnlyLastDeferredEnableRequestIsHonored());
        }

        [TestMethod]
        public void AllNonDeferredEnableRequestAreHonored()
        {
            ExecuteWithHelperInAppDomain(
                helper => helper.AllNonDeferredEnableRequestAreHonored());
        }

        [TestMethod]
        public void DeferredRequestCopiesDictionaryValues()
        {
            ExecuteWithHelperInAppDomain(
                helper => helper.DeferredRequestCopiesDictionaryValues());
        }

        [TestMethod]
        public void CanDisableEventsOnNonEnabledNonCreatedEventSource()
        {
            ExecuteWithHelperInAppDomain(
                helper => helper.CanDisableEventsOnNonEnabledNonCreatedEventSource());
        }

        [TestMethod]
        public void CanDisableEventsOnNonEnabledCreatedEventSource()
        {
            ExecuteWithHelperInAppDomain(
                helper => helper.CanDisableEventsOnNonEnabledCreatedEventSource());
        }

        [TestMethod]
        public void CanDisableEventsOnEnabledEventSourceBeforeItIsCreated()
        {
            ExecuteWithHelperInAppDomain(
                helper => helper.CanDisableEventsOnEnabledEventSourceBeforeItIsCreated());
        }

        [TestMethod]
        public void CanDisableEventsOnEnabledEventSourceAfterItIsCreated()
        {
            ExecuteWithHelperInAppDomain(
                helper => helper.CanDisableEventsOnEnabledEventSourceAfterItIsCreated());
        }

        [TestMethod]
        public void CanDeferRequestsForMultipleSources()
        {
            ExecuteWithHelperInAppDomain(
                helper => helper.CanDeferRequestsForMultipleSources());
        }

        public static void ExecuteWithHelperInAppDomain(Action<DeferredEnablementFixtureTestHelper> testAction)
        {
            var appDomain = AppDomain.CreateDomain("test app domain", AppDomain.CurrentDomain.Evidence, AppDomain.CurrentDomain.SetupInformation);
            try
            {
                var helper =
                    (DeferredEnablementFixtureTestHelper)appDomain.CreateInstanceAndUnwrap(
                        typeof(DeferredEnablementFixtureTestHelper).Assembly.FullName,
                        typeof(DeferredEnablementFixtureTestHelper).FullName);

                testAction(helper);
            }
            finally
            {
                AppDomain.Unload(appDomain);
            }
        }
    }

    public class DeferredEnablementFixtureTestHelper : MarshalByRefObject
    {
        public void EnablingOnExistingEventSourceIsImmediateUsingSuppliedParameters()
        {
            var eventSourceName = SensingEventSource.Log.Name;

            using (EventListener listener = new ObservableEventListener())
            {
                var immediate = listener.EnableEvents(eventSourceName, EventLevel.Error, EventKeywords.AuditSuccess | EventKeywords.AuditFailure);

                Assert.IsTrue(immediate);

                Assert.IsTrue(SensingEventSource.Log.IsEnabled(EventLevel.Error, EventKeywords.AuditFailure));
                Assert.IsFalse(SensingEventSource.Log.IsEnabled(EventLevel.Error, EventKeywords.EventLogClassic));
                Assert.IsFalse(SensingEventSource.Log.IsEnabled(EventLevel.Warning, EventKeywords.AuditFailure));
            }
        }

        public void EnablingWithoutKeywordsDefaultsToNone()
        {
            var eventSourceName = SensingEventSource.Log.Name;

            using (EventListener listener = new ObservableEventListener())
            {
                var immediate = listener.EnableEvents(eventSourceName, EventLevel.Error);

                Assert.IsTrue(immediate);

                Assert.IsTrue(SensingEventSource.Log.IsEnabled(EventLevel.Error, EventKeywords.None));
                Assert.IsTrue(SensingEventSource.Log.IsEnabled(EventLevel.Error, EventKeywords.AuditFailure));
                Assert.IsFalse(SensingEventSource.Log.IsEnabled(EventLevel.Warning, EventKeywords.None));
            }
        }

        public void EnablingOnNonExistingEventSourceIsDeferredUsingSuppliedParameters()
        {
            var eventSourceName = "SensingEventSource";

            using (EventListener listener = new ObservableEventListener())
            {
                var immediate = listener.EnableEvents(eventSourceName, EventLevel.Error, EventKeywords.AuditSuccess | EventKeywords.AuditFailure);

                Assert.IsFalse(immediate);

                Assert.IsTrue(SensingEventSource.Log.IsEnabled(EventLevel.Error, EventKeywords.AuditFailure));
                Assert.IsFalse(SensingEventSource.Log.IsEnabled(EventLevel.Error, EventKeywords.EventLogClassic));
                Assert.IsFalse(SensingEventSource.Log.IsEnabled(EventLevel.Warning, EventKeywords.AuditFailure));
            }
        }

        public void OnlyLastDeferredEnableRequestIsHonored()
        {
            var eventSourceName = "SensingEventSource";

            using (EventListener listener = new ObservableEventListener())
            {
                listener.EnableEvents(eventSourceName, EventLevel.Error, EventKeywords.AuditSuccess | EventKeywords.AuditFailure);
                listener.EnableEvents(eventSourceName, EventLevel.Informational, EventKeywords.Sqm, new Dictionary<string, string> { { "key1", "value1" } });
                listener.EnableEvents(eventSourceName, EventLevel.Warning, EventKeywords.EventLogClassic, new Dictionary<string, string> { { "key2", "value2" } });

                Assert.IsTrue(SensingEventSource.Log.IsEnabled(EventLevel.Warning, EventKeywords.EventLogClassic));
                Assert.AreEqual(1, SensingEventSource.Log.Commands.Count);
                Assert.AreEqual(1, SensingEventSource.Log.Commands[0].Count);
                Assert.AreEqual("value2", SensingEventSource.Log.Commands[0]["key2"]);
            }
        }

        public void AllNonDeferredEnableRequestAreHonored()
        {
            var eventSourceName = SensingEventSource.Log.Name;

            using (EventListener listener = new ObservableEventListener())
            {
                listener.EnableEvents(eventSourceName, EventLevel.Error, EventKeywords.AuditSuccess | EventKeywords.AuditFailure);
                listener.EnableEvents(eventSourceName, EventLevel.Informational, EventKeywords.Sqm, new Dictionary<string, string> { { "key1", "value1" } });
                listener.EnableEvents(eventSourceName, EventLevel.Warning, EventKeywords.EventLogClassic, new Dictionary<string, string> { { "key2", "value2" } });

                Assert.IsTrue(SensingEventSource.Log.IsEnabled(EventLevel.Warning, EventKeywords.EventLogClassic));
                Assert.AreEqual(3, SensingEventSource.Log.Commands.Count);
                Assert.AreEqual(0, SensingEventSource.Log.Commands[0].Count);
                Assert.AreEqual(1, SensingEventSource.Log.Commands[1].Count);
                Assert.AreEqual("value1", SensingEventSource.Log.Commands[1]["key1"]);
                Assert.AreEqual(1, SensingEventSource.Log.Commands[2].Count);
                Assert.AreEqual("value2", SensingEventSource.Log.Commands[2]["key2"]);
            }
        }

        public void DeferredRequestCopiesDictionaryValues()
        {
            var eventSourceName = "SensingEventSource";

            using (EventListener listener = new ObservableEventListener())
            {
                var commands = new Dictionary<string, string>();
                commands["key"] = "oldvalue";
                var immediate = listener.EnableEvents(eventSourceName, EventLevel.Warning, EventKeywords.EventLogClassic, commands);

                Assert.IsFalse(immediate);
                commands["key"] = "newvalue";
                Assert.AreEqual("oldvalue", SensingEventSource.Log.Commands[0]["key"]);
            }
        }

        public void CanDisableEventsOnNonEnabledNonCreatedEventSource()
        {
            var eventSourceName = "SensingEventSource";

            using (EventListener listener = new ObservableEventListener())
            {
                listener.DisableEvents(eventSourceName);

                Assert.IsFalse(SensingEventSource.Log.IsEnabled());
            }
        }

        public void CanDisableEventsOnNonEnabledCreatedEventSource()
        {
            var eventSourceName = SensingEventSource.Log.Name;

            using (EventListener listener = new ObservableEventListener())
            {
                listener.DisableEvents(eventSourceName);

                Assert.IsFalse(SensingEventSource.Log.IsEnabled());
            }
        }

        public void CanDisableEventsOnEnabledEventSourceBeforeItIsCreated()
        {
            var eventSourceName = "SensingEventSource";

            using (EventListener listener = new ObservableEventListener())
            {
                listener.EnableEvents(eventSourceName, EventLevel.Error, EventKeywords.AuditSuccess | EventKeywords.AuditFailure);
                listener.EnableEvents(eventSourceName, EventLevel.Informational, EventKeywords.Sqm, new Dictionary<string, string> { { "key1", "value1" } });
                listener.EnableEvents(eventSourceName, EventLevel.Warning, EventKeywords.EventLogClassic, new Dictionary<string, string> { { "key2", "value2" } });
                listener.DisableEvents(eventSourceName);

                Assert.IsFalse(SensingEventSource.Log.IsEnabled());
                Assert.AreEqual(0, SensingEventSource.Log.Commands.Count);
            }
        }

        public void CanDisableEventsOnEnabledEventSourceAfterItIsCreated()
        {
            var eventSourceName = "SensingEventSource";

            using (EventListener listener = new ObservableEventListener())
            {
                listener.EnableEvents(eventSourceName, EventLevel.Error, EventKeywords.AuditSuccess | EventKeywords.AuditFailure);
                listener.EnableEvents(eventSourceName, EventLevel.Informational, EventKeywords.Sqm, new Dictionary<string, string> { { "key1", "value1" } });
                listener.EnableEvents(eventSourceName, EventLevel.Warning, EventKeywords.EventLogClassic, new Dictionary<string, string> { { "key2", "value2" } });

                Assert.IsTrue(SensingEventSource.Log.IsEnabled());
                Assert.AreEqual(1, SensingEventSource.Log.Commands.Count);

                listener.DisableEvents(eventSourceName);

                Assert.IsFalse(SensingEventSource.Log.IsEnabled());
                Assert.AreEqual(2, SensingEventSource.Log.Commands.Count);
            }
        }

        public void CanDeferRequestsForMultipleSources()
        {
            using (EventListener listener = new ObservableEventListener())
            {
                listener.EnableEvents("DeferredEventSource1", EventLevel.Error, EventKeywords.AuditSuccess | EventKeywords.AuditFailure);
                listener.EnableEvents("DeferredEventSource2", EventLevel.Informational, EventKeywords.Sqm, new Dictionary<string, string> { { "key1", "value1" } });
                listener.EnableEvents("DeferredEventSource3", EventLevel.Warning, EventKeywords.EventLogClassic, new Dictionary<string, string> { { "key2", "value2" } });
                listener.EnableEvents("DeferredEventSource2", EventLevel.Warning, EventKeywords.EventLogClassic, new Dictionary<string, string> { { "key2", "value2" } });
                listener.EnableEvents("DeferredEventSource1", EventLevel.Warning, EventKeywords.EventLogClassic, new Dictionary<string, string> { { "key2", "value2" } });
                listener.EnableEvents("DeferredEventSource1", EventLevel.Informational, EventKeywords.Sqm, new Dictionary<string, string> { { "key1", "value1" } });
                listener.DisableEvents("DeferredEventSource2");
                listener.EnableEvents("DeferredEventSource2", EventLevel.Error, EventKeywords.AuditSuccess | EventKeywords.AuditFailure);
                listener.DisableEvents("DeferredEventSource3");

                Assert.IsTrue(DeferredEventSource1.Log.IsEnabled(EventLevel.Informational, EventKeywords.Sqm));
                Assert.IsFalse(DeferredEventSource1.Log.IsEnabled(EventLevel.Verbose, EventKeywords.Sqm));
                Assert.IsTrue(DeferredEventSource2.Log.IsEnabled(EventLevel.Error, EventKeywords.AuditSuccess));
                Assert.IsFalse(DeferredEventSource2.Log.IsEnabled(EventLevel.Warning, EventKeywords.AuditSuccess));
                Assert.IsFalse(DeferredEventSource3.Log.IsEnabled());
            }
        }
    }

    public class NotObservableEventListener : EventListener
    {
        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            throw new NotImplementedException();
        }
    }

    [EventSource(Name = "SensingEventSource")]
    public sealed class SensingEventSource : EventSource
    {
        public static SensingEventSource Log { get { return LogHolder.Value; } }
        private static Lazy<SensingEventSource> LogHolder = new Lazy<SensingEventSource>(() => new SensingEventSource());

        public readonly List<IDictionary<string, string>> Commands = new List<IDictionary<string, string>>();

        private SensingEventSource()
        {
        }

        protected override void OnEventCommand(EventCommandEventArgs command)
        {
            base.OnEventCommand(command);

            this.Commands.Add(command.Arguments);
        }
    }

    [EventSource(Name = "DeferredEventSource1")]
    public sealed class DeferredEventSource1 : EventSource
    {
        public static DeferredEventSource1 Log { get { return LogHolder.Value; } }
        private static Lazy<DeferredEventSource1> LogHolder = new Lazy<DeferredEventSource1>(() => new DeferredEventSource1());

        private DeferredEventSource1()
        {
        }
    }

    [EventSource(Name = "DeferredEventSource2")]
    public sealed class DeferredEventSource2 : EventSource
    {
        public static DeferredEventSource2 Log { get { return LogHolder.Value; } }
        private static Lazy<DeferredEventSource2> LogHolder = new Lazy<DeferredEventSource2>(() => new DeferredEventSource2());

        private DeferredEventSource2()
        {
        }
    }

    [EventSource(Name = "DeferredEventSource3")]
    public sealed class DeferredEventSource3 : EventSource
    {
        public static DeferredEventSource3 Log { get { return LogHolder.Value; } }
        private static Lazy<DeferredEventSource3> LogHolder = new Lazy<DeferredEventSource3>(() => new DeferredEventSource3());

        private DeferredEventSource3()
        {
        }
    }
}
