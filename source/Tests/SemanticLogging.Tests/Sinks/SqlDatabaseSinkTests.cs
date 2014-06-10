// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Configuration;
using System.Data.SqlClient;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Sinks;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.Properties;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.TestObjects;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.TestSupport;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Utility;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.Sinks
{
    [TestClass]
    public class sql_db_sink_given_configuration
    {
        private static readonly string ValidConnectionString = ConfigurationManager.ConnectionStrings["valid"].ConnectionString;
        private static readonly string InvalidConnectionString = ConfigurationManager.ConnectionStrings["invalid"].ConnectionString;

        [TestMethod]
        public void when_creating_listener_for_null_instance_name_then_throws()
        {
            AssertEx.Throws<ArgumentNullException>(() => new SqlDatabaseSink(null, "valid", "tableName", Buffering.DefaultBufferingInterval, Buffering.DefaultBufferingCount, Buffering.DefaultMaxBufferSize, Timeout.InfiniteTimeSpan));
        }

        [TestMethod]
        public void when_creating_listener_for_null_connection_string_then_throws()
        {
            AssertEx.Throws<ArgumentNullException>(() => new SqlDatabaseSink("test", null, "tableName", Buffering.DefaultBufferingInterval, Buffering.DefaultBufferingCount, Buffering.DefaultMaxBufferSize, Timeout.InfiniteTimeSpan));
        }

        [TestMethod]
        public void when_creating_listener_with_invalid_connection_string_then_throws()
        {
            AssertEx.Throws<ArgumentException>(() => new SqlDatabaseSink("test", InvalidConnectionString, "tableName", Buffering.DefaultBufferingInterval, Buffering.DefaultBufferingCount, Buffering.DefaultMaxBufferSize, Timeout.InfiniteTimeSpan));
        }

        [TestMethod]
        public void when_cannot_connect_to_database_then_flush_should_finish_faulted()
        {
            const string ValidNotExisting = @"Data Source=(localdb)\v11.0; AttachDBFilename='|DataDirectory|\DoesNotExist.mdf';Initial Catalog=SemanticLoggingTests;Integrated Security=True";

            using (var sink = new SqlDatabaseSink("test", ValidNotExisting, "tableName", Buffering.DefaultBufferingInterval, Buffering.DefaultBufferingCount, Buffering.DefaultMaxBufferSize, TimeSpan.FromSeconds(20)))
            using (var collectErrorsListener = new MockEventListener())
            {
                collectErrorsListener.EnableEvents(SemanticLoggingEventSource.Log, EventLevel.Error, Keywords.All);

                sink.OnNext(EventEntryTestHelper.Create());
                try
                {
                    Assert.IsTrue(sink.FlushAsync().Wait(TimeSpan.FromSeconds(5)));
                    Assert.Fail("Exception should be thrown.");
                }
                catch (AggregateException ex)
                {
                    Assert.IsInstanceOfType(ex.InnerException, typeof(FlushFailedException));
                }

                Assert.IsTrue(collectErrorsListener.WrittenEntries.Any(x => x.EventId == 101));
            }
        }

        [TestMethod]
        public void when_cannot_connect_to_database_then_on_completed_should_not_stall_or_throw()
        {
            const string ValidNotExisting = @"Data Source=(localdb)\v11.0; AttachDBFilename='|DataDirectory|\DoesNotExist.mdf';Initial Catalog=SemanticLoggingTests;Integrated Security=True";

            using (var sink = new SqlDatabaseSink("test", ValidNotExisting, "tableName", Buffering.DefaultBufferingInterval, Buffering.DefaultBufferingCount, Buffering.DefaultMaxBufferSize, TimeSpan.FromSeconds(20)))
            using (var collectErrorsListener = new MockEventListener())
            {
                collectErrorsListener.EnableEvents(SemanticLoggingEventSource.Log, EventLevel.Error, Keywords.All);

                sink.OnNext(EventEntryTestHelper.Create());
                Assert.IsTrue(Task.Run(() => sink.OnCompleted()).Wait(TimeSpan.FromSeconds(5)));

                Assert.IsTrue(collectErrorsListener.WrittenEntries.Any(x => x.EventId == 101));
            }
        }
    }

    public class given_empty_logging_database : LocalDatabaseContext
    {
        protected override string GetLocalDatabaseFileName()
        {
            return "sqldbtests" + new Random().Next(1000, 9999).ToString();
        }

        protected override void Given()
        {
            base.Given();

            this.localDbConnection.ChangeDatabase(this.dbName);

            using (var cmd = new SqlCommand(Resources.CreateTracesTable, this.localDbConnection))
            {
                cmd.ExecuteNonQuery();
            }

            using (var cmd = new SqlCommand(Resources.CreateTracesType, this.localDbConnection))
            {
                cmd.ExecuteNonQuery();
            }

            using (var cmd = new SqlCommand(Resources.CreateProcedureWriteTraces, this.localDbConnection))
            {
                cmd.ExecuteNonQuery();
            }
        }
    }

    [TestClass]
    public class when_receiving_many_events_with_imperative_flush : given_empty_logging_database
    {
        private const int NumberOfEntries = 10000;
        private const string InstanceName = "instance name";

        protected SqlDatabaseSink sink;
        protected MockEventListener collectErrorsListener;

        protected override void Given()
        {
            base.Given();
            this.sink = new SqlDatabaseSink(InstanceName, this.GetSqlConnectionString(), SqlDatabaseLog.DefaultTableName, Buffering.DefaultBufferingInterval, NumberOfEntries, Buffering.DefaultMaxBufferSize, Timeout.InfiniteTimeSpan);
            this.collectErrorsListener = new MockEventListener();
            this.collectErrorsListener.EnableEvents(SemanticLoggingEventSource.Log, EventLevel.Error, Keywords.All);
        }

        protected override void OnCleanup()
        {
            using (this.sink) { }
            using (this.collectErrorsListener) { }
            base.OnCleanup();
        }

        [TestMethod]
        public void then_all_events_should_be_flushed()
        {
            for (int i = 0; i < NumberOfEntries; i++)
            {
                var entry =
                    EventEntryTestHelper.Create(
                        providerId: Guid.NewGuid(),
                        providerName: "TestName",
                        eventId: 50,
                        level: EventLevel.Verbose,
                        opcode: (EventOpcode)5,
                        task: (EventTask)6,
                        timestamp: DateTimeOffset.UtcNow,
                        version: 2,
                        formattedMessage: "Test" + i,
                        payloadNames: new string[] { "arg0" },
                        payload: new object[] { "Test" },
                        activityId: Guid.NewGuid(),
                        relatedActivityId: Guid.NewGuid());

                this.sink.OnNext(entry);
            }

            Thread.Sleep(50);

            this.sink.FlushAsync().Wait();

            int count;

            using (var cmd = new SqlCommand("SELECT COUNT(*) FROM Traces", this.localDbConnection))
            {
                count = (int)cmd.ExecuteScalar();
            }

            Assert.AreEqual<int>(NumberOfEntries, count);
        }

        [TestMethod]
        public void then_should_write_properties()
        {
            var entry = CreateValidEntry(
                processId: 300,
                threadId: 500);
            this.sink.OnNext(entry);

            this.sink.FlushAsync().Wait();

            using (var cmd = new SqlCommand("SELECT * FROM Traces", this.localDbConnection))
            {
                using (var reader = cmd.ExecuteReader())
                {
                    Assert.IsTrue(reader.Read());

                    Assert.AreEqual<Guid>(entry.ProviderId, (Guid)reader["ProviderId"]);
                    Assert.AreEqual<string>(entry.Schema.ProviderName, (string)reader["ProviderName"]);
                    Assert.AreEqual<int>(entry.EventId, (int)reader["EventId"]);
                    Assert.AreEqual<int>((int)entry.Schema.Level, (int)reader["Level"]);
                    Assert.AreEqual<int>((int)entry.Schema.Opcode, (int)reader["Opcode"]);
                    Assert.AreEqual<int>((int)entry.Schema.Task, (int)reader["Task"]);
                    Assert.AreEqual<DateTimeOffset>(entry.Timestamp, (DateTimeOffset)reader["Timestamp"]);
                    Assert.AreEqual<int>(entry.Schema.Version, (int)reader["Version"]);
                    Assert.AreEqual<string>(InstanceName, (string)reader["InstanceName"]);
                    Assert.AreEqual<string>(entry.FormattedMessage, (string)reader["FormattedMessage"]);
                    //Assert.AreEqual<string>(entry.Payload, (string)reader["Payload"]);
                    Assert.AreEqual<Guid>(Guid.Empty, (Guid)reader["ActivityId"]);
                    Assert.AreEqual<Guid>(Guid.Empty, (Guid)reader["RelatedActivityId"]);
                    Assert.AreEqual<int>(entry.ProcessId, (int)reader["ProcessId"]);
                    Assert.AreEqual<int>(entry.ThreadId, (int)reader["ThreadId"]);
                }
            }
        }

        [TestMethod]
        public void then_should_write_properties_with_activity_id()
        {
            var entry = CreateValidEntry(
                activityId: Guid.Parse("{D6A8536E-398F-4AD5-BB0A-3BFFD05EF5CB}"),
                relatedActivityId: Guid.Parse("{28ED52F1-1AB2-4B8C-9F30-4382BE2928AA}"));
            this.sink.OnNext(entry);

            this.sink.FlushAsync().Wait();

            using (var cmd = new SqlCommand("SELECT * FROM Traces", this.localDbConnection))
            {
                using (var reader = cmd.ExecuteReader())
                {
                    Assert.IsTrue(reader.Read());

                    Assert.AreEqual<Guid>(entry.ProviderId, (Guid)reader["ProviderId"]);
                    Assert.AreEqual<string>(entry.Schema.ProviderName, (string)reader["ProviderName"]);
                    Assert.AreEqual<int>(entry.EventId, (int)reader["EventId"]);
                    Assert.AreEqual<int>((int)entry.Schema.Level, (int)reader["Level"]);
                    Assert.AreEqual<int>((int)entry.Schema.Opcode, (int)reader["Opcode"]);
                    Assert.AreEqual<int>((int)entry.Schema.Task, (int)reader["Task"]);
                    Assert.AreEqual<DateTimeOffset>(entry.Timestamp, (DateTimeOffset)reader["Timestamp"]);
                    Assert.AreEqual<int>(entry.Schema.Version, (int)reader["Version"]);
                    Assert.AreEqual<string>(InstanceName, (string)reader["InstanceName"]);
                    Assert.AreEqual<string>(entry.FormattedMessage, (string)reader["FormattedMessage"]);
                    //Assert.AreEqual<string>(entry.Payload, (string)reader["Payload"]);
                    Assert.AreEqual<Guid>(entry.ActivityId, (Guid)reader["ActivityId"]);
                    Assert.AreEqual<Guid>(entry.RelatedActivityId, (Guid)reader["RelatedActivityId"]);
                }
            }
        }

        ////[TestMethod]
        ////public void then_should_not_log_cancelled_error()
        ////{
        ////    for (int i = 0; i < NumberOfEntries - 1; i++)
        ////    {
        ////        this.sink.OnNext(CreateValidEntry());
        ////    }

        ////    this.sink.FlushAsync();
        ////    Thread.Sleep(1000);
        ////    this.sink.Dispose();

        ////    Assert.IsFalse(collectErrorsListener.WrittenEntries.Any(x => x.EventId == 101));
        ////}

        [TestMethod]
        public void then_defaults_EventSourceName_property()
        {
            var entry = CreateValidEntry(providerName: null);

            sink.OnNext(entry);
            sink.FlushAsync().Wait(TimeSpan.FromSeconds(5));

            using (var cmd = new SqlCommand("SELECT [ProviderName] FROM Traces", this.localDbConnection))
            {
                Assert.AreEqual(string.Empty, (string)cmd.ExecuteScalar());
            }
        }

        [TestMethod]
        public void then_should_truncate_longer_properties()
        {
            var entry = CreateValidEntry(
                providerName: new string('a', 5000));
            //entry.InstanceName = new string('b', 5000);

            sink.OnNext(entry);
            sink.FlushAsync().Wait(TimeSpan.FromSeconds(5));

            using (var cmd = new SqlCommand("SELECT * FROM Traces", this.localDbConnection))
            {
                using (var reader = cmd.ExecuteReader())
                {
                    Assert.IsTrue(reader.Read());

                    Assert.AreEqual<string>(entry.Schema.ProviderName.Substring(0, 500), (string)reader["ProviderName"]);
                    //Assert.AreEqual<string>(entry.InstanceName.Substring(0, 1000), (string)reader["InstanceName"]);
                }
            }
        }

        private static EventEntry CreateValidEntry(string providerName = "TestName", int processId = 0, int threadId = 0, Guid activityId = default(Guid), Guid relatedActivityId = default (Guid))
        {
            var record =
                EventEntryTestHelper.Create(
                    providerId: Guid.NewGuid(),
                    providerName: providerName,
                    eventId: 50,
                    level: EventLevel.Verbose,
                    opcode: (EventOpcode)5,
                    task: (EventTask)6,
                    timestamp: DateTimeOffset.UtcNow,
                    version: 2,
                    formattedMessage: "Formatted message",
                    payloadNames: new string[] { "arg0" },
                    payload: new object[] { "Test" },
                    processId: processId,
                    threadId: threadId,
                    activityId: activityId,
                    relatedActivityId: relatedActivityId);

            return record;
        }
    }

    [TestClass]
    public class when_using_small_buffering_count : given_empty_logging_database
    {
        private const int BufferingCount = 5;

        protected SqlDatabaseSink sink;

        protected override void Given()
        {
            base.Given();
            this.sink = new SqlDatabaseSink(
                "TestInstanceName",
                this.GetSqlConnectionString(),
                SqlDatabaseLog.DefaultTableName,
                TimeSpan.FromMinutes(1),
                BufferingCount,
                Buffering.DefaultMaxBufferSize,
                Timeout.InfiniteTimeSpan);
        }

        protected override void OnCleanup()
        {
            this.sink.Dispose();
            base.OnCleanup();
        }

        [TestMethod]
        public void then_writing_more_events_should_flush_only_the_batch_size()
        {
            for (int i = 0; i < BufferingCount + 2; i++)
            {
                var entry =
                    EventEntryTestHelper.Create(
                        providerId: Guid.NewGuid(),
                        providerName: "TestName",
                        eventId: 50,
                        level: EventLevel.Verbose,
                        opcode: (EventOpcode)5,
                        task: (EventTask)6,
                        timestamp: DateTimeOffset.UtcNow,
                        version: 2,
                        formattedMessage: "Test" + i,
                        payloadNames: new string[] { "arg0" },
                        payload: new object[] { "Test" });

                this.sink.OnNext(entry);
                Thread.Sleep(10);
            }

            int count = PollingHelper.WaitUntil(() =>
                {
                    using (var cmd = new SqlCommand("SELECT COUNT(*) FROM Traces", this.localDbConnection))
                    {
                        return (int)cmd.ExecuteScalar();
                    }
                },
                c => c > 0,
                TimeSpan.FromSeconds(30));

            this.sink.Dispose();

            Assert.AreEqual(BufferingCount, count);
        }

        //[TestClass]
        //public class when_single_invalid_record_in_batch : given_empty_logging_database
        //{
        //    private const int BufferingCount = 129;

        //    protected SqlDatabaseSink sink;

        //    protected override void Given()
        //    {
        //        base.Given();
        //        this.sink = new SqlDatabaseSink(
        //            "TestInstanceName",
        //            this.GetSqlConnectionString(),
        //            SqlDatabaseLog.DefaultTableName,
        //            Timeout.InfiniteTimeSpan,
        //            BufferingCount,
        //            Buffering.DefaultMaxBufferSize,
        //            Timeout.InfiniteTimeSpan);
        //    }

        //    protected override void When()
        //    {
        //        // how do we create an invalid entry??
        //        var invalid = new EventRecord();
        //        this.sink.OnNext(invalid);

        //        for (int i = 1; i < BufferingCount; i++)
        //        {
        //            var entry = new EventRecord
        //            {
        //                ProviderId = Guid.NewGuid(),
        //                ProviderName = "TestName",
        //                EventId = 50,
        //                Level = "Verbose",
        //                Opcode = 5,
        //                Task = 6,
        //                Timestamp = DateTimeOffset.UtcNow,
        //                Version = 2,
        //                InstanceName = "Custom instance name",
        //                FormattedMessage = "Test" + i,
        //                Payload = "{arg0:Test}"
        //            };

        //            this.sink.OnNext(entry);
        //        }
        //    }

        //    protected override void OnCleanup()
        //    {
        //        this.sink.Dispose();
        //        base.OnCleanup();
        //    }

        //    [TestMethod]
        //    public void then_all_valid_records_should_flush()
        //    {
        //        int count = PollingHelper.WaitUntil(() =>
        //        {
        //            using (var cmd = new SqlCommand("SELECT COUNT(*) FROM Traces", this.LocalDbConnection))
        //            {
        //                return (int)cmd.ExecuteScalar();
        //            }
        //        }, c => c > 0, TimeSpan.FromSeconds(5));

        //        Assert.AreEqual(BufferingCount - 1, count);
        //    }
        //}

        //[TestClass]
        //public class when_multiple_invalid_record_in_batch : given_empty_logging_database
        //{
        //    private const int BufferingCount = 129;
        //    protected SqlDatabaseSink sink;
        //    protected InMemoryEventListener listener = new InMemoryEventListener();

        //    protected override void Given()
        //    {
        //        base.Given();
        //        this.sink = new SqlDatabaseSink(
        //            "TestInstanceName",
        //            this.GetSqlConnectionString(),
        //            SqlDatabaseLog.DefaultTableName,
        //            Timeout.InfiniteTimeSpan,
        //            BufferingCount,
        //            Buffering.DefaultMaxBufferSize,
        //            Timeout.InfiniteTimeSpan);

        //        this.listener.EnableEvents(SemanticLoggingEventSource.Log, EventLevel.Error, Keywords.All);
        //    }

        //    protected override void When()
        //    {
        //        for (int i = 0; i < 4; i++)
        //        {
        //            // how do we create an invalid entry??
        //            var invalid = new EventRecord();
        //            this.sink.OnNext(invalid);
        //        }

        //        for (int i = 4; i < BufferingCount; i++)
        //        {
        //            var entry = new EventRecord
        //            {
        //                ProviderId = Guid.NewGuid(),
        //                ProviderName = "TestName",
        //                EventId = 50,
        //                Level = (int)EventLevel.Verbose,
        //                Opcode = 5,
        //                Task = 6,
        //                Timestamp = DateTimeOffset.UtcNow,
        //                Version = 2,
        //                InstanceName = "Custom instance name",
        //                FormattedMessage = "Test" + i,
        //                Payload = "{arg0:Test}"
        //            };

        //            this.sink.OnNext(entry);
        //        }
        //    }

        //    protected override void OnCleanup()
        //    {
        //        this.listener.DisableEvents(SemanticLoggingEventSource.Log);
        //        this.listener.Dispose();
        //        this.sink.Dispose();
        //        base.OnCleanup();
        //    }

        //    [TestMethod]
        //    public void then_no_records_should_flush()
        //    {
        //        this.listener.WaitSignalCondition = () => this.listener.EventWrittenCount == 4;
        //        bool signaled = this.listener.WaitOnAsyncEvents.WaitOne(3000);

        //        Assert.IsTrue(signaled);
        //    }
        //}
    }
}