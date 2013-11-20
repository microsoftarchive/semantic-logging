// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Diagnostics.Tracing;
using System.Threading;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Sinks;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Utility;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging
{
    /// <summary>
    /// Factories and helpers for using the <see cref="WindowsAzureTableSink"/>.
    /// </summary>
    public static class WindowsAzureTableLog
    {
        /// <summary>
        /// The default table name where logs are written to.
        /// </summary>
        public const string DefaultTableName = "SLABLogsTable";

        /// <summary>
        /// Subscribes to an <see cref="IObservable{EventEntry}" /> using a <see cref="WindowsAzureTableSink" />.
        /// </summary>
        /// <param name="eventStream">The event stream. Typically this is an instance of <see cref="ObservableEventListener" />.</param>
        /// <param name="instanceName">The name of the instance originating the entries.</param>
        /// <param name="connectionString">The connection string for the storage account.</param>
        /// <param name="tableAddress">Either the name of the table, or the absolute URI to the table.</param>
        /// <param name="bufferingInterval">The buffering interval between each batch publishing. Default value is <see cref="Buffering.DefaultBufferingInterval" />.</param>
        /// <param name="sortKeysAscending">The value indicating whether to sort the row keys in ascending order.</param>
        /// <param name="onCompletedTimeout">Defines a timeout interval for when flushing the entries after an <see cref="WindowsAzureTableSink.OnCompleted" /> call is received and before disposing the sink.</param>
        /// <param name="maxBufferSize">The maximum number of entries that can be buffered while it's sending to Windows Azure Storage before the sink starts dropping entries.
        /// This means that if the timeout period elapses, some event entries will be dropped and not sent to the store. Normally, calling <see cref="IDisposable.Dispose" /> on
        /// the <see cref="System.Diagnostics.Tracing.EventListener" /> will block until all the entries are flushed or the interval elapses.
        /// If <see langword="null" /> is specified, then the call will block indefinitely until the flush operation finishes.</param>
        /// <returns>
        /// A subscription to the sink that can be disposed to unsubscribe the sink and dispose it, or to get access to the sink instance.
        /// </returns>
        public static SinkSubscription<WindowsAzureTableSink> LogToWindowsAzureTable(this IObservable<EventEntry> eventStream, string instanceName, string connectionString, string tableAddress = DefaultTableName, TimeSpan? bufferingInterval = null, bool sortKeysAscending = false, TimeSpan? onCompletedTimeout = null, int maxBufferSize = Buffering.DefaultMaxBufferSize)
        {
            var sink = new WindowsAzureTableSink(
                instanceName,
                connectionString,
                tableAddress,
                bufferingInterval ?? Buffering.DefaultBufferingInterval,
                maxBufferSize,
                onCompletedTimeout ?? Timeout.InfiniteTimeSpan)
            {
                SortKeysAscending = sortKeysAscending
            };

            var subscription = eventStream.SubscribeWithConversion(sink);
            return new SinkSubscription<WindowsAzureTableSink>(subscription, sink);
        }

        /// <summary>
        /// Creates an event listener that logs using a <see cref="WindowsAzureTableSink" />.
        /// </summary>
        /// <param name="instanceName">The name of the instance originating the entries.</param>
        /// <param name="connectionString">The connection string for the storage account.</param>
        /// <param name="tableAddress">Either the name of the table, or the absolute URI to the table.</param>
        /// <param name="bufferingInterval">The buffering interval between each batch publishing.</param>
        /// <param name="sortKeysAscending">The value indicating whether to sort the row keys in ascending order.</param>
        /// <param name="listenerDisposeTimeout">Defines a timeout interval for the flush operation when the listener is disposed.</param>
        /// <param name="maxBufferSize">The maximum number of entries that can be buffered while it's sending to Windows Azure Storage before the sink starts dropping entries.
        /// This means that if the timeout period elapses, some event entries will be dropped and not sent to the store. Calling <see cref="IDisposable.Dispose" /> on
        /// the <see cref="EventListener" /> will block until all the entries are flushed or the interval elapses.
        /// If <see langword="null" /> is specified, then the call will block indefinitely until the flush operation finishes.</param>
        /// <returns>
        /// An event listener that uses <see cref="WindowsAzureTableSink" /> to log events.
        /// </returns>
        public static EventListener CreateListener(string instanceName, string connectionString, string tableAddress = DefaultTableName, TimeSpan? bufferingInterval = null, bool sortKeysAscending = false, TimeSpan? listenerDisposeTimeout = null, int maxBufferSize = Buffering.DefaultMaxBufferSize)
        {
            var listener = new ObservableEventListener();
            listener.LogToWindowsAzureTable(instanceName, connectionString, tableAddress, bufferingInterval, sortKeysAscending, listenerDisposeTimeout, maxBufferSize);
            return listener;
        }
    }
}
