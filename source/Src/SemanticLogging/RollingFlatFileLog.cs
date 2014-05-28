// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Diagnostics.Tracing;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Formatters;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Sinks;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Utility;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging
{
    /// <summary>
    /// Factories and helpers for using the <see cref="RollingFlatFileSink"/>.
    /// </summary>
    public static class RollingFlatFileLog
    {
        /// <summary>
        /// Subscribes to an <see cref="IObservable{EventEntry}"/> using a <see cref="RollingFlatFileSink"/>.
        /// </summary>
        /// <param name="eventStream">The event stream. Typically this is an instance of <see cref="ObservableEventListener"/>.</param>
        /// <param name="fileName">The filename where the entries will be logged.</param>
        /// <param name="rollSizeKB">The maximum file size (KB) before rolling.</param>
        /// <param name="timestampPattern">The date format that will be appended to the new roll file.</param>
        /// <param name="rollFileExistsBehavior">Expected behavior that will be used when the roll file has to be created.</param>
        /// <param name="rollInterval">The time interval that makes the file to be rolled.</param>
        /// <param name="formatter">The formatter.</param>
        /// <param name="maxArchivedFiles">The maximum number of archived files to keep.</param>
        /// <param name="isAsync">Specifies if the writing should be done asynchronously, or synchronously with a blocking call.</param>
        /// <returns>A subscription to the sink that can be disposed to unsubscribe the sink and dispose it, or to get access to the sink instance.</returns>
        public static SinkSubscription<RollingFlatFileSink> LogToRollingFlatFile(this IObservable<EventEntry> eventStream, string fileName, int rollSizeKB, string timestampPattern, RollFileExistsBehavior rollFileExistsBehavior, RollInterval rollInterval, IEventTextFormatter formatter = null, int maxArchivedFiles = 0, bool isAsync = false)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                fileName = FileUtil.CreateRandomFileName();
            }

            var sink = new RollingFlatFileSink(fileName, rollSizeKB, timestampPattern, rollFileExistsBehavior, rollInterval, maxArchivedFiles, formatter ?? new EventTextFormatter(), isAsync);

            var subscription = eventStream.Subscribe(sink);

            return new SinkSubscription<RollingFlatFileSink>(subscription, sink);
        }

        /// <summary>
        /// Creates an event listener that logs using a <see cref="RollingFlatFileSink"/>.
        /// </summary>
        /// <param name="fileName">The filename where the entries will be logged.</param>
        /// <param name="rollSizeKB">The maximum file size (KB) before rolling.</param>
        /// <param name="timestampPattern">The date format that will be appended to the new roll file.</param>
        /// <param name="rollFileExistsBehavior">Expected behavior that will be used when the roll file has to be created.</param>
        /// <param name="rollInterval">The time interval that makes the file to be rolled.</param>
        /// <param name="formatter">The formatter.</param>
        /// <param name="maxArchivedFiles">The maximum number of archived files to keep.</param>
        /// <param name="isAsync">Specifies if the writing should be done asynchronously, or synchronously with a blocking call.</param>
        /// <returns>An event listener that uses <see cref="RollingFlatFileSink"/> to log events.</returns>
        public static EventListener CreateListener(string fileName, int rollSizeKB, string timestampPattern, RollFileExistsBehavior rollFileExistsBehavior, RollInterval rollInterval, IEventTextFormatter formatter = null, int maxArchivedFiles = 0, bool isAsync = false)
        {
            var listener = new ObservableEventListener();
            listener.LogToRollingFlatFile(fileName, rollSizeKB, timestampPattern, rollFileExistsBehavior, rollInterval, formatter, maxArchivedFiles, isAsync);
            return listener;
        }
    }
}
