// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Sinks;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.TestSupport;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Utility;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.Utility
{
    [TestClass]
    public class bufferedEventPublisher_given_configuration
    {
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void when_null_sinkId_throws()
        {
            BufferedEventPublisher<int>.CreateAndStart(null, b => { return Task.FromResult(b.Count); }, TimeSpan.Zero, 1, 1000, CancellationToken.None);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void when_null_eventPublisherAction_throws()
        {
            BufferedEventPublisher<int>.CreateAndStart("sink", null, TimeSpan.Zero, 1, 1000, CancellationToken.None);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void when_negative_bufferingInterval_throws()
        {
            BufferedEventPublisher<int>.CreateAndStart("sink", b => { return Task.FromResult(b.Count); }, TimeSpan.FromSeconds(-1), 1, 1000, CancellationToken.None);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void when_overrange_bufferingInterval_throws()
        {
            BufferedEventPublisher<int>.CreateAndStart("sink", b => { return Task.FromResult(b.Count); }, TimeSpan.FromMilliseconds(Convert.ToInt64(int.MaxValue) + 1L), 1, 1000, CancellationToken.None);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void when_negative_bufferingCount_throw()
        {
            BufferedEventPublisher<int>.CreateAndStart("sink", b => { return Task.FromResult(b.Count); }, TimeSpan.FromSeconds(5), -1, 1000, CancellationToken.None);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void when_no_interval_and_no_count_throw()
        {
            BufferedEventPublisher<int>.CreateAndStart("sink", b => { return Task.FromResult(b.Count); }, Timeout.InfiniteTimeSpan, 0, 1000, CancellationToken.None);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void when_maxBufferSize_smaller_than_3times_count_throw()
        {
            BufferedEventPublisher<int>.CreateAndStart("sink", b => { return Task.FromResult(b.Count); }, Timeout.InfiniteTimeSpan, 1000, 2999, CancellationToken.None);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void when_overrange_maxbuffering_throws()
        {
            BufferedEventPublisher<int>.CreateAndStart("sink", b => { return Task.FromResult(b.Count); }, Timeout.InfiniteTimeSpan, 1, 499, CancellationToken.None);
        }

        [TestMethod]
        public void when_zero_interval_is_set_default_minimum()
        {
            var sut = BufferedEventPublisher<int>.CreateAndStart("sink", b => { return Task.FromResult(b.Count); }, TimeSpan.Zero, 0, 1000, CancellationToken.None);
            Assert.IsNotNull(sut);
        }
    }

    public abstract class given_bufferedEventPublisher : ContextBase
    {
        private BufferedEventPublisher<int> sut;
        private TimeSpan bufferingInterval = TimeSpan.FromSeconds(1);
        private int totalEventsToPublish;
        private List<int> publishedEvents = new List<int>();
        private TaskCompletionSource<bool> completionSource = new TaskCompletionSource<bool>();
        private TimeSpan waitTimeout = TimeSpan.FromSeconds(5);

        public bool ShouldThrowOnPublish { get; set; }

        protected override void Given()
        {
            this.sut = BufferedEventPublisher<int>.CreateAndStart("sink", PublishEventsAsync, bufferingInterval, 0, 1000, new CancellationToken());
        }

        protected override void OnCleanup()
        {
            this.sut.Dispose();
        }

        private Task<bool> Completed
        {
            get { return this.completionSource.Task; }
        }

        private async Task<int> PublishEventsAsync(IList<int> batch)
        {
            await Task.Yield();
            publishedEvents.AddRange(batch);

            if (this.totalEventsToPublish == 0 || this.publishedEvents.Count >= this.totalEventsToPublish)
            {
                this.completionSource.TrySetResult(true);
            }

            if (this.ShouldThrowOnPublish)
            {
                throw new InvalidOperationException("Thrown by text fixture.");    
            }

            return batch.Count;
        }

        [TestClass]
        public class when_post_events_and_interval_expire : given_bufferedEventPublisher
        {
            protected override void Given()
            {
                this.totalEventsToPublish = 10;
                base.Given();
            }

            protected override void When()
            {
                for (int i = 0; i < this.totalEventsToPublish; i++)
                {
                    this.sut.TryPost(i);
                    Thread.Sleep(10);
                }
            }

            [TestMethod]
            public void then_all_events_are_published()
            {
                Assert.IsTrue(this.Completed.Wait(waitTimeout));

                Assert.AreEqual(this.totalEventsToPublish, this.publishedEvents.Count);

                for (int i = 0; i < this.totalEventsToPublish; i++)
                {
                    Assert.AreEqual(i, this.publishedEvents[i]);
                }
            }
        }

        [TestClass]
        public class when_post_events_and_flushAsync : given_bufferedEventPublisher
        {
            protected override void Given()
            {
                this.totalEventsToPublish = 10;
                base.Given();
            }

            protected override void When()
            {
                for (int i = 0; i < this.totalEventsToPublish; i++)
                {
                    this.sut.TryPost(i);
                    Thread.Sleep(10);
                }
            }

            [TestMethod]
            public void then_all_events_are_published()
            {
                Assert.IsTrue(this.sut.FlushAsync().Wait(waitTimeout));

                Assert.AreEqual(this.totalEventsToPublish, this.publishedEvents.Count);
            }
        }

        [TestClass]
        public class when_post_events_before_and_after_retry_started : given_bufferedEventPublisher
        {
            private readonly TimeSpan interval = TimeSpan.FromMilliseconds(100);

            protected override void Given()
            {
                this.sut = BufferedEventPublisher<int>.CreateAndStart("sink", PublishEventsAsync, interval, 0, 1000, new CancellationToken());
                this.totalEventsToPublish = 3;
            }

            protected override void When()
            {
                for (int i = 0; i < this.totalEventsToPublish - 1; i++)
                {
                    this.sut.TryPost(i);
                    Thread.Sleep(10);
                }

                Thread.Sleep(interval + TimeSpan.FromSeconds(1));
                // Post an event after timeout fired

                this.sut.TryPost(0);
            }

            [TestMethod]
            public void then_all_events_posted_before_and_after_retry_should_be_published()
            {
                Assert.IsTrue(this.Completed.Wait(waitTimeout));

                Assert.AreEqual(this.totalEventsToPublish, this.publishedEvents.Count);
            }
        }

        [TestClass]
        public class when_post_events_with_count_only : given_bufferedEventPublisher
        {
            protected override void Given()
            {
                this.totalEventsToPublish = 2;
                this.sut = BufferedEventPublisher<int>.CreateAndStart("sink", PublishEventsAsync, Timeout.InfiniteTimeSpan, this.totalEventsToPublish, 1000, new CancellationToken());
            }

            protected override void When()
            {
                for (int i = 0; i < this.totalEventsToPublish; i++)
                {
                    this.sut.TryPost(i);
                    Thread.Sleep(30);
                }
            }

            [TestMethod]
            public void then_all_events_are_published()
            {
                Assert.IsTrue(this.Completed.Wait(waitTimeout));
                Assert.AreEqual(this.totalEventsToPublish, this.publishedEvents.Count);
            }
        }

        [TestClass]
        public class when_post_events_with_interval_only : given_bufferedEventPublisher
        {
            protected override void Given()
            {
                this.totalEventsToPublish = 100;
                this.sut = BufferedEventPublisher<int>.CreateAndStart("sink", PublishEventsAsync, TimeSpan.FromMilliseconds(1), 0, 1000, new CancellationToken());
            }

            protected override void When()
            {
                for (int i = 0; i < this.totalEventsToPublish; i++)
                {
                    this.sut.TryPost(i);
                    Thread.Sleep(30);
                }
            }

            [TestMethod]
            public void then_all_events_are_published()
            {
                Assert.IsTrue(this.Completed.Wait(waitTimeout));
                
                for (int i = 0; i < this.totalEventsToPublish; i++)
                {
                    Assert.AreEqual(i, this.publishedEvents[i], string.Join(", ", this.publishedEvents));
                }

                Assert.AreEqual(this.totalEventsToPublish, this.publishedEvents.Count);
            }
        }

        [TestClass]
        public class when_post_events_with_count_only_and_flush : given_bufferedEventPublisher
        {
            protected override void Given()
            {
                this.totalEventsToPublish = 2;
                this.sut = BufferedEventPublisher<int>.CreateAndStart("sink", PublishEventsAsync, Timeout.InfiniteTimeSpan, this.totalEventsToPublish, 1000, new CancellationToken());
            }

            protected override void When()
            {
                for (int i = 0; i < this.totalEventsToPublish; i++)
                {
                    this.sut.TryPost(i);
                    Thread.Sleep(30);
                }
            }

            [TestMethod]
            public void then_all_events_are_published()
            {
                Assert.IsTrue(this.sut.FlushAsync().Wait(waitTimeout));
                Assert.IsTrue(this.Completed.Wait(waitTimeout));

                Assert.AreEqual(this.totalEventsToPublish, this.publishedEvents.Count);
            }
        }

        [TestClass]
        public class when_post_events_with_internval_only_and_flush : given_bufferedEventPublisher
        {
            protected override void Given()
            {
                this.totalEventsToPublish = 100;
                this.sut = BufferedEventPublisher<int>.CreateAndStart("sink", PublishEventsAsync, TimeSpan.FromMilliseconds(1), 0, 1000, new CancellationToken());
            }

            protected override void When()
            {
                for (int i = 0; i < this.totalEventsToPublish; i++)
                {
                    this.sut.TryPost(i);
                    Thread.Sleep(30);
                }
            }

            [TestMethod]
            public void then_all_events_are_published()
            {
                Assert.IsTrue(this.sut.FlushAsync().Wait(waitTimeout));
                Assert.IsTrue(this.Completed.Wait(waitTimeout));

                for (int i = 0; i < this.totalEventsToPublish; i++)
                {
                    Assert.AreEqual(i, this.publishedEvents[i]);
                }

                Assert.AreEqual(this.totalEventsToPublish, this.publishedEvents.Count);
            }
        }

        [TestClass]
        public class when_post_events_with_count_only_and_multi_flush : given_bufferedEventPublisher
        {
            protected override void Given()
            {
                this.totalEventsToPublish = 2;
                this.sut = BufferedEventPublisher<int>.CreateAndStart("sink", PublishEventsAsync, Timeout.InfiniteTimeSpan, this.totalEventsToPublish, 1000, new CancellationToken());
            }

            protected override void When()
            {
                for (int i = 0; i < this.totalEventsToPublish; i++)
                {
                    this.sut.TryPost(i);
                    Thread.Sleep(30);
                }

                // First flush
                Assert.IsTrue(this.sut.FlushAsync().Wait(waitTimeout));
                Assert.AreEqual(TaskStatus.RanToCompletion, this.Completed.Status);

                Assert.AreEqual(this.totalEventsToPublish, this.publishedEvents.Count);

                // Reset
                this.completionSource = new TaskCompletionSource<bool>();
                this.publishedEvents.Clear();

                for (int i = 0; i < this.totalEventsToPublish; i++)
                {
                    this.sut.TryPost(i);
                    Thread.Sleep(30);
                }
            }

            [TestMethod]
            public void then_all_events_are_published_after_each_flush()
            {
                // Second Flush
                Assert.IsTrue(this.sut.FlushAsync().Wait(waitTimeout));
                Assert.AreEqual(TaskStatus.RanToCompletion, this.Completed.Status);

                Assert.IsTrue(this.Completed.Result);
                Assert.AreEqual(this.totalEventsToPublish, this.publishedEvents.Count);
            }
        }

        [TestClass]
        public class when_post_events_with_count_and_interval_with_count_first : given_bufferedEventPublisher
        {
            protected override void Given()
            {
                this.totalEventsToPublish = 2;
                this.sut = BufferedEventPublisher<int>.CreateAndStart("sink", PublishEventsAsync, TimeSpan.FromSeconds(10), this.totalEventsToPublish, 1000, new CancellationToken());
            }

            protected override void When()
            {
                for (int i = 0; i < this.totalEventsToPublish; i++)
                {
                    this.sut.TryPost(i);
                    Thread.Sleep(30);
                }
            }

            [TestMethod]
            public void then_all_events_are_published()
            {
                Assert.IsTrue(this.Completed.Wait(waitTimeout));
                Assert.AreEqual(this.totalEventsToPublish, this.publishedEvents.Count);
            }
        }

        [TestClass]
        public class when_post_events_with_count_and_interval_with_interval_first : given_bufferedEventPublisher
        {
            protected override void Given()
            {
                this.totalEventsToPublish = 2;
                this.sut = BufferedEventPublisher<int>.CreateAndStart("sink", PublishEventsAsync, TimeSpan.FromMilliseconds(500), this.totalEventsToPublish + 1, 1000, new CancellationToken());
            }

            protected override void When()
            {
                for (int i = 0; i < this.totalEventsToPublish; i++)
                {
                    this.sut.TryPost(i);
                    Thread.Sleep(30);
                }
            }

            [TestMethod]
            public void then_all_events_are_published()
            {
                Assert.IsTrue(this.Completed.Wait(waitTimeout));
                Assert.AreEqual(this.totalEventsToPublish, this.publishedEvents.Count);
            }
        }

        [TestClass]
        public class when_publishing_more_than_buffering_count : given_bufferedEventPublisher
        {
            private int bufferingCount;

            protected override void Given()
            {
                this.bufferingCount = 10;
                this.totalEventsToPublish = 0;
                var automaticFlushingInterval = TimeSpan.FromMinutes(30);
                this.sut = BufferedEventPublisher<int>.CreateAndStart("sink", PublishEventsAsync, automaticFlushingInterval, bufferingCount, 1000, new CancellationToken());
                Thread.Sleep(200);
            }

            protected override void When()
            {
                for (int i = 0; i < bufferingCount + 5; i++)
                {
                    this.sut.TryPost(i);
                    Thread.Sleep(10);
                }
            }

            [TestMethod]
            public void then_only_batch_size_number_of_events_are_published()
            {
                Assert.IsTrue(this.Completed.Wait(TimeSpan.FromSeconds(30)));
                Assert.AreEqual(this.bufferingCount, this.publishedEvents.Count);
            }
        }

        [TestClass]
        public class when_throwing_exception_on_publish : given_bufferedEventPublisher
        {
            private int bufferingCount;

            protected override void Given()
            {
                this.ShouldThrowOnPublish = true;
                this.bufferingCount = 10;
                this.totalEventsToPublish = 0;
                var automaticFlushingInterval = TimeSpan.FromMilliseconds(1);
                this.sut = BufferedEventPublisher<int>.CreateAndStart("sink", PublishEventsAsync, automaticFlushingInterval, bufferingCount, 1000, new CancellationToken());
            }

            protected override void When()
            {
                for (int i = 0; i < bufferingCount; i++)
                {
                    this.sut.TryPost(i);
                    Thread.Sleep(10);
                }
            }

            [TestMethod]
            public void then_buffering_count_does_not_attempt_to_flush()
            {
                Assert.IsTrue(this.Completed.Wait(TimeSpan.FromSeconds(10)));
                Assert.AreEqual(this.bufferingCount, this.publishedEvents.Count);

                // Reset
                this.completionSource = new TaskCompletionSource<bool>();

                for (int i = 0; i < bufferingCount; i++)
                {
                    this.sut.TryPost(i);
                    Thread.Sleep(10);
                }

                Assert.IsFalse(this.Completed.Wait(TimeSpan.FromSeconds(3)));
                Assert.AreEqual(this.bufferingCount, this.publishedEvents.Count);
            }

            [TestMethod]
            public void then_flushing_returns_failure()
            {
                Assert.IsTrue(this.Completed.Wait(TimeSpan.FromSeconds(10)));

                try
                {
                    Assert.IsTrue(this.sut.FlushAsync().Wait(TimeSpan.FromSeconds(3)));
                    Assert.Fail("Exception should be thrown.");
                }
                catch (AggregateException ex)
                {
                    Assert.IsInstanceOfType(ex.InnerException, typeof(FlushFailedException));
                }
            }
        }
    }
}
