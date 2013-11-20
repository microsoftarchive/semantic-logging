// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Reactive.Linq;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging
{
    /// <summary>
    /// Demo extensions on how you can leverage the power of Rx to perform some filtering (or transformation) of the event stream
    /// before it is sent to the underlying sink.
    /// </summary>
    public static class ThrottleObservableExtensions
    {
        /// <summary>
        /// Throttles entries with the specified <paramref name="eventId"/>. The first occurrence of the event with ID <paramref name="eventId"/>
        /// is sent to the output stream and then muted for a time interval specified by <paramref name="throttleInterval"/>.
        /// All other events with a different ID are sent to the output stream without filtering.
        /// </summary>
        /// <param name="stream">The original stream of events.</param>
        /// <param name="throttleInterval">The time interval to mute additional occurrences of the event after one is sent to the output stream.</param>
        /// <param name="eventId">The event id that will be throttled.</param>
        /// <returns>An observable that has this filtering capability.</returns>
        public static IObservable<EventEntry> ThrottleEventsWithEventId(this IObservable<EventEntry> stream, TimeSpan throttleInterval, int eventId)
        {
            return ThrottleByCondition(stream, throttleInterval, x => x.EventId == eventId);
        }

        private static IObservable<T> ThrottleByCondition<T>(this IObservable<T> stream, TimeSpan throttleInterval, Func<T, bool> throttleCondition)
        {
            if (stream == null) throw new ArgumentNullException("stream");
            if (throttleInterval < TimeSpan.Zero) throw new ArgumentOutOfRangeException("throttleInterval");
            if (throttleCondition == null) throw new ArgumentNullException("throttleCondition");
            
            return System.Reactive.Linq.Observable.Create<T>(observer =>
            {
                var nextPublishingTime = DateTimeOffset.MinValue;
                var subscription = stream.Timestamp().Subscribe(
                    newItem =>
                        {
                            bool shouldThrottle;
                            try
                            {
                                shouldThrottle = throttleCondition(newItem.Value);
                            }
                            catch (Exception ex)
                            {
                                observer.OnError(ex);
                                return;
                            }

                            if (shouldThrottle)
                            {
                                if (newItem.Timestamp >= nextPublishingTime)
                                {
                                    nextPublishingTime = newItem.Timestamp.Add(throttleInterval);
                                    observer.OnNext(newItem.Value);
                                }
                            }
                            else
                            {
                                observer.OnNext(newItem.Value);
                            }
                        },
                    observer.OnError,
                    observer.OnCompleted);

                return subscription;
            });
        }
    }
}
