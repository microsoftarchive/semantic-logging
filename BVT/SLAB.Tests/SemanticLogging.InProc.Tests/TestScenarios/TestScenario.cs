// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.Shared.TestObjects;
using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.InProc.Tests.TestScenarios
{
    internal static class TestScenario
    {
        public static void With1Listener(EventSource logger, Action<ObservableEventListener> scenario)
        {
            With1Listener(new[] { logger }, scenario);
        }

        public static void With1Listener(EventSource logger, Action<ObservableEventListener, InMemoryEventListener> scenario)
        {
            With1Listener(new[] { logger }, scenario);
        }

        public static void With1Listener(IEnumerable<EventSource> loggers, Action<ObservableEventListener> scenario)
        {
            With1Listener(loggers, (listener, notUsedErrorListener) => scenario(listener));
        }

        public static void With1Listener(IEnumerable<EventSource> loggers, Action<ObservableEventListener, InMemoryEventListener> scenario)
        {
            using (var errorsListener = new InMemoryEventListener())
            using (var listener = new ObservableEventListener())
            {
                try
                {
                    errorsListener.EnableEvents(SemanticLoggingEventSource.Log, EventLevel.Verbose, Keywords.All);

                    scenario(listener, errorsListener);
                }
                finally
                {
                    foreach (var logger in loggers)
                    {
                        try
                        { listener.DisableEvents(logger); }
                        catch
                        { }
                    }

                    errorsListener.DisableEvents(SemanticLoggingEventSource.Log);
                }
            }
        }

        public static void With2Listeners(EventSource logger, Action<ObservableEventListener, ObservableEventListener> scenario)
        {
            using (var listener1 = new ObservableEventListener())
            using (var listener2 = new ObservableEventListener())
            {
                try
                {
                    scenario(listener1, listener2);
                }
                finally
                {
                    try
                    { listener1.DisableEvents(logger); }
                    catch
                    { }

                    try
                    { listener2.DisableEvents(logger); }
                    catch
                    { }
                }
            }
        }
    }
}
