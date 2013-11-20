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
    public class EventEntrySubjectFixture
    {
        [TestMethod]
        public void ShouldCallOnNext()
        {
            using (var subject = new EventEntrySubject())
            {
                var observer = new MockObserver<EventEntry>();

                subject.Subscribe(observer);

                var entry1 = CreateEntry();
                var entry2 = CreateEntry();
                subject.OnNext(entry1);
                subject.OnNext(entry2);

                Assert.AreSame(entry1, observer.OnNextCalls.ElementAt(0));
                Assert.AreSame(entry2, observer.OnNextCalls.ElementAt(1));
            }
        }

        [TestMethod]
        public void ShouldCallOnCompleted()
        {
            using (var subject = new EventEntrySubject())
            {
                var observer = new MockObserver<EventEntry>();
                subject.Subscribe(observer);

                Assert.IsFalse(observer.OnCompletedCalled);

                subject.OnCompleted();

                Assert.IsTrue(observer.OnCompletedCalled);
            }
        }

        [TestMethod]
        public void ShouldCallOnError()
        {
            using (var subject = new EventEntrySubject())
            {
                var observer = new MockObserver<EventEntry>();
                subject.Subscribe(observer);
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
            var observer = new MockObserver<EventEntry>();

            using (var subject = new EventEntrySubject())
            {
                subject.Subscribe(observer);

                Assert.IsFalse(observer.OnCompletedCalled);
            }

            Assert.IsTrue(observer.OnCompletedCalled);
        }

        [TestMethod]
        public void OnCompletedStopsPropagatingEvents()
        {
            using (var subject = new EventEntrySubject())
            {
                var observer = new MockObserver<EventEntry>();
                subject.Subscribe(observer);

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
                var observer = new MockObserver<EventEntry>();
                var subscription = subject.Subscribe(observer);

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
                var observer = new MockObserver<EventEntry>();
                subject.Subscribe(observer);
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

                var observer = new MockObserver<EventEntry>();
                subject.Subscribe(observer);

                Assert.IsTrue(observer.OnCompletedCalled);
            }
        }

        [TestMethod]
        public void CanUnsubscribeNewSubscribersAfterItWasCompleted()
        {
            using (var subject = new EventEntrySubject())
            {
                subject.OnCompleted();

                var observer = new MockObserver<EventEntry>();
                var subscription = subject.Subscribe(observer);

                subscription.Dispose();
            }
        }

        [TestMethod]
        public void ShouldCallOnCompletedInParallel()
        {
            using (var subject = new EventEntrySubject())
            {
                var observer1 = new MockBlockingObserver();
                var observer2 = new MockBlockingObserver();

                subject.Subscribe(observer1);
                subject.Subscribe(observer2);

                var task = Task.Run(() => subject.OnCompleted());

                Thread.Sleep(30);

                Assert.IsTrue(observer1.OnCompletedCalled);
                Assert.IsTrue(observer2.OnCompletedCalled);

                Assert.IsFalse(task.IsCompleted);

                observer1.ResetEvent.Set();
                observer2.ResetEvent.Set();

                Assert.IsTrue(task.Wait(500));
            }
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void SubscribingNullThrows()
        {
            using (var subject = new EventEntrySubject())
            {
                subject.Subscribe(null);
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

        private class MockBlockingObserver : IObserver<EventEntry>
        {
            public ManualResetEvent ResetEvent = new ManualResetEvent(false);
            public ConcurrentQueue<EventEntry> OnNextCalls = new ConcurrentQueue<EventEntry>();
            public bool OnCompletedCalled;
            public Exception OnErrorException;

            void IObserver<EventEntry>.OnCompleted()
            {
                if (OnCompletedCalled) { throw new InvalidOperationException(); }
                this.OnCompletedCalled = true;
                this.ResetEvent.WaitOne();
            }

            void IObserver<EventEntry>.OnError(Exception error)
            {
                if (OnErrorException != null) { throw new InvalidOperationException(); }
                this.OnErrorException = error;
                this.ResetEvent.WaitOne();
            }

            void IObserver<EventEntry>.OnNext(EventEntry value)
            {
                this.OnNextCalls.Enqueue(value);
                this.ResetEvent.WaitOne();
            }
        }
    }
}
