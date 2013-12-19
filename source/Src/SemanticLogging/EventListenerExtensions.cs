// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Properties;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Utility;
using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging
{
    /// <summary>
    /// Extension methods to enable and disable events based on <see cref="EventSource"/> names rather than actual instances.
    /// </summary>
    public static class EventListenerExtensions
    {
        /// <summary>
        /// Disables all events for the specified event source.
        /// </summary>
        /// <param name="eventListener">The event listener.</param>
        /// <param name="eventSourceName">The name of the event source to enable events for.</param>
        /// <remarks>
        /// If the event source with the supplied name has already been created the request is processed immediately. Otherwise the request
        /// is deferred until the event source is created.
        /// </remarks>
        public static void DisableEvents(this EventListener eventListener, string eventSourceName)
        {
            CastToObservableEventListener(eventListener).DisableEvents(eventSourceName);
        }

        /// <summary>
        /// Enables events for the event source with the specified name that have the specified verbosity level or lower.
        /// </summary>
        /// <param name="eventListener">The event listener.</param>
        /// <param name="eventSourceName">The name of the event source to enable events for.</param>
        /// <param name="level">The level of events to enable.</param>
        /// <returns>
        ///   <see langword="false" /> if the request was deferred; otherwise, <see langword="true" />.
        /// </returns>
        /// <remarks>
        /// If the event source with the supplied name has already been created the request is processed immediately. Otherwise the request
        /// is deferred until the event source is created.
        /// </remarks>
        public static bool EnableEvents(this EventListener eventListener, string eventSourceName, EventLevel level)
        {
            return CastToObservableEventListener(eventListener).EnableEvents(eventSourceName, level);
        }

        /// <summary>
        /// Enables events for the specified event source that has the specified verbosity level or lower, and matching keyword flags.
        /// </summary>
        /// <param name="eventListener">The event listener.</param>
        /// <param name="eventSourceName">The name of the event source to enable events for.</param>
        /// <param name="level">The level of events to enable.</param>
        /// <param name="matchAnyKeyword">The keyword flags necessary to enable the events.</param>
        /// <returns>
        ///   <see langword="false" /> if the request was deferred; otherwise, <see langword="true" />.
        /// </returns>
        /// <remarks>
        /// If the event source with the supplied name has already been created the request is processed immediately. Otherwise the request
        /// is deferred until the event source is created.
        /// </remarks>
        public static bool EnableEvents(this EventListener eventListener, string eventSourceName, EventLevel level, EventKeywords matchAnyKeyword)
        {
            return CastToObservableEventListener(eventListener).EnableEvents(eventSourceName, level, matchAnyKeyword);
        }

        /// <summary>
        /// Enables events for the specified event source that has the specified verbosity level or lower, and matching keyword flags.
        /// </summary>
        /// <param name="eventListener">The event listener.</param>
        /// <param name="eventSourceName">The name of the event source to enable events for.</param>
        /// <param name="level">The level of events to enable.</param>
        /// <param name="matchAnyKeyword">The keyword flags necessary to enable the events.</param>
        /// <param name="arguments">The arguments to be matched to enable the events.</param>
        /// <returns>
        ///   <see langword="false" /> if the request was deferred; otherwise, <see langword="true" />.
        /// </returns>
        /// <remarks>
        /// If the event source with the supplied name has already been created the request is processed immediately. Otherwise the request
        /// is deferred until the event source is created.
        /// </remarks>
        public static bool EnableEvents(this EventListener eventListener, string eventSourceName, EventLevel level, EventKeywords matchAnyKeyword, IDictionary<string, string> arguments)
        {
            return CastToObservableEventListener(eventListener).EnableEvents(eventSourceName, level, matchAnyKeyword, arguments);
        }

        private static ObservableEventListener CastToObservableEventListener(EventListener eventListener)
        {
            Guard.ArgumentNotNull(eventListener, "eventListener");

            var observableEventListener = eventListener as ObservableEventListener;

            if (observableEventListener == null)
            {
                throw new ArgumentException(Resources.ArgumentMustBeObservableEventListener, "eventListener");
            }

            return observableEventListener;
        }
    }
}
