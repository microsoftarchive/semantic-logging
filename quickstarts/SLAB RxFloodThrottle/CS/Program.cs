// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Data;
using System.Diagnostics.Tracing;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Formatters;

namespace QuickStart
{
    class Program
    {
        private const int ThrottledEventId = 4;

        // this formatter makes all entries with a level higher than Critical to be single-line, so several entries fit in the console screen for the demo.
        private static readonly IEventTextFormatter SingleLineFormatter = new EventTextFormatter(verbosityThreshold: EventLevel.Critical);

        static void Main(string[] args)
        {
            var listener = new ObservableEventListener();
            listener.EnableEvents(RxFloodQuickStartEventSource.Log, EventLevel.LogAlways, Keywords.All);

            // ThrottleEventsWithEventId is a custom extension method that shows how you can leverage the power of Reactive Extensions (Rx) 
            // to perform filtering (or transformation) of the event stream before it is sent to the underlying sink.
            // In this case, ThrottleEventsWithEventId will throttle entries with EventID=4 and mute additional occurrences for 15 seconds.
            // This prevents a particular event from flooding the log sink, making it difficult to diagnose other issues.
            // This can be useful in the case that a high-throughput event does not have a keyword or verbosity setting that makes it easy
            // to exclude it in the call to listener.EnableEvents(EventSource, EventLevel, EventKeywords).

            // Note: For basic scenarios without this extra filtering, you DO NOT need to use Rx, and SLAB does not depend on it.

            var subscription = listener
                                .ThrottleEventsWithEventId(TimeSpan.FromSeconds(15), ThrottledEventId)
                                .LogToConsole(SingleLineFormatter);

            // The previous custom extension method (ThrottleEventsWithEventId) is all that is needed to call to throttle
            // an event that is flooding the log. 
            // The rest of the code in this QuickStart is here to show an interactive demo of how it looks if this filter is turned on or off.
            bool currentlyThrottling = true;

            var cts = new CancellationTokenSource();

            Console.WriteLine("This program simulates the scenario of a particular event being logged multiple times in succession when a certain condition occurs,");
            Console.WriteLine("such as when there is a transient or expected connectivity error during system upgrades.");
            Console.WriteLine();
            Console.WriteLine("While the application is logging messages, use the following commands:");
            Console.WriteLine(" [ESC]      Exists the application.");
            Console.WriteLine(" [Spacebar] Toggles the throttling filter.");
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine("Press any key to start doing background work.");

            var key = Console.ReadKey(false);
            if (key.Key == ConsoleKey.Escape)
            {
                return;
            }

            DoBackgroundWork(cts.Token);

            while (!cts.IsCancellationRequested)
            {
                key = Console.ReadKey(false);
                switch (key.Key)
                {
                    case ConsoleKey.Spacebar:
                        subscription.Dispose();
                        if (currentlyThrottling)
                        {
                            Console.WriteLine("Filter toggled: event entries will not be throttled. In this scenario, if there is no post-filtering of events, important messages could go unnoticed.");
                            Thread.Sleep(TimeSpan.FromSeconds(3));
                            currentlyThrottling = false;

                            // Note that the events are sent directly to the console, without using Reactive Extensions.
                            subscription = listener
                                                .LogToConsole(SingleLineFormatter);
                        }
                        else
                        {
                            Console.WriteLine("Filter toggled: event entries with ID {0} will be throttled for 15 seconds to prevent that type of entry to flood the log.", ThrottledEventId);
                            Thread.Sleep(TimeSpan.FromSeconds(3));
                            currentlyThrottling = true;

                            // Note that the events are filtered first and then sent to the console, using Reactive Extensions.
                            subscription = listener
                                                .ThrottleEventsWithEventId(TimeSpan.FromSeconds(15), ThrottledEventId)
                                                .LogToConsole(SingleLineFormatter);
                        }
                        
                        break;

                    case ConsoleKey.Escape:
                        cts.Cancel();
                        break;
                }
            }

            listener.Dispose();
        }


        private static void DoBackgroundWork(CancellationToken token)
        {
            Task.Run(() => RefreshDisplayData(token)).ContinueWith(t => { }, TaskContinuationOptions.OnlyOnCanceled);
            Task.Run(() => DoImportantWork(token)).ContinueWith(t => { }, TaskContinuationOptions.OnlyOnCanceled);
        }

        private static void RefreshDisplayData(CancellationToken token)
        {
            // simulate very frequent errors that could flood an unfiltered log sink.
            Thread.Sleep(TimeSpan.FromSeconds(1));
            var random = new Random();

            while (!token.IsCancellationRequested)
            {
                try
                {
                    throw new WebException("Error connecting to the back-end service.");
                }
                catch (Exception ex)
                {
                    RxFloodQuickStartEventSource.Log.UnknownError(ex.ToString());
                }

                // sleep between poll retries.
                Thread.Sleep(TimeSpan.FromMilliseconds(random.Next(1500)));
            }
        }

        private static void DoImportantWork(CancellationToken token)
        {
            Thread.Sleep(TimeSpan.FromSeconds(2));
            var random = new Random();

            while (!token.IsCancellationRequested)
            {
                int customerId = random.Next(100000);

                try
                {
                    // simulate very infrequent important errors that might get "lost" (or go unnoticed) in a flooded log file.
                    RxFloodQuickStartEventSource.Log.UpdatingAccountBalance(customerId);

                    if (random.Next(100) < 15)
                    {
                        throw new DataException("Error updating account balance information. Something unexpected happened and needs to be fixed. Do not ignore this error!");
                    }
                }
                catch (Exception ex)
                {
                    RxFloodQuickStartEventSource.Log.UpdateAccountBalanceFailed(customerId, ex.ToString());
                }

                // sleep between work iterations.
                Thread.Sleep(TimeSpan.FromSeconds(5));
            }
        }
    }
}
