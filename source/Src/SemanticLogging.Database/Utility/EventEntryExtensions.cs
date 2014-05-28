// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Data;
using System.Linq;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Utility;
using Microsoft.SqlServer.Server;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Database.Utility
{
    /// <summary>
    /// Extensions for <see cref="EventEntry"/>.
    /// </summary>
    internal static class EventEntryExtensions
    {
        internal static readonly SqlMetaData[] SqlMetaData;
        internal static readonly string[] Fields;

        static EventEntryExtensions()
        {
            SqlMetaData = new[]
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
                new SqlMetaData("Payload", SqlDbType.NVarChar, 4000),
                new SqlMetaData("ActivityId", SqlDbType.UniqueIdentifier),
                new SqlMetaData("RelatedActivityId", SqlDbType.UniqueIdentifier),
                new SqlMetaData("ProcessId", SqlDbType.Int),
                new SqlMetaData("ThreadId", SqlDbType.Int)
            };

            Fields = SqlMetaData.Select(x => x.Name).ToArray();
        }

        internal static SqlDataRecord ToSqlDataRecord(this EventEntry record, string instanceName)
        {
            var sqlDataRecord = new SqlDataRecord(SqlMetaData);

            sqlDataRecord.SetValue(0, instanceName ?? string.Empty);
            sqlDataRecord.SetValue(1, record.ProviderId);
            sqlDataRecord.SetValue(2, record.Schema.ProviderName ?? string.Empty);
            sqlDataRecord.SetValue(3, record.EventId);
            sqlDataRecord.SetValue(4, (long)record.Schema.Keywords);
            sqlDataRecord.SetValue(5, (int)record.Schema.Level);
            sqlDataRecord.SetValue(6, (int)record.Schema.Opcode);
            sqlDataRecord.SetValue(7, (int)record.Schema.Task);
            sqlDataRecord.SetValue(8, record.Timestamp);
            sqlDataRecord.SetValue(9, record.Schema.Version);
            sqlDataRecord.SetValue(10, (object)record.FormattedMessage ?? DBNull.Value);
            sqlDataRecord.SetValue(11, (object)EventEntryUtil.JsonSerializePayload(record) ?? DBNull.Value);
            sqlDataRecord.SetValue(12, record.ActivityId);
            sqlDataRecord.SetValue(13, record.RelatedActivityId);
            sqlDataRecord.SetValue(14, record.ProcessId);
            sqlDataRecord.SetValue(15, record.ThreadId);

            return sqlDataRecord;
        }
    }
}
