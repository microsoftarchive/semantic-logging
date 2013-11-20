// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Observable
{
    /// <summary>
    /// Very basic implementation of a projection of IObservable to avoid references to Rx when the
    /// end user might not want to do advanced filtering and projection of event streams.
    /// </summary>
    internal static class ObservableProjection
    {
        /// <summary>
        /// Creates a subscription to the source, where in every occurrence of the source stream, it transforms the input
        /// item using the <paramref name="selector"/> and pushes the result to the <paramref name="observer"/>.
        /// </summary>
        /// <param name="source">The original source stream.</param>
        /// <param name="observer">The observer of the output stream.</param>
        /// <param name="selector">The conversion delegate to convert from the original type to the destination.</param>
        /// <typeparam name="Tin">The type of the original source stream.</typeparam>
        /// <typeparam name="Tout">The type of the observer and output stream.</typeparam>
        /// <returns>Returns a subscription token used to unsubscribe to the original source.</returns>
        /// <remarks>This method is behaviorally equivalent to doing the following using Rx: source.Select(selector).Subscribe(observer).</remarks>
        public static IDisposable CreateSubscription<Tin, Tout>(this IObservable<Tin> source, IObserver<Tout> observer, Func<Tin, Tout> selector)
            where Tin : class
        {
            var projection = new Projection<Tin, Tout>(observer, selector);
            return projection.Connect(source);
        }

        [SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable", Justification = "The lifetime is managed by the subscription that is returned to the user.")]
        private sealed class Projection<Tin, Tout> : IObserver<Tin>
            where Tin : class
        {
            private readonly Func<Tin, Tout> selector;
            private IObserver<Tout> observer;
            private ProjectionSubscription subscription;

            public Projection(IObserver<Tout> observer, Func<Tin, Tout> selector)
            {
                this.observer = observer;
                this.selector = selector;
            }

            public IDisposable Connect(IObservable<Tin> source)
            {
                this.subscription = new ProjectionSubscription(this, source.Subscribe(this));
                return this.subscription;
            }

            void IObserver<Tin>.OnCompleted()
            {
                this.observer.OnCompleted();
                using (this.subscription) { }
            }

            void IObserver<Tin>.OnError(Exception error)
            {
                this.observer.OnError(error);
                using (this.subscription) { }
            }

            void IObserver<Tin>.OnNext(Tin value)
            {
                if (value != null)
                {
                    this.observer.OnNext(selector(value));
                }
            }

            private class NoOpObserver : IObserver<Tout>
            {
                void IObserver<Tout>.OnCompleted() { }
                void IObserver<Tout>.OnError(Exception error) { }
                void IObserver<Tout>.OnNext(Tout value) { }
            }

            private sealed class ProjectionSubscription : IDisposable
            {
                private Projection<Tin, Tout> parent;
                private IDisposable subscription;

                public ProjectionSubscription(Projection<Tin, Tout> parent, IDisposable subscription)
                {
                    this.parent = parent;
                    this.subscription = subscription;
                }

                public void Dispose()
                {
                    var currentParent = Interlocked.Exchange<Projection<Tin, Tout>>(ref this.parent, null);
                    if (currentParent != null)
                    {
                        this.subscription.Dispose();
                        currentParent.observer = new NoOpObserver();
                        this.subscription = null;
                    }
                }
            }
        }
    }
}
