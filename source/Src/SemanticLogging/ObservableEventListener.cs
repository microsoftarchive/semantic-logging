// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Tracing;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Observable;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Schema;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Utility;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging
{
    /// <summary>
    /// An <see cref="EventListener"/> that can be observed.
    /// </summary>    
    /// <remarks>This class is thread-safe.</remarks>
    public sealed class ObservableEventListener : EventListener, IObservable<EventEntry>
    {
        private EventSourceSchemaCache schemaCache = EventSourceSchemaCache.Instance;
        private EventEntrySubject subject = new EventEntrySubject();

        /// <summary>
        /// Releases all resources used by the current instance and unsubscribes all the observers.
        /// </summary>
        [SuppressMessage("Microsoft.Design", "CA1063:ImplementIDisposableCorrectly", Justification = "Incorrect implementation is inherited from base class")]
        [SuppressMessage("Microsoft.Usage", "CA2202:Do not dispose objects multiple times", Justification = "Calls the base class Dispose() and the local class Dispose(bool)")]
        public override void Dispose()
        {
            base.Dispose();
            this.subject.Dispose();
        }

        /// <summary>
        /// Called whenever an event has been written by an event source for which the event listener has enabled events.
        /// </summary>
        /// <param name="eventData">The event arguments that describe the event.</param>
        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            Guard.ArgumentNotNull(eventData, "eventData");

            EventSchema schema = null;
            try
            {
                schema = this.schemaCache.GetSchema(eventData.EventId, eventData.EventSource);
            }
            catch (Exception ex)
            {
                // TODO: should I notify all the observers or should I just publish a non-transient
                // error and not notify the rest of the listeners?
                // this.subject.OnError(ex);

                SemanticLoggingEventSource.Log.ParsingEventSourceManifestFailed(eventData.EventSource.Name, eventData.EventId, ex.ToString());
                return;
            }

            var entry = EventEntry.Create(eventData, schema);

            this.subject.OnNext(entry);
        }

        /// <summary>
        /// Notifies the provider that an observer is to receive notifications.
        /// </summary>
        /// <param name="observer">The object that is to receive notifications.</param>
        /// <returns>A reference to an interface that allows observers to stop receiving notifications
        /// before the provider has finished sending them.</returns>
        public IDisposable Subscribe(IObserver<EventEntry> observer)
        {
            return this.subject.Subscribe(observer);
        }
    }
}
