// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Data;
using System.Linq;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Observable;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Sinks.Database;
using Microsoft.SqlServer.Server;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Utility
{
    /// <summary>
    /// Extensions for <see cref="EventEntry"/>.
    /// </summary>
    internal static class EventRecordExtensions
    {
        internal static readonly SqlMetaData[] SqlMetaData;
        internal static readonly string[] Fields;

        static EventRecordExtensions()
        {
            SqlMetaData = new SqlMetaData[]
            {
                new SqlMetaData("InstanceName", SqlDbType.NVarChar, 1000),
                new SqlMetaData("ProviderId", SqlDbType.UniqueIdentifier),
                new SqlMetaData("ProviderName", SqlDbType.NVarChar, 500),
                new SqlMetaData("EventId", SqlDbType.Int),
                new SqlMetaData("EventKeywords", SqlDbType.BigInt),
                new SqlMetaData("Level", SqlDbType.Int),
                new SqlMetaData("Opcode", SqlDbType.Int),
                new SqlMetaData("Task", SqlDbType.Int),
                new SqlMetaData("Timestamp", SqlDbType.DateTimeOffset),
                new SqlMetaData("Version", SqlDbType.Int),
                new SqlMetaData("FormattedMessage", SqlDbType.NVarChar, 4000),
                new SqlMetaData("Payload", SqlDbType.NVarChar, 4000)
            };

            Fields = SqlMetaData.Select(x => x.Name).ToArray();
        }

        /// <summary>
        /// Subscribes an <see cref="IObserver{EventRecord}"/> sink by doing a straight projection of a sequence of <see cref="EventEntry"/>
        /// and converting it to a <see cref="EventRecord"/> entity.
        /// </summary>
        /// <param name="source">The original stream of events.</param>
        /// <param name="sink">The underlying sink.</param>
        /// <returns>A subscription token to unsubscribe to the event stream.</returns>
        /// <remarks>When using Reactive Extensions (Rx), this is equivalent to doing a Select statement on the <paramref name="source"/> to convert it to <see cref="IObservable{String}"/> and then
        /// calling Subscribe on it.
        /// </remarks>
        public static IDisposable SubscribeWithConversion(this IObservable<EventEntry> source, IObserver<EventRecord> sink)
        {
            return source.CreateSubscription(sink, TryConvertToEventRecord);
        }

        /// <summary>
        /// Converts an <see cref="EventEntry"/> to a <see cref="EventRecord"/>.
        /// </summary>
        /// <param name="entry">The entry to convert.</param>
        /// <returns>A converted entry, or <see langword="null"/> if the payload is invalid.</returns>
        public static EventRecord TryConvertToEventRecord(this EventEntry entry)
        {
            var entity = new EventRecord()
            {
                ProviderId = entry.ProviderId,
                ProviderName = entry.Schema.ProviderName,
                EventId = entry.EventId,
                EventKeywords = (long)entry.Schema.Keywords,
                Level = (int)entry.Schema.Level,
                Opcode = (int)entry.Schema.Opcode,
                Task = (int)entry.Schema.Task,
                Timestamp = entry.Timestamp,
                Version = entry.Schema.Version,
                FormattedMessage = entry.FormattedMessage,
                Payload = EventEntryUtil.JsonSerializePayload(entry)
            };

            return entity;
        }

        internal static SqlDataRecord ToSqlDataRecord(this EventRecord record)
        {
            var sqlDataRecord = new SqlDataRecord(SqlMetaData);

            sqlDataRecord.SetValue(0, record.InstanceName ?? string.Empty);
            sqlDataRecord.SetValue(1, record.ProviderId);
            sqlDataRecord.SetValue(2, record.ProviderName ?? string.Empty);
            sqlDataRecord.SetValue(3, record.EventId);
            sqlDataRecord.SetValue(4, record.EventKeywords);
            sqlDataRecord.SetValue(5, record.Level);
            sqlDataRecord.SetValue(6, record.Opcode);
            sqlDataRecord.SetValue(7, record.Task);
            sqlDataRecord.SetValue(8, record.Timestamp);
            sqlDataRecord.SetValue(9, record.Version);
            sqlDataRecord.SetValue(10, (object)record.FormattedMessage ?? DBNull.Value);
            sqlDataRecord.SetValue(11, (object)record.Payload ?? DBNull.Value);

            return sqlDataRecord;
        }
    }
}
