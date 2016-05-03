// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Diagnostics.Tracing;
using System.Threading;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Sinks;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Utility;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging
{
    /// <summary>
    /// Factories and helpers for using the <see cref="SqlDatabaseSink"/>.
    /// </summary>
    public static class SqlDatabaseLog
    {
        /// <summary>
        /// Default table name used to write traces.
        /// </summary>
        public const string DefaultTableName = "Traces";

        /// <summary>
        /// Default stored procedure name used to write traces.
        /// </summary>
        public const string DefaultStoredProcedureName = "dbo.WriteTraces";

        /// <summary>
        /// Subscribes to an <see cref="IObservable{EventEntry}"/> using a <see cref="SqlDatabaseSink"/>.
        /// </summary>
        /// <param name="eventStream">The event stream. Typically this is an instance of <see cref="ObservableEventListener" />.</param>
        /// <param name="instanceName">The name of the instance originating the entries.</param>
        /// <param name="connectionString">The connection string.</param>
        /// <param name="tableName">The name of the table.</param>
        /// <param name="storedProcedureName">The name of the stored procedure that writes to table></param>
        /// <param name="bufferingInterval">The buffering interval between each batch publishing. Default value is <see cref="Buffering.DefaultBufferingInterval"/>.</param>
        /// <param name="bufferingCount">The number of entries that will trigger a batch publishing.</param>
        /// <param name="onCompletedTimeout">Defines a timeout interval for when flushing the entries after an <see cref="SqlDatabaseSink.OnCompleted"/> call is received and before disposing the sink.
        /// This means that if the timeout period elapses, some event entries will be dropped and not sent to the store. Normally, calling <see cref="IDisposable.Dispose"/> on 
        /// the <see cref="System.Diagnostics.Tracing.EventListener"/> will block until all the entries are flushed or the interval elapses.
        /// If <see langword="null"/> is specified, then the call will block indefinitely until the flush operation finishes.</param>
        /// <param name="maxBufferSize">The maximum number of entries that can be buffered while it's sending to SQL Database before the sink starts dropping entries.
        /// This means that if the timeout period elapses, some event entries will be dropped and not sent to the store. Normally, calling <see cref="IDisposable.Dispose" /> on
        /// the <see cref="System.Diagnostics.Tracing.EventListener" /> will block until all the entries are flushed or the interval elapses.
        /// If <see langword="null" /> is specified, then the call will block indefinitely until the flush operation finishes.</param>
        /// <returns>A subscription to the sink that can be disposed to unsubscribe the sink and dispose it, or to get access to the sink instance.</returns>
        public static SinkSubscription<SqlDatabaseSink> LogToSqlDatabase(this IObservable<EventEntry> eventStream, string instanceName, string connectionString, string tableName = DefaultTableName, string storedProcedureName = DefaultStoredProcedureName, TimeSpan? bufferingInterval = null, int bufferingCount = Buffering.DefaultBufferingCount, TimeSpan? onCompletedTimeout = null, int maxBufferSize = Buffering.DefaultMaxBufferSize)
        {
            var sink = new SqlDatabaseSink(
                instanceName,
                connectionString,
                tableName,
                storedProcedureName,
                bufferingInterval ?? Buffering.DefaultBufferingInterval,
                bufferingCount,
                maxBufferSize,
                onCompletedTimeout ?? Timeout.InfiniteTimeSpan);

            var subscription = eventStream.Subscribe(sink);

            return new SinkSubscription<SqlDatabaseSink>(subscription, sink);
        }

        /// <summary>
        /// Subscribes to the listener using a <see cref="SqlDatabaseSink" />.
        /// </summary>
        /// <param name="instanceName">The name of the instance originating the entries.</param>
        /// <param name="connectionString">The connection string.</param>
        /// <param name="tableName">The name of the table.</param>
        /// <param name="storedProcedureName">The name of the stored procedure that writes to table></param>
        /// <param name="bufferingInterval">The buffering interval between each batch publishing.</param>
        /// <param name="bufferingCount">The number of entries that will trigger a batch publishing.</param>
        /// <param name="listenerDisposeTimeout">Defines a timeout interval for the flush operation when the listener is disposed.
        /// This means that if the timeout period elapses, some event entries will be dropped and not sent to the store. Calling <see cref="IDisposable.Dispose"/> on 
        /// the <see cref="EventListener"/> will block until all the entries are flushed or the interval elapses.
        /// If <see langword="null"/> is specified, then the call will block indefinitely until the flush operation finishes.</param>
        /// <param name="maxBufferSize">The maximum number of entries that can be buffered while it's sending to SQL Database before the sink starts dropping entries.
        /// This means that if the timeout period elapses, some event entries will be dropped and not sent to the store. Normally, calling <see cref="IDisposable.Dispose" /> on
        /// the <see cref="System.Diagnostics.Tracing.EventListener" /> will block until all the entries are flushed or the interval elapses.
        /// If <see langword="null" /> is specified, then the call will block indefinitely until the flush operation finishes.</param>
        /// <returns>An event listener that uses <see cref="SqlDatabaseSink"/> to log events.</returns>
        public static EventListener CreateListener(string instanceName, string connectionString, string tableName = DefaultTableName, string storedProcedureName = DefaultStoredProcedureName, TimeSpan? bufferingInterval = null, int bufferingCount = Buffering.DefaultBufferingCount, TimeSpan? listenerDisposeTimeout = null, int maxBufferSize = Buffering.DefaultMaxBufferSize)
        {
            var listener = new ObservableEventListener();
            listener.LogToSqlDatabase(instanceName, connectionString, tableName, storedProcedureName, bufferingInterval, bufferingCount, listenerDisposeTimeout, maxBufferSize);
            return listener;
        }
    }
}
