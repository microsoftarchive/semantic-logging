// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Sinks;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Sinks.WindowsAzure;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.TestObjects;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.Sinks
{
    [TestClass]
    public class given_configuration
    {
        private const string DevelopmentStorageConnectionString = "UseDevelopmentStorage=true";

        [TestMethod]
        public void when_creating_sink_for_null_connection_string_then_throws()
        {
            AssertEx.Throws<ArgumentNullException>(() => new WindowsAzureTableSink("instanceName", null, "Table", TimeSpan.FromSeconds(1), 10000, Timeout.InfiniteTimeSpan));
        }

        [TestMethod]
        public void when_creating_sink_with_invalid_connection_string_then_throws()
        {
            AssertEx.Throws<ArgumentException>(() => new WindowsAzureTableSink("instanceName", "InvalidConnection", "Table", TimeSpan.FromSeconds(1), 10000, Timeout.InfiniteTimeSpan));
        }

        [TestMethod]
        public void when_creating_sink_with_small_buffer_size_then_throws()
        {
            AssertEx.Throws<ArgumentException>(() => new WindowsAzureTableSink("instanceName", DevelopmentStorageConnectionString, "Table", TimeSpan.FromSeconds(1), 10, Timeout.InfiniteTimeSpan));
        }

        [TestMethod]
        public void when_creating_sink_sort_is_descending_by_default()
        {
            Assert.IsFalse(new WindowsAzureTableSink("instanceName", DevelopmentStorageConnectionString, "Table", TimeSpan.FromSeconds(1), 10000, Timeout.InfiniteTimeSpan).SortKeysAscending);
        }

        [TestMethod]
        public void tablename_is_invalid_if_starts_with_number()
        {
            AssertEx.Throws<ArgumentException>(() => new WindowsAzureTableSink("instanceName", DevelopmentStorageConnectionString, "123dfadfasfdasfd", TimeSpan.FromSeconds(1), 10000, Timeout.InfiniteTimeSpan));
        }

        [TestMethod]
        public void tablename_is_invalid_if_longer_than_64()
        {
            AssertEx.Throws<ArgumentException>(() => new WindowsAzureTableSink("instanceName", DevelopmentStorageConnectionString, "windowsazurewindowsazurewindowsazurewindowsazurewindowsazurewind", TimeSpan.FromSeconds(1), 10000, Timeout.InfiniteTimeSpan));
        }

        [TestMethod]
        [Ignore]
        // Ignoring because the storage client library considers inexistant account as transient so it retries and the exponential back-off could take time
        public void when_cannot_connect_to_storage_account_then_flush_should_finish_faulted()
        {
            const string ValidNotExisting = "DefaultEndpointsProtocol=https;AccountName=InexistantDoesntReallyMatter;AccountKey=aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa==";

            using (var sink = new WindowsAzureTableSink("instanceName", ValidNotExisting, "Table", TimeSpan.FromSeconds(1), 5000, TimeSpan.FromSeconds(20)))
            using (var collectErrorsListener = new MockEventListener())
            {
                collectErrorsListener.EnableEvents(SemanticLoggingEventSource.Log, EventLevel.Error, Keywords.All);

                sink.OnNext(new CloudEventEntry(EventEntryTestHelper.Create()));
                try
                {
                    Assert.IsTrue(sink.FlushAsync().Wait(TimeSpan.FromSeconds(15)));
                    Assert.Fail("Exception should be thrown.");
                }
                catch (AggregateException ex)
                {
                    Assert.IsInstanceOfType(ex.InnerException, typeof(FlushFailedException));
                }

                Assert.IsTrue(collectErrorsListener.WrittenEntries.Any(x => x.EventId == 500));
            }
        }

        [TestMethod]
        [Ignore]
        // Ignoring because the storage client library considers inexistant account as transient so it retries and the exponential back-off could take time
        public void when_cannot_connect_to_storage_account_then_on_completed_should_not_stall_or_throw()
        {
            const string ValidNotExisting = "DefaultEndpointsProtocol=https;AccountName=InexistantDoesntReallyMatter;AccountKey=aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa==";

            using (var sink = new WindowsAzureTableSink("instanceName", ValidNotExisting, "Table", TimeSpan.FromSeconds(1), 5000, TimeSpan.FromSeconds(20)))
            using (var collectErrorsListener = new MockEventListener())
            {
                collectErrorsListener.EnableEvents(SemanticLoggingEventSource.Log, EventLevel.Error, Keywords.All);

                sink.OnNext(new CloudEventEntry(EventEntryTestHelper.Create()));
                Assert.IsTrue(Task.Run(() => sink.OnCompleted()).Wait(TimeSpan.FromSeconds(15)));

                Assert.IsTrue(collectErrorsListener.WrittenEntries.Any(x => x.EventId == 500));
            }
        }
    }

    [TestClass]
    public class given_azure_table_event_entry
    {
        [TestMethod]
        public void when_writing_entity_adds_payload_to_dictionary()
        {
            var entity = new CloudEventEntry(EventEntryTestHelper.Create(payloadNames: new string[] { "message1", "message2" }, payload: new object[] { "value1", "value2" }));
            entity.CreateKey(true, 0);

            var dict = entity.CreateTableEntity().WriteEntity(null);

            Assert.IsTrue(dict.ContainsKey("Payload_message1"));
            Assert.IsTrue(dict.ContainsKey("Payload_message2"));
        }

        [TestMethod]
        public void when_writing_entity_keeps_proper_type()
        {
            var guid = Guid.NewGuid();
            var binary = new byte[] { 1, 2, 3, 4 };

            var entity = new CloudEventEntry(EventEntryTestHelper.Create(
                payloadNames: new string[] { "string1", "int1", "long1", "double1", "bool1", "bool2", "guid1", "binary1" },
                payload: new object[] { "This is a string", 123456, 123456L, 123456D, true, false, guid, binary }));
            entity.CreateKey(true, 0);

            var dict = entity.CreateTableEntity().WriteEntity(null);

            Assert.AreEqual<string>("This is a string", dict["Payload_string1"].StringValue);
            Assert.AreEqual<int>(123456, dict["Payload_int1"].Int32Value.Value);
            Assert.AreEqual<long>(123456L, dict["Payload_long1"].Int64Value.Value);
            Assert.AreEqual<double>(123456D, dict["Payload_double1"].DoubleValue.Value);
            Assert.AreEqual<Guid>(guid, dict["Payload_guid1"].GuidValue.Value);
            Assert.AreEqual<bool>(true, dict["Payload_bool1"].BooleanValue.Value);
            Assert.AreEqual<bool>(false, dict["Payload_bool2"].BooleanValue.Value);
            Assert.AreEqual<byte[]>(binary, dict["Payload_binary1"].BinaryValue);
        }

        [TestMethod]
        public void when_generating_key_then_prefixes_with_instance_name()
        {
            var entity = new CloudEventEntry(EventEntryTestHelper.Create(
                timestamp: DateTimeOffset.UtcNow))
            {
                InstanceName = "MyInstanceName"
            };

            entity.CreateKey(false, 0);

            StringAssert.StartsWith(entity.RowKey, "MyInstanceName");
        }

        [TestMethod]
        public void when_having_big_message_value_then_truncates()
        {
            var entity = new CloudEventEntry(EventEntryTestHelper.Create(formattedMessage: new string('a', 500000)));
            entity.CreateKey(true, 0);

            var dict = entity.CreateTableEntity().WriteEntity(null);

            Assert.AreEqual(new string('a', 30000) + "--TRUNCATED--", dict["Message"].StringValue);
        }

        [TestMethod]
        public void when_having_big_payload_value_then_stores_warning_and_does_not_contain_payload()
        {
            var entity = new CloudEventEntry(EventEntryTestHelper.Create(
                payloadNames: new string[] { "arg1" },
                payload: new object[] { new string('a', 500000) }));
            entity.CreateKey(true, 0);

            var dict = entity.CreateTableEntity().WriteEntity(null);

            StringAssert.Contains(dict["Payload"].StringValue, "'payload_serialization_error'");
            Assert.IsFalse(dict.ContainsKey("Payload_arg1"));
        }

        [TestMethod]
        public void when_having_big_payload_then_truncates()
        {
            var entity = new CloudEventEntry(EventEntryTestHelper.Create(payloadNames: new string[] { "arg1" }, payload: new object[] { new string('a', 500000) }));
            entity.CreateKey(true, 0);

            var dict = entity.CreateTableEntity().WriteEntity(null);

            StringAssert.Contains(dict["Payload"].StringValue, "'payload_serialization_error'");
            Assert.IsFalse(dict.ContainsKey("Payload_arg1"));
        }

        [TestMethod]
        public void when_having_big_overall_payloads_then_stores_warning_and_does_not_contain_payload()
        {
            var entity = new CloudEventEntry(EventEntryTestHelper.Create(payloadNames: Enumerable.Range(0, 50).Select(i => "arg" + i), payload: Enumerable.Range(0, 50).Select(i => new string('a', 2000))));
            entity.CreateKey(true, 0);

            var dict = entity.CreateTableEntity().WriteEntity(null);

            StringAssert.Contains(dict["Payload"].StringValue, "'payload_serialization_error'");

            Assert.AreEqual(0, dict.Keys.Count(x => x.StartsWith("Payload_")));
        }

        [TestMethod]
        public void when_several_payload_values_then_takes_first_ones_as_columns()
        {
            int numberOfAllowedItems = 200;

            var entity = new CloudEventEntry(EventEntryTestHelper.Create(
                payloadNames: Enumerable.Range(0, 300).Select(i => "arg" + i),
                payload: Enumerable.Range(0, 300).Select(i => (object)i)));
            entity.CreateKey(true, 0);

            var dict = entity.CreateTableEntity().WriteEntity(null);

            for (int i = 0; i < numberOfAllowedItems; i++)
            {
                Assert.IsTrue(dict.ContainsKey("Payload_arg" + i), i.ToString());
                Assert.AreEqual<int>(i, dict["Payload_arg" + i].Int32Value.Value);
            }

            for (int i = numberOfAllowedItems; i < 300; i++)
            {
                Assert.IsFalse(dict.ContainsKey("Payload_arg" + i), i.ToString());
            }

            var deserializedPayloadField = JsonConvert.DeserializeObject<Dictionary<string, object>>(dict["Payload"].StringValue);

            foreach (var payloadItem in entity.Payload)
            {
                Assert.IsTrue(deserializedPayloadField.ContainsKey(payloadItem.Key));
                Assert.AreEqual<int>((int)payloadItem.Value, (int)(long)deserializedPayloadField[payloadItem.Key]);
            }
        }
    }

    [TestClass]
    public class given_bounded_windows_azure_table_sink : ContextBase
    {
        private const int BufferSize = 500;

        private TestableWindowsAzureTableSink sink;
        private MockEventListener collectErrorsListener;

        protected override void Given()
        {
            this.collectErrorsListener = new MockEventListener();
            this.collectErrorsListener.EnableEvents(SemanticLoggingEventSource.Log, EventLevel.Informational, SemanticLoggingEventSource.Keywords.Sink);

            this.sink = new TestableWindowsAzureTableSink("TestName", maxBufferSize: BufferSize);
            sink.WaitHandle.Reset();
        }

        protected override void OnCleanup()
        {
            this.sink.Dispose();
            this.collectErrorsListener.Dispose();
        }

        [TestMethod]
        public void when_overflowing_buffer_capacity_then_sends_only_buffer_capacity()
        {
            const int NumberOfEntries = 1200;

            sink.WaitHandle.Reset();

            for (int i = 0; i < NumberOfEntries; i++)
            {
                sink.OnNext(new CloudEventEntry(EventEntryTestHelper.Create(eventId: 10, payloadNames: new string[] { "arg" }, payload: new object[] { i })));
            }

            sink.WaitHandle.Set();

            Assert.IsTrue(sink.FlushAsync().Wait(TimeSpan.FromSeconds(10)));

            Assert.AreEqual(BufferSize, sink.SentEntriesCount);
        }

        [TestMethod]
        public void when_overflowing_buffer_capacity_then_notifies_once()
        {
            const int NumberOfEntries = 600;

            sink.WaitHandle.Reset();

            for (int i = 0; i < NumberOfEntries; i++)
            {
                sink.OnNext(new CloudEventEntry(EventEntryTestHelper.Create(eventId: 10, payloadNames: new string[] { "arg" }, payload: new object[] { i })));
            }

            sink.WaitHandle.Set();

            Assert.IsTrue(sink.FlushAsync().Wait(TimeSpan.FromSeconds(10)));

            Assert.AreEqual(1, collectErrorsListener.WrittenEntries.Count(e => e.EventId == 900));
        }

        [TestMethod]
        public void when_restoring_buffer_capacity_then_writes_events()
        {
            const int NumberOfNewEntries = 200;

            sink.WaitHandle.Reset();

            for (int i = 0; i < 600; i++)
            {
                sink.OnNext(new CloudEventEntry(EventEntryTestHelper.Create(eventId: 10, payloadNames: new string[] { "arg" }, payload: new object[] { i })));
            }

            sink.WaitHandle.Set();

            Assert.IsTrue(sink.FlushAsync().Wait(TimeSpan.FromSeconds(10)));

            for (int i = 0; i < NumberOfNewEntries; i++)
            {
                sink.OnNext(new CloudEventEntry(EventEntryTestHelper.Create(eventId: 10, payloadNames: new string[] { "arg" }, payload: new object[] { i })));
            }

            Assert.IsTrue(sink.FlushAsync().Wait(TimeSpan.FromSeconds(10)));
            Assert.AreEqual(BufferSize + NumberOfNewEntries, sink.SentEntriesCount);
        }

        [TestMethod]
        public void when_restoring_buffer_capacity_then_notifies_once()
        {
            const int NumberOfEntries = 600;

            sink.WaitHandle.Reset();

            for (int i = 0; i < NumberOfEntries; i++)
            {
                sink.OnNext(new CloudEventEntry(EventEntryTestHelper.Create(eventId: 10, payloadNames: new string[] { "arg" }, payload: new object[] { i })));
            }

            sink.WaitHandle.Set();

            Assert.IsTrue(sink.FlushAsync().Wait(TimeSpan.FromSeconds(10)));

            Assert.AreEqual(1, collectErrorsListener.WrittenEntries.Count(e => e.EventId == 901));
        }
    }

    [TestClass]
    public class given_sink_with_onCompleted_timeout : ContextBase
    {
        [TestMethod]
        public void when_sending_on_completed_blocks_for_timeout_period_only()
        {
            var timeout = TimeSpan.FromSeconds(1.75);
            var delta = TimeSpan.FromSeconds(.5);
            using (var sink = new TestableWindowsAzureTableSink("TestName", onCompletedTimeout: timeout))
            {
                sink.WaitHandle.Reset();

                sink.OnNext(new CloudEventEntry(EventEntryTestHelper.Create(eventId: 10)));

                var stopWatch = Stopwatch.StartNew();
                sink.OnCompleted();
                stopWatch.Stop();

                Assert.IsTrue(stopWatch.Elapsed >= timeout - delta, "Elapsed: {0}. Expected at least {1}", stopWatch.Elapsed, timeout - delta);
                Assert.IsTrue(stopWatch.Elapsed <= timeout + delta, "Elapsed: {0}. Expected at most {1}", stopWatch.Elapsed, timeout + delta);

                sink.WaitHandle.Set();
            }
        }
    }

    internal class TestableWindowsAzureTableSink : WindowsAzureTableSink
    {
        public int SentEntriesCount = 0;

        public ManualResetEventSlim WaitHandle = new ManualResetEventSlim(true);

        public TestableWindowsAzureTableSink(string instanceName, int maxBufferSize = 500, TimeSpan? onCompletedTimeout = null)
            : base(instanceName, "UseDevelopmentStorage=true", "LogsTableAddess", TimeSpan.FromSeconds(5), maxBufferSize, onCompletedTimeout ?? Timeout.InfiniteTimeSpan)
        {
        }

        internal override Task<IList<TableResult>> ExecuteBatchAsync(TableBatchOperation batch)
        {
            SentEntriesCount += batch.Count;
            return Task.Run(() =>
            {
                WaitHandle.Wait();
                return (IList<TableResult>)new List<TableResult>();
            });
        }

        internal override async Task<bool> EnsureTableExistsAsync()
        {
            await Task.Yield();
            return true;
        }
    }
}
