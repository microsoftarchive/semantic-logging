// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging
{
    /// <summary>
    /// This map or container class holds a reference to an instance of a logging sink
    /// and a subscription token to an IObservable{EventEntry}, both of which will be disposed by this container
    /// if the container itself is explicitly disposed.
    /// </summary>
    public class SinkSubscription : IDisposable
    {
        private IDisposable subscription;
        private object sink;

        /// <summary>
        /// Initializes a new instance of <see cref="SinkSubscription"/>.
        /// It holds references to a logging sink an a subscription, both
        /// of which will be disposed by this container if the container itself is explicitly disposed.
        /// </summary>
        /// <param name="subscription">The subscription that is used to connect to an <see cref="IObservable{EventEntry}"/>.</param>
        /// <param name="sink">The logging sink.</param>
        public SinkSubscription(IDisposable subscription, object sink)
        {
            this.subscription = subscription;
            this.sink = sink;
        }

        /// <summary>
        /// A reference to the underlying subscription token.
        /// </summary>
        public IDisposable Subscription
        {
            get { return this.subscription; }
        }

        /// <summary>
        /// A reference to the underlying log sink.
        /// </summary>
        public object Sink
        {
            get { return this.sink; }
        }

        /// <summary>
        /// Disposes both the <see cref="Subscription"/> and the <see cref="Sink"/> if it implements <see cref="IDisposable"/>
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes both the <see cref="Subscription"/> and the <see cref="Sink"/> if it implements <see cref="IDisposable"/>
        /// </summary>
        /// <param name="disposing">True if explicitly disposing the instance.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Only disposes subscription and sink if explicitly disposed.
                using (this.subscription) { }
                using (this.sink as IDisposable) { }
            }
        }
    }

    /// <summary>
    /// This map or container class holds a reference to an instance of a logging sink
    /// and a subscription token to an IObservable{EventEntry}, both of which will be disposed by this container
    /// when the container itself is disposed.
    /// </summary>
    public sealed class SinkSubscription<T> : SinkSubscription
    {
        /// <summary>
        /// Initializes a new instance of <see cref="SinkSubscription"/>.
        /// It holds references to a logging sink an a subscription, both
        /// of which will be disposed by this container when the container itself is disposed.
        /// </summary>
        /// <param name="subscription">The subscription that is used to connect to an <see cref="IObservable{EventEntry}"/>.</param>
        /// <param name="sink">The logging sink.</param>
        public SinkSubscription(IDisposable subscription, T sink)
            : base(subscription, sink)
        {
        }

        /// <summary>
        /// A reference to the underlying log sink.
        /// </summary>
        public new T Sink
        {
            get { return (T)base.Sink; }
        }
    }
}
