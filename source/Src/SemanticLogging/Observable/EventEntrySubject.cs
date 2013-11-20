// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Utility;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Observable
{
    /// <summary>
    /// A subject that can be observed and publish events.
    /// </summary>    
    /// <remarks>
    /// This is a very basic implementation of a subject to avoid references to Rx when the
    /// end user might not want to do advanced filtering and projection of event streams.
    /// </remarks>
    internal sealed class EventEntrySubject : IObservable<EventEntry>, IObserver<EventEntry>, IDisposable
    {
        private readonly object lockObject = new object();
        private volatile ReadOnlyCollection<IObserver<EventEntry>> observers = new List<IObserver<EventEntry>>().AsReadOnly();
        private volatile bool isFrozen = false;

        /// <summary>
        /// Releases all resources used by the current instance and unsubscribes all the observers.
        /// </summary>
        public void Dispose()
        {
            this.OnCompleted();
        }

        /// <summary>
        /// Notifies the provider that an observer is to receive notifications.
        /// </summary>
        /// <param name="observer">The object that is to receive notifications.</param>
        /// <returns>A reference to an interface that allows observers to stop receiving notifications
        /// before the provider has finished sending them.</returns>
        public IDisposable Subscribe(IObserver<EventEntry> observer)
        {
            Guard.ArgumentNotNull(observer, "observer");

            lock (this.lockObject)
            {
                if (!this.isFrozen)
                {
                    var copy = this.observers.ToList();
                    copy.Add(observer);
                    this.observers = copy.AsReadOnly();
                    return new Subscription(this, observer);
                }
            }

            observer.OnCompleted();
            return new EmptyDisposable();
        }

        private void Unsubscribe(IObserver<EventEntry> observer)
        {
            lock (this.lockObject)
            {
                this.observers = this.observers.Where(x => !observer.Equals(x)).ToList().AsReadOnly();
            }
        }

        /// <summary>
        /// Notifies the observer that the provider has finished sending push-based notifications.
        /// </summary>
        public void OnCompleted()
        {
            var currentObservers = this.TakeObserversAndFreeze();

            if (currentObservers != null)
            {
                Parallel.ForEach(currentObservers, observer => observer.OnCompleted());
            }
        }

        /// <summary>
        /// Notifies the observer that the provider has experienced an error condition.
        /// </summary>
        /// <param name="error">An object that provides additional information about the error.</param>
        public void OnError(Exception error)
        {
            var currentObservers = TakeObserversAndFreeze();

            if (currentObservers != null)
            {
                Parallel.ForEach(currentObservers, observer => observer.OnError(error));
            }
        }

        /// <summary>
        /// Provides the observers with new data.
        /// </summary>
        /// <param name="value">The current notification information.</param>
        public void OnNext(EventEntry value)
        {
            foreach (var observer in this.observers)
            {
                // TODO: should I isolate errors (i.e: try/catch around each OnNext call)?
                observer.OnNext(value);
            }
        }

        private ReadOnlyCollection<IObserver<EventEntry>> TakeObserversAndFreeze()
        {
            lock (this.lockObject)
            {
                if (!this.isFrozen)
                {
                    this.isFrozen = true;
                    var copy = this.observers;
                    this.observers = new List<IObserver<EventEntry>>().AsReadOnly();

                    return copy;
                }

                return null;
            }
        }

        private sealed class Subscription : IDisposable
        {
            private IObserver<EventEntry> observer;
            private EventEntrySubject subject;

            public Subscription(EventEntrySubject subject, IObserver<EventEntry> observer)
            {
                this.subject = subject;
                this.observer = observer;
            }

            public void Dispose()
            {
                var current = Interlocked.Exchange<IObserver<EventEntry>>(ref this.observer, null);
                if (current != null)
                {
                    this.subject.Unsubscribe(current);
                    this.subject = null;
                }
            }
        }

        private sealed class EmptyDisposable : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}
