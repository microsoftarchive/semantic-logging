// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Observable;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Sinks;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.Shared.TestSupport;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Utility;
using System;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.Shared.TestObjects
{
    public static class IObservableEventEntryExtensions
    {
        /// <summary>
        /// Subscribes to the listener using a <see cref="SqlDatabaseSink" />.
        /// </summary>
        /// <param name="eventStream">The event stream. Typically this is an instance of <see cref="ObservableEventListener" />.</param>
        /// <param name="instanceName">The name of the instance originating the entries.</param>
        /// <param name="connectionString">The connection string.</param>
        /// <param name="tableName">The name of the table.</param>
        /// <param name="bufferingInterval">The buffering interval between each batch publishing.</param>
        /// <param name="bufferingCount">The number of entries that will trigger a batch publishing.</param>
        /// <returns>The sink instance.</returns>
        public static SinkSubscription<CustomSqlSink> LogToCustomSqlDatabase(this IObservable<EventEntry> eventStream, string instanceName, string connectionString, string tableName = "Traces", TimeSpan? bufferingInterval = null, int bufferingCount = Buffering.DefaultBufferingCount)
        {
            var sink = new CustomSqlSink(instanceName, connectionString, tableName, bufferingInterval ?? Buffering.DefaultBufferingInterval, bufferingCount);

            var subscription = eventStream.SubscribeWithConversion(sink);

            return new SinkSubscription<CustomSqlSink>(subscription, sink);
        }

        public static SinkSubscription<MockFlatFileSink> LogToMockFlatFile(this IObservable<EventEntry> eventStream, string fileName, string header)
        {
            var sink = new MockFlatFileSink(fileName, header);

            var subscription = eventStream.SubscribeWithConversion(sink);

            return new SinkSubscription<MockFlatFileSink>(subscription, sink);
        }

        public static IDisposable SubscribeWithConversion(this IObservable<EventEntry> source, IObserver<EventEntry> sink)
        {
            return source.CreateSubscription(sink, PassEventEntry);
        }

        public static EventEntry PassEventEntry(this EventEntry entry)
        {
            return entry;
        }
    }
}
