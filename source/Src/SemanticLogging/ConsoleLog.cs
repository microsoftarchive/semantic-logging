// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Diagnostics.Tracing;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Formatters;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Sinks;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Utility;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging
{
    /// <summary>
    /// Factories and helpers for using the <see cref="ConsoleSink"/>.
    /// </summary>
    public static class ConsoleLog
    {
        /// <summary>
        /// Subscribes to an <see cref="IObservable{EventEntry}"/> using a <see cref="ConsoleSink"/>.
        /// </summary>
        /// <param name="eventStream">The event stream. Typically this is an instance of <see cref="ObservableEventListener"/>.</param>
        /// <param name="formatter">The formatter.</param>
        /// <param name="colorMapper">The color mapper instance.</param>
        /// <returns>A subscription to the sink that can be disposed to unsubscribe the sink, or to get access to the sink instance.</returns>
        public static SinkSubscription<ConsoleSink> LogToConsole(this IObservable<EventEntry> eventStream, IEventTextFormatter formatter = null, IConsoleColorMapper colorMapper = null)
        {
            formatter = formatter ?? new EventTextFormatter();
            colorMapper = colorMapper ?? new DefaultConsoleColorMapper();

            var sink = new ConsoleSink(formatter, colorMapper);

            var subscription = eventStream.Subscribe(sink);

            return new SinkSubscription<ConsoleSink>(subscription, sink);
        }

        /// <summary>
        /// Creates an event listener that logs using a <see cref="ConsoleSink"/>.
        /// </summary>
        /// <param name="formatter">The formatter.</param>
        /// <param name="colorMapper">The color mapper instance.</param>
        /// <returns>An event listener that uses <see cref="ConsoleSink"/> to display events.</returns>
        public static EventListener CreateListener(IEventTextFormatter formatter = null, IConsoleColorMapper colorMapper = null)
        {
            var listener = new ObservableEventListener();
            listener.LogToConsole(formatter, colorMapper);
            return listener;
        }
    }
}
