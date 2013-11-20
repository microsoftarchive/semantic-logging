// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging
{
    /// <summary>
    /// Demo extensions on how you can leverage the power of Rx to perform some filtering (or transformation) of the event stream
    /// before it is sent to the underlying sink.
    /// </summary>
    public static class FlushObservableExtensions
    {
        /// <summary>
        /// Buffers entries that do no satisfy the <paramref name="shouldFlush"/> condition, using a circular buffer with a max
        /// capacity. When an entry that satisfies the condition ocurrs, then it flushes the circular buffer and the new entry,
        /// and starts buffering again.
        /// </summary>
        /// <typeparam name="T">The type of entry.</typeparam>
        /// <param name="stream">The original stream of events.</param>
        /// <param name="shouldFlush">The condition that defines whether the item and the buffered entries are flushed.</param>
        /// <param name="bufferSize">The buffer size for accumulated entries.</param>
        /// <returns>An observable that has this filtering capability.</returns>
        public static IObservable<T> FlushOnTrigger<T>(this IObservable<T> stream, Func<T, bool> shouldFlush, int bufferSize)
        {
            if (stream == null) throw new ArgumentNullException("stream");
            if (shouldFlush == null) throw new ArgumentNullException("shouldFlush");
            if (bufferSize < 1) throw new ArgumentOutOfRangeException("bufferSize");

            return System.Reactive.Linq.Observable.Create<T>(observer =>
            {
                var buffer = new CircularBuffer<T>(bufferSize);
                var subscription = stream.Subscribe(
                    newItem =>
                        {
                            bool result;
                            try
                            {
                                result = shouldFlush(newItem);
                            }
                            catch (Exception ex)
                            {
                                observer.OnError(ex);
                                return;
                            }

                            if (result)
                            {
                                foreach (var buffered in buffer.TakeAll())
                                {
                                    observer.OnNext(buffered);
                                }

                                observer.OnNext(newItem);
                            }
                            else
                            {
                                buffer.Add(newItem);
                            }
                        },
                    observer.OnError,
                    observer.OnCompleted);

                return subscription;
            });
        }

        /// <summary>
        /// Buffers entries that are of an <see cref="EventLevel"/> value larger than <paramref name="level"/>, using a circular buffer with a max
        /// capacity. When an entry that has a value of <paramref name="level"/> or lower, then it flushes the circular buffer and the new entry,
        /// and starts buffering again.
        /// </summary>
        /// <param name="stream">The original stream of events.</param>
        /// <param name="level">The minimum level that defines whether the item and the buffered entries are flushed.</param>
        /// <param name="bufferSize">The buffer size for accumulated entries.</param>
        /// <returns>An observable that has this filtering capability.</returns>
        public static IObservable<EventEntry> FlushOnEventLevel(this IObservable<EventEntry> stream, EventLevel level, int bufferSize)
        {
            return FlushOnTrigger(stream, entry => entry.Schema.Level <= level, bufferSize);
        }

        /// <summary>
        /// Very basic implemantation of a circular buffer.
        /// </summary>
        /// <typeparam name="T">The contained type.</typeparam>
        /// <remarks>This class is not thread safe.</remarks>
        private class CircularBuffer<T>
        {
            private readonly int size;
            private Queue<T> queue;

            public CircularBuffer(int size)
            {
                this.queue = new Queue<T>(size);
                this.size = size;
            }

            public void Add(T obj)
            {
                if (this.queue.Count >= this.size)
                {
                    this.queue.Dequeue();
                    this.queue.Enqueue(obj);
                }
                else
                {
                    this.queue.Enqueue(obj);
                }
            }

            public IEnumerable<T> TakeAll()
            {
                var list = new List<T>(this.queue.Count);
                while (this.queue.Count > 0)
                {
                    list.Add(this.queue.Dequeue());
                }

                return list;
            }
        }
    }
}
