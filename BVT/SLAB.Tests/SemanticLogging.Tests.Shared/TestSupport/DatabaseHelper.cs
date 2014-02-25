// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.Shared.TestSupport
{
    public static class DatabaseHelper
    {
        public static void CleanLoggingDB(string databaseConnectionString)
        {
            using (var connection = new SqlConnection(databaseConnectionString))
            {
                connection.Open();
                SqlCommand command = new SqlCommand("DELETE TRACES", connection);
                command.ExecuteNonQuery();
            }
        }

        public static DataTable GetLoggedTable(string databaseConnectionString)
        {
            return GetLoggedTable(databaseConnectionString, 1);
        }

        public static DataTable GetLoggedTable(string databaseConnectionString, int recordCount)
        {
            using (var connection = new SqlConnection(databaseConnectionString))
            {
                DataSet dataset = new DataSet();
                DataTable datatable = new DataTable();
                connection.Open();
                SqlDataAdapter adapter = new SqlDataAdapter("SELECT * FROM TRACES", connection);

                for (int n = 0; n < 20; n++)
                {
                    dataset = new DataSet();
                    adapter.Fill(dataset);
                    datatable = dataset.Tables[0];
                    if (datatable.Rows.Count >= recordCount)
                    {
                        break; 
                    }

                    Task.Delay(800).Wait();
                }

                return datatable;
            }
        }

        public static DataTable PollUntilEventsAreWritten(string databaseConnectionString, int eventsCount)
        {
            var timeoutToWaitUntilEventIsReceived = DateTime.UtcNow.AddSeconds(10);
            var datatable = new DataTable();
            while (DateTime.UtcNow < timeoutToWaitUntilEventIsReceived)
            {
                try
                {
                    using (var connection = new SqlConnection(databaseConnectionString))
                    {
                        var dataset = new DataSet();
                        connection.Open();
                        var adapter = new SqlDataAdapter("SELECT * FROM TRACES", connection);
                        dataset = new DataSet();
                        adapter.Fill(dataset);
                        datatable = dataset.Tables[0];
                        if (datatable.Rows.Count >= eventsCount)
                        {
                            break;
                        }
                    }
                }
                catch 
                { }

                Task.Delay(200).Wait();
            }

            return datatable;
        }

        public static void DropLoggingTable(string databaseConnectionString)
        {
            using (var connection = new SqlConnection(databaseConnectionString))
            {
                connection.Open();
                SqlCommand command = new SqlCommand("DROP TABLE [dbo].[Traces]", connection);
                command.ExecuteNonQuery();
            }
        }

        public static void CreateLoggingTable(string databaseConnectionString)
        {
            using (var connection = new SqlConnection(databaseConnectionString))
            {
                connection.Open();
                SqlCommand command = new SqlCommand(@"CREATE TABLE [dbo].[Traces](
	[id] [bigint] IDENTITY(1,1) NOT NULL,
	[InstanceName] [nvarchar](1000) NOT NULL,
	[ProviderId] [uniqueidentifier] NOT NULL,
	[ProviderName] [nvarchar](500) NOT NULL,
	[EventId] [int] NOT NULL,
	[EventKeywords] [bigint] NOT NULL,
	[Level] [int] NOT NULL,
	[Opcode] [int] NOT NULL,
	[Task] [int] NOT NULL,
	[Timestamp] [datetimeoffset](7) NOT NULL,
	[Version] [int] NOT NULL,
	[FormattedMessage] [nvarchar](4000) NULL,
	[Payload] [nvarchar](4000) NULL,
	[ActivityId] [uniqueidentifier],
	[RelatedActivityId] [uniqueidentifier],
 CONSTRAINT [PK_Traces] PRIMARY KEY CLUSTERED 
(
	[id] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF)
)", connection);
                command.ExecuteNonQuery();
            }
        }

        public static int GetRowCount(string databaseConnectionString)
        {
            using (var connection = new SqlConnection(databaseConnectionString))
            {
                connection.Open();
                SqlCommand command = new SqlCommand("SELECT count(*) FROM TRACES", connection);
                return (int)command.ExecuteScalar();
            }
        }
    }
}
