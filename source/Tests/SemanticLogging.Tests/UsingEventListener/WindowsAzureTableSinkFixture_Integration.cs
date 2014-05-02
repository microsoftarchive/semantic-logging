// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Diagnostics.Tracing;
using System.Linq;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Sinks;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.TestObjects;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.EventListeners.WindowsAzureTableSinkFixture_Integration
{
    public class given_empty_account : ArrangeActAssert
    {
        protected readonly TestEventSource Logger = TestEventSource.Log;
        protected string connectionString;
        private CloudStorageAccount account;
        protected CloudTableClient client;
        protected string tableName;
        protected WindowsAzureTableSink sink;
        protected ObservableEventListener listener;

        protected override void Arrange()
        {
            this.connectionString = ConfigurationHelper.GetSetting("StorageConnectionString");

            if (string.IsNullOrEmpty(connectionString)
                || connectionString.Contains("[AccountName]")
                || connectionString.Contains("[AccountKey]"))
            {
                Assert.Inconclusive("Cannot run tests because the Windows Azure Storage credentials are not configured");
            }

            this.account = CloudStorageAccount.Parse(connectionString);
            this.client = this.account.CreateCloudTableClient();
            this.tableName = "AzureTableEventListenerTests" + new Random(unchecked((int)DateTime.Now.Ticks)).Next(10000).ToString();

            this.listener = new ObservableEventListener();
            this.sink = this.listener.LogToWindowsAzureTable("TestInstanceName", connectionString, tableName).Sink;
        }

        protected override void Teardown()
        {
            base.Teardown();
            this.listener.Dispose();

            if (this.tableName != null)
            {
                this.account.CreateCloudTableClient().GetTableReference(tableName).DeleteIfExists();
            }
        }
    }

    [TestClass]
    public class when_writing_to_in_ascending_order : given_empty_account
    {
        protected override void Act()
        {
            base.Act();

            sink.SortKeysAscending = true;
            listener.EnableEvents(Logger, EventLevel.LogAlways);

            Logger.Informational("Information message");
            Logger.Error("Error message");
            Logger.Critical("Critical message");
        }

        [TestMethod]
        public void then_orders_them_in_ascending_order()
        {
            Assert.IsTrue(this.sink.FlushAsync().Wait(TimeSpan.FromSeconds(45)));

            var table = client.GetTableReference(tableName);
            var query = new TableQuery<TestCloudTableEntry>();
            var list = table.ExecuteQuery(query).ToArray();

            Assert.AreEqual<int>(3, list.Count());
            Assert.AreEqual<int>(TestEventSource.InformationalEventId, list.First().EventId);
            Assert.AreEqual<int>(TestEventSource.ErrorEventId, list.ElementAt(1).EventId);
            Assert.AreEqual<int>(TestEventSource.CriticalEventId, list.Last().EventId);
        }
    }

    [TestClass]
    public class when_writing_to_storage : given_empty_account
    {
        protected override void Act()
        {
            base.Act();

            listener.EnableEvents(Logger, EventLevel.LogAlways);

            Logger.Informational("Information message");
            Logger.Error("Error message");
            Logger.Critical("Critical message");

            Logger.EventWithoutPayloadNorMessage();
        }

        [TestMethod]
        public void then_can_force_flush_messages()
        {
            var table = client.GetTableReference(tableName);
            var query = new TableQuery<TestCloudTableEntry>();

            Assert.IsTrue(this.sink.FlushAsync().Wait(TimeSpan.FromSeconds(45)));

            var list = table.ExecuteQuery(query).ToArray();

            Assert.AreEqual<int>(4, list.Count());
            Assert.IsTrue(list.Any(x => x.EventId == TestEventSource.InformationalEventId));
            Assert.IsTrue(list.Any(x => x.EventId == TestEventSource.CriticalEventId));
        }

        [TestMethod]
        public void then_orders_them_from_newer_to_older()
        {
            var table = client.GetTableReference(tableName);
            var query = new TableQuery<TestCloudTableEntry>();

            Assert.IsTrue(this.sink.FlushAsync().Wait(TimeSpan.FromSeconds(45)));

            var list = table.ExecuteQuery<TestCloudTableEntry>(query).ToArray();

            Assert.AreEqual<int>(4, list.Count());
            Assert.AreEqual<int>(TestEventSource.EventWithoutPayloadNorMessageId, list.ElementAt(0).EventId);
            Assert.AreEqual<int>(TestEventSource.CriticalEventId, list.ElementAt(1).EventId);
            Assert.AreEqual<int>(TestEventSource.ErrorEventId, list.ElementAt(2).EventId);
            Assert.AreEqual<int>(TestEventSource.InformationalEventId, list.ElementAt(3).EventId);
        }

        [TestMethod]
        public void then_includes_process_and_thread_id()
        {
            var processId = System.Diagnostics.Process.GetCurrentProcess().Id;
            var threadId = Utility.NativeMethods.GetCurrentThreadId();

            var table = client.GetTableReference(tableName);
            var query = new TableQuery<TestCloudTableEntry>();

            Assert.IsTrue(this.sink.FlushAsync().Wait(TimeSpan.FromSeconds(45)));

            var list = table.ExecuteQuery<TestCloudTableEntry>(query).ToArray();

            Assert.AreEqual<int>(4, list.Count());
            foreach (var entry in list)
            {
                Assert.AreEqual(processId, entry.ProcessId);
                Assert.AreEqual(threadId, entry.ThreadId);
            }
        }
    }

    [TestClass]
    public class when_writing_to_storage_with_activity_id : given_empty_account
    {
        private Guid activityId, relatedActivityId, previousActivityId;

        protected override void Arrange()
        {
            base.Arrange();

            this.activityId = Guid.NewGuid();
            this.relatedActivityId = Guid.NewGuid();

            EventSource.SetCurrentThreadActivityId(this.activityId, out this.previousActivityId);
        }

        protected override void Act()
        {
            base.Act();

            listener.EnableEvents(Logger, EventLevel.LogAlways);

            Logger.Informational("Information message");
            Logger.EventWithPayloadAndMessageAndRelatedActivityId(this.relatedActivityId, string.Empty, 0);
        }

        protected override void Teardown()
        {
            base.Teardown();

            EventSource.SetCurrentThreadActivityId(this.previousActivityId);
        }

        [TestMethod]
        public void then_includes_activity_id_in_all_events()
        {
            var table = client.GetTableReference(tableName);
            var query = new TableQuery<TestCloudTableEntry>();

            Assert.IsTrue(this.sink.FlushAsync().Wait(TimeSpan.FromSeconds(45)));

            var list = table.ExecuteQuery(query).ToArray();

            Assert.AreEqual<int>(2, list.Count());
            Assert.IsTrue(list.All(x => x.ActivityId == this.activityId));
        }

        [TestMethod]
        public void then_includes_related_activity_id_in_event()
        {
            var table = client.GetTableReference(tableName);
            var query = new TableQuery<TestCloudTableEntry>();

            Assert.IsTrue(this.sink.FlushAsync().Wait(TimeSpan.FromSeconds(45)));

            var list = table.ExecuteQuery(query).ToArray();

            Assert.AreEqual<int>(2, list.Count());
            Assert.AreEqual(this.relatedActivityId, list.First(x => x.EventId == TestEventSource.EventWithPayloadAndMessageAndRelatedActivityIdId).RelatedActivityId);
        }
    }

    [TestClass]
    public class when_writing_with_version_opcode_level : given_empty_account
    {
        protected override void Act()
        {
            base.Act();

            listener.EnableEvents(Logger, EventLevel.LogAlways);

            Logger.NonDefaultOpcodeNonDefaultVersionEvent(1, 2, 3);
        }

        [TestMethod]
        public void then_all_entries_with_version_opcode_level_are_written()
        {
            Assert.IsTrue(this.sink.FlushAsync().Wait(TimeSpan.FromSeconds(45)));

            var table = client.GetTableReference(tableName);
            var query = new TableQuery<TestCloudTableEntry>();
            var list = table.ExecuteQuery(query).ToArray();

            Assert.AreEqual<int>(1, list.Count());
            Assert.AreEqual<int>(2, list.First().Version);
            Assert.AreEqual<int>((int)EventOpcode.Reply, list.First().Opcode);
            Assert.AreEqual<int>((int)EventLevel.Informational, list.First().Level);
        }
    }    
}
