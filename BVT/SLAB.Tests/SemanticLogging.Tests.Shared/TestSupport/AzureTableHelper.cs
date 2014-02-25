// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.Shared.TestSupport
{
    public static class AzureTableHelper
    {
        public static void DeleteTable(string connectionString, string tableName)
        {
            var table = GetTable(connectionString, tableName);
            table.DeleteIfExists();
        }

        public static IEnumerable<WindowsAzureTableEventEntry> PollForEvents(string connectionString, string tableName, int eventsToRecieve)
        {
            return PollForEvents(connectionString, tableName, eventsToRecieve, TimeSpan.FromSeconds(10));
        }

        public static IEnumerable<WindowsAzureTableEventEntry> PollForEvents(string connectionString, string tableName, int eventsToRecieve, TimeSpan waitFor)
        {
            IEnumerable<WindowsAzureTableEventEntry> entries = new WindowsAzureTableEventEntry[0];
            var timeoutToWaitUntilEventIsReceived = DateTime.UtcNow.Add(waitFor);
            var table = GetTable(connectionString, tableName);
            var query = new TableQuery<WindowsAzureTableEventEntry>();
            while (DateTime.UtcNow < timeoutToWaitUntilEventIsReceived)
            {
                try
                {
                    entries = table.ExecuteQuery<WindowsAzureTableEventEntry>(query);
                    if (entries.Count() >= eventsToRecieve)
                    {
                        break;
                    }
                }
                catch
                { }

                Task.Delay(200).Wait();
            }

            return entries;
        }

        //public static IEnumerable<WindowsAzureTableEventEntry> GetEvents(string connectionString, string tableName)
        //{
        //    var table = GetTable(connectionString, tableName);
        //    var query = new TableQuery<WindowsAzureTableEventEntry>();
        //    return table.ExecuteQuery<WindowsAzureTableEventEntry>(query).ToList();
        //}

        public static int GetEventsCount(string connectionString, string tableName)
        {
            var table = GetTable(connectionString, tableName);
            if (!table.Exists())
            {
                return 0;
            }

            var query = new TableQuery<WindowsAzureTableEventEntry>();
            return table.ExecuteQuery<WindowsAzureTableEventEntry>(query.Select(new List<string>() { "PartitionKey", "RowKey", "EventId" })).Count();
        }

        private static CloudTable GetTable(string connectionString, string tableName)
        {
            var account = CloudStorageAccount.Parse(connectionString);
            var client = account.CreateCloudTableClient();

            return client.GetTableReference(tableName);
        }
    }
}
