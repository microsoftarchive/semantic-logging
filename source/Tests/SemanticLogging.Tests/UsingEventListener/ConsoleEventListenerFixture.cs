// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Formatters;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.TestObjects;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.EventListeners
{
    public abstract class given_console_event_listener : ContextBase
    {
        protected MockDefaultConsoleColorMapper colorMapper;
        protected MockConsoleOutput mockConsole;
        protected ObservableEventListener listener;
        protected EventTextFormatter formatter;
        protected IEnumerable<string> entries
        {
            get { return Regex.Split(mockConsole.Ouput, formatter.Header + "\r\n").Where(c => !string.IsNullOrWhiteSpace(c)); }
        }

        protected override void Given()
        {
            colorMapper = new MockDefaultConsoleColorMapper();
            mockConsole = new MockConsoleOutput();
            formatter = new EventTextFormatter(EventTextFormatter.DashSeparator) { VerbosityThreshold = EventLevel.Informational };
            listener = new ObservableEventListener();
            listener.LogToConsole(formatter, colorMapper);
            listener.EnableEvents(TestEventSource.Log, EventLevel.LogAlways);
        }

        protected override void OnCleanup()
        {
            base.OnCleanup();
            mockConsole.Dispose();
            listener.DisableEvents(TestEventSource.Log);
            listener.Dispose();
        }

        [TestClass]
        public class when_receiving_event_without_payload_nor_message : given_console_event_listener
        {
            protected override void When()
            {
                TestEventSource.Log.EventWithoutPayloadNorMessage();
            }

            [TestMethod]
            public void then_writes_event_to_console()
            {
                var entry = this.entries.SingleOrDefault();

                Assert.IsNotNull(entry);
                Assert.AreEqual(DefaultConsoleColorMapper.Warning, colorMapper.Color);

                StringAssert.Contains(entry.ToString(), "EventId : 200");
                StringAssert.Contains(entry.ToString(), "Level : Warning");
                StringAssert.Contains(entry.ToString(), "Message : ");
                StringAssert.Contains(entry.ToString(), "Payload :");
            }
        }

        [TestClass]
        public class when_receiving_event_with_payload : given_console_event_listener
        {
            protected override void When()
            {
                TestEventSource.Log.EventWithPayload("payload", 100);
            }

            [TestMethod]
            public void then_writes_event_to_console()
            {
                var entry = this.entries.SingleOrDefault();

                Assert.IsNotNull(entry);
                StringAssert.Contains(entry, TestEventSource.EventWithPayloadId.ToString());
                Assert.AreEqual(DefaultConsoleColorMapper.Warning, colorMapper.Color);
                StringAssert.Contains(entry.ToString(), "EventId : 201");
                StringAssert.Contains(entry.ToString(), "Level : Warning");
                StringAssert.Contains(entry.ToString(), "Payload : [payload1 : payload] [payload2 : 100]");
            }
        }

        [TestClass]
        public class when_receiving_event_with_message : given_console_event_listener
        {
            protected override void When()
            {
                TestEventSource.Log.EventWithMessage();
            }

            [TestMethod]
            public void then_writes_event_to_console()
            {
                var entry = this.entries.SingleOrDefault();

                Assert.IsNotNull(entry);
                StringAssert.Contains(entry, TestEventSource.EventWithMessageId.ToString());
                Assert.AreEqual(DefaultConsoleColorMapper.Warning, colorMapper.Color);

                StringAssert.Contains(entry.ToString(), "EventId : 202");
                StringAssert.Contains(entry.ToString(), "Level : Warning");
                StringAssert.Contains(entry.ToString(), "Message : Test message");
            }
        }

        [TestClass]
        public class when_receiving_event_with_payload_and_message : given_console_event_listener
        {
            protected override void When()
            {
                TestEventSource.Log.EventWithPayloadAndMessage("payload", 100);
            }

            [TestMethod]
            public void then_writes_event_to_console()
            {
                var entry = this.entries.SingleOrDefault();

                Assert.IsNotNull(entry);
                StringAssert.Contains(entry, TestEventSource.EventWithPayloadAndMessageId.ToString());
                Assert.AreEqual(DefaultConsoleColorMapper.Warning, colorMapper.Color);

                StringAssert.Contains(entry.ToString(), "EventId : 203");
                StringAssert.Contains(entry.ToString(), "Level : Warning");
                StringAssert.Contains(entry.ToString(), "Message : Test message payload 100");
                StringAssert.Contains(entry.ToString(), "Payload : [payload1 : payload] [payload2 : 100]");
            }
        }

        [TestClass]
        public class when_receiving_multiple_events_with_message : given_console_event_listener
        {
            protected override void When()
            {
                TestEventSource.Log.EventWithMessage();
                TestEventSource.Log.EventWithMessage();
                TestEventSource.Log.EventWithMessage();
            }

            [TestMethod]
            public void then_writes_event_to_console()
            {
                Assert.AreEqual(3, this.entries.Count());
                Assert.IsTrue(this.entries.All(e => e.Contains("Test message")));
            }
        }

        [TestClass]
        public class when_receiving_information_event_with_message : given_console_event_listener
        {
            private ConsoleColor color;

            protected override void When()
            {
                color = Console.ForegroundColor;
                TestEventSource.Log.Informational("Test");
            }

            [TestMethod]
            public void then_writes_event_to_console_with_information_foreground_color()
            {
                var entry = this.entries.SingleOrDefault();

                Assert.IsNotNull(entry);
                StringAssert.Contains(entry, TestEventSource.InformationalEventId.ToString());
                Assert.AreEqual(color, colorMapper.Color);
            }
        }

        [TestClass]
        public class when_receiving_verbose_event_with_message : given_console_event_listener
        {
            protected override void When()
            {
                TestEventSource.Log.Write("Test");
            }

            [TestMethod]
            public void then_writes_event_to_console_with_verbose_foreground_color()
            {
                var entry = this.entries.SingleOrDefault();

                Assert.IsNotNull(entry);
                StringAssert.Contains(entry, TestEventSource.VerboseEventId.ToString());
                Assert.AreEqual(DefaultConsoleColorMapper.Verbose, colorMapper.Color);
            }
        }

        [TestClass]
        public class when_receiving_error_event_with_message : given_console_event_listener
        {
            protected override void When()
            {
                TestEventSource.Log.Error("Test");
            }

            [TestMethod]
            public void then_writes_event_to_console_with_error_foreground_color()
            {
                var entry = this.entries.SingleOrDefault();

                Assert.IsNotNull(entry);
                StringAssert.Contains(entry, TestEventSource.ErrorEventId.ToString());
                Assert.AreEqual(DefaultConsoleColorMapper.Error, colorMapper.Color);
            }
        }

        [TestClass]
        public class when_receiving_critical_event_with_message : given_console_event_listener
        {
            protected override void When()
            {
                TestEventSource.Log.Critical("Test");
            }

            [TestMethod]
            public void then_writes_event_to_console_with_critical_foreground_color()
            {
                var entry = this.entries.SingleOrDefault();

                Assert.IsNotNull(entry);
                StringAssert.Contains(entry, TestEventSource.CriticalEventId.ToString());
                Assert.AreEqual(DefaultConsoleColorMapper.Critical, colorMapper.Color);
            }
        }

        [TestClass]
        public class when_providing_a_custom_formatter_with_payload : given_console_event_listener
        {
            protected override void Given()
            {
                this.mockConsole = new MockConsoleOutput();
                this.listener = new ObservableEventListener();
                this.listener.LogToConsole(new MockFormatter());
                this.listener.EnableEvents(TestEventSource.Log, EventLevel.LogAlways);
            }

            protected override void When()
            {
                TestEventSource.Log.EventWithPayload("payload1", 100);
            }

            [TestMethod]
            public void then_writes_event_to_console()
            {
                Assert.AreEqual("payload1,100", this.mockConsole.Ouput);
            }
        }

        [TestClass]
        public class when_providing_a_custom_colormapper_with_error : given_console_event_listener
        {
            private MockColorMapper mockColorMapper;

            protected override void Given()
            {
                this.mockColorMapper = new MockColorMapper();
                this.mockConsole = new MockConsoleOutput();
                this.listener = new ObservableEventListener();
                this.listener.LogToConsole(null, this.mockColorMapper);
                this.listener.EnableEvents(TestEventSource.Log, EventLevel.LogAlways);
            }

            protected override void When()
            {
                TestEventSource.Log.Error("error");
            }

            [TestMethod]
            public void then_writes_event_to_console()
            {
                Assert.AreEqual(MockColorMapper.Error, this.mockColorMapper.Color);
            }
        }

        [TestClass]
        public class when_receiving_concurrent_events_with_message : given_console_event_listener
        {
            private const int MaxLoggedEntries = 20;

            protected override void When()
            {
                Parallel.For(0, MaxLoggedEntries, i => TestEventSource.Log.Informational("Info " + i));
            }

            [TestMethod]
            public void then_writes_multiple_events_to_console()
            {
                Assert.AreEqual<int>(MaxLoggedEntries, this.entries.Count());
            }
        }

        [TestClass]
        public class when_providing_a_failed_formatter_with_payload : given_console_event_listener
        {
            private InMemoryEventListener collectErrorsListener;

            protected override void Given()
            {
                this.mockConsole = new MockConsoleOutput();
                // Will always throw
                this.listener = new ObservableEventListener();
                this.listener.LogToConsole(new MockFormatter() { BeforeWriteEventAction = (f) => { throw new InvalidOperationException(); } });
                this.listener.EnableEvents(TestEventSource.Log, EventLevel.LogAlways);
                collectErrorsListener = new InMemoryEventListener();
                collectErrorsListener.EnableEvents(SemanticLoggingEventSource.Log, EventLevel.Error, SemanticLoggingEventSource.Keywords.Sink);
            }

            protected override void When()
            {
                TestEventSource.Log.EventWithPayload("payload1", 100);
            }

            protected override void OnCleanup()
            {
                base.OnCleanup();
                collectErrorsListener.Dispose();
            }

            [TestMethod]
            public void then_writes_event_to_errors_source()
            {
                StringAssert.Contains(collectErrorsListener.ToString(), "EventId : 1100");
                StringAssert.Contains(collectErrorsListener.ToString(), "Level : Critical");
                StringAssert.Contains(collectErrorsListener.ToString(), "Payload : [message : System.InvalidOperationException");
            }

            [TestMethod]
            public void then_does_not_write_to_console()
            {
                Assert.AreEqual(string.Empty, this.mockConsole.Ouput);
            }
        }

        [TestClass]
        public class when_a_formatter_fails_flush_should_not_dump_invalid_output : given_console_event_listener
        {
            protected override void Given()
            {
                this.mockConsole = new MockConsoleOutput();
                // Will throw on first write only
                this.listener = new ObservableEventListener();
                this.listener.LogToConsole(new MockFormatter() { AfterWriteEventAction = (f) => { if (f.WriteEventCalls.Count == 1) { throw new InvalidOperationException(); } } });
                this.listener.EnableEvents(TestEventSource.Log, EventLevel.LogAlways);
            }

            protected override void When()
            {
                // First log will fail
                TestEventSource.Log.EventWithPayload("payload1", 100);
                // Second log will succeed and first error log should not be flushed 
                TestEventSource.Log.EventWithPayloadAndMessage("payload2", 10);
            }

            [TestMethod]
            public void then_only_non_faulted_event_is_written()
            {
                Assert.IsTrue(this.mockConsole.Ouput.StartsWith("Test message payload2 10"));
            }
        }
    }
}
