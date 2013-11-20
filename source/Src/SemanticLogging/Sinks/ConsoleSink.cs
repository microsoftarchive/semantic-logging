// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Sinks
{
    /// <summary>
    /// A sink that writes to the Console.
    /// </summary>
    /// <remarks>This class is thread-safe.</remarks>
    // TODO: validate: should it be Observer<Tuple<string, ConsoleColor?>> or should we create a custom class?
    public class ConsoleSink : IObserver<Tuple<string, ConsoleColor?>>
    {
        // lock on static because Console.Out is shared across all threads and sink instances
        private static readonly object LockObject = new object();

        /// <summary>
        /// Initializes a new instance of the <see cref="ConsoleSink" /> class.
        /// </summary>
        public ConsoleSink()
        {
        }

        /// <summary>
        /// Notifies the observer that the provider has finished sending push-based notifications.
        /// </summary>
        public void OnCompleted()
        {
        }

        /// <summary>
        /// Notifies the observer that the provider has experienced an error condition.
        /// </summary>
        /// <param name="error">An object that provides additional information about the error.</param>
        public void OnError(Exception error)
        {
        }

        /// <summary>
        /// Provides the sink with new data to write.
        /// </summary>
        /// <param name="value">The current entry and its color to write to the console.</param>
        public void OnNext(Tuple<string, ConsoleColor?> value)
        {
            if (value != null)
            {
                OnNext(value.Item1, value.Item2);
            }
        }

        private static void OnNext(string entry, ConsoleColor? color)
        {
            lock (LockObject)
            {
                ConsoleColor? currentColor = null;
                try
                {
                    if (color.HasValue)
                    {
                        currentColor = Console.ForegroundColor;
                        Console.ForegroundColor = color.Value;
                    }

                    Console.Out.Write(entry);
                    Console.Out.Flush();
                }
                catch (Exception e)
                {
                    SemanticLoggingEventSource.Log.ConsoleSinkWriteFailed(e.ToString());
                }
                finally
                {
                    if (currentColor.HasValue)
                    {
                        Console.ForegroundColor = currentColor.Value;
                    }
                }
            }
        }
    }
}
