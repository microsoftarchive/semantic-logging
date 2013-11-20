// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Observable;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.Observable
{
    [TestClass]
    public class ObservableProjectionFixture
    {
        [TestMethod]
        public void ShouldCallOnNext()
        {
            using (var subject = new EventEntrySubject())
            {
                var observer = new MockObserver<int>();

                var subscription = subject.CreateSubscription(observer, entry => entry.EventId);

                var entry1 = CreateEntry(1);
                var entry2 = CreateEntry(2);
                subject.OnNext(entry1);
                subject.OnNext(entry2);

                Assert.AreEqual(entry1.EventId, observer.OnNextCalls.ElementAt(0));
                Assert.AreEqual(entry2.EventId, observer.OnNextCalls.ElementAt(1));

                subscription.Dispose();
            }
        }

        [TestMethod]
        public void ShouldCallOnComplete()
        {
            using (var subject = new EventEntrySubject())
            {
                var observer = new MockObserver<int>();

                Assert.IsFalse(observer.OnCompletedCalled);

                subject.CreateSubscription(observer, entry => entry.EventId);

                subject.OnCompleted();

                Assert.IsTrue(observer.OnCompletedCalled);
            }
        }

        [TestMethod]
        public void ShouldCallOnError()
        {
            using (var subject = new EventEntrySubject())
            {
                var observer = new MockObserver<int>();
                subject.CreateSubscription(observer, entry => entry.EventId);
                var error = new Exception();
                subject.OnError(error);

                subject.OnNext(CreateEntry());

                Assert.AreSame(error, observer.OnErrorException);
                Assert.AreEqual(0, observer.OnNextCalls.Count);
            }
        }

        [TestMethod]
        public void DisposeCallsOnCompleted()
        {
            var observer = new MockObserver<int>();

            using (var subject = new EventEntrySubject())
            {
                subject.CreateSubscription(observer, entry => entry.EventId);

                Assert.IsFalse(observer.OnCompletedCalled);
            }

            Assert.IsTrue(observer.OnCompletedCalled);
        }

        [TestMethod]
        public void OnCompletedStopsPropagatingEvents()
        {
            using (var subject = new EventEntrySubject())
            {
                var observer = new MockObserver<int>();
                subject.CreateSubscription(observer, entry => entry.EventId);

                subject.OnCompleted();
                subject.OnNext(CreateEntry());

                Assert.AreEqual(0, observer.OnNextCalls.Count);
            }
        }

        [TestMethod]
        public void UnsubscribeStopsPropagatingEvents()
        {
            using (var subject = new EventEntrySubject())
            {
                var observer = new MockObserver<int>();
                var subscription = subject.CreateSubscription(observer, entry => entry.EventId);

                subscription.Dispose();
                subject.OnNext(CreateEntry());

                Assert.AreEqual(0, observer.OnNextCalls.Count);
            }
        }

        [TestMethod]
        public void OnErrorStopsPropagatingEvents()
        {
            using (var subject = new EventEntrySubject())
            {
                var observer = new MockObserver<int>();
                subject.CreateSubscription(observer, entry => entry.EventId);
                subject.OnError(new Exception());

                subject.OnNext(CreateEntry());

                Assert.AreEqual(0, observer.OnNextCalls.Count);
            }
        }

        [TestMethod]
        public void OnCompletedIsSentToAllNewSubscribersAfterItWasCompleted()
        {
            using (var subject = new EventEntrySubject())
            {
                subject.OnCompleted();

                var observer = new MockObserver<int>();
                subject.CreateSubscription(observer, entry => entry.EventId);

                Assert.IsTrue(observer.OnCompletedCalled);
            }
        }

        [TestMethod]
        public void CanUnsubscribeNewSubscribersAfterItWasCompleted()
        {
            using (var subject = new EventEntrySubject())
            {
                subject.OnCompleted();

                var observer = new MockObserver<int>();
                var subscription = subject.CreateSubscription(observer, entry => entry.EventId);

                subscription.Dispose();
            }
        }

        private static EventEntry CreateEntry(int id = 1)
        {
            return new EventEntry(Guid.Empty, id, null, null, DateTimeOffset.UtcNow, null);
        }

        private class MockObserver<T> : IObserver<T>
        {
            public ConcurrentQueue<T> OnNextCalls = new ConcurrentQueue<T>();
            public bool OnCompletedCalled;
            public Exception OnErrorException;

            void IObserver<T>.OnCompleted()
            {
                if (OnCompletedCalled) { throw new InvalidOperationException(); }
                this.OnCompletedCalled = true;
            }

            void IObserver<T>.OnError(Exception error)
            {
                if (OnErrorException != null) { throw new InvalidOperationException(); }
                this.OnErrorException = error;
            }

            void IObserver<T>.OnNext(T value)
            {
                this.OnNextCalls.Enqueue(value);
            }
        }
    }
}
