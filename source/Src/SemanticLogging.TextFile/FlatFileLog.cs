// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Diagnostics.Tracing;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Formatters;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Sinks;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Utility;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging
{
    /// <summary>
    /// Factories and helpers for using the <see cref="FlatFileSink"/>.
    /// </summary>
    public static class FlatFileLog
    {
        /// <summary>
        /// Subscribes to an <see cref="IObservable{EventEntry}"/> using a <see cref="FlatFileSink"/>.
        /// </summary>
        /// <param name="eventStream">The event stream. Typically this is an instance of <see cref="ObservableEventListener"/>.</param>
        /// <param name="fileName">Name of the file.</param>
        /// <param name="formatter">The formatter.</param>
        /// <param name="isAsync">Specifies if the writing should be done asynchronously, or synchronously with a blocking call.</param>
        /// <returns>A subscription to the sink that can be disposed to unsubscribe the sink and dispose it, or to get access to the sink instance.</returns>
        public static SinkSubscription<FlatFileSink> LogToFlatFile(this IObservable<EventEntry> eventStream, string fileName = null, IEventTextFormatter formatter = null, bool isAsync = false)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                fileName = FileUtil.CreateRandomFileName();
            }

            var sink = new FlatFileSink(fileName, formatter ?? new EventTextFormatter(), isAsync);

            var subscription = eventStream.Subscribe(sink);

            return new SinkSubscription<FlatFileSink>(subscription, sink);
        }

        /// <summary>
        /// Creates an event listener that logs using a <see cref="FlatFileSink"/>.
        /// </summary>
        /// <param name="fileName">Name of the file.</param>
        /// <param name="formatter">The formatter.</param>
        /// <param name="isAsync">Specifies if the writing should be done asynchronously, or synchronously with a blocking call.</param>
        /// <returns>An event listener that uses <see cref="FlatFileSink"/> to log events.</returns>
        public static EventListener CreateListener(string fileName = null, IEventTextFormatter formatter = null, bool isAsync = false)
        {
            var listener = new ObservableEventListener();
            listener.LogToFlatFile(fileName, formatter, isAsync);
            return listener;
        }
    }
}
