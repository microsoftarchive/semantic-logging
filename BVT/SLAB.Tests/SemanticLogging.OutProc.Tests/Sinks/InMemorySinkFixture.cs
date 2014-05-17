// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw.Configuration;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Formatters;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.OutProc.Tests.TestObjects;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.OutProc.Tests.TestScenarios;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.Shared.TestObjects;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.OutProc.Tests.Sinks
{
    [TestClass]
    public class InMemorySinkFixture
    {
        [TestMethod]
        public void WhenConcurrentEventsRaised()
        {
            var logger = MockEventSourceOutProc.Logger;
            EventTextFormatter formatter = new EventTextFormatter();
            EventSourceSettings settings = new EventSourceSettings("MockEventSourceOutProc", null, EventLevel.LogAlways);
            InMemorySink sink = new InMemorySink(formatter);

            SinkSettings sinkSettings = new SinkSettings("memorySink", sink, new List<EventSourceSettings>() { { settings } });
            List<SinkSettings> sinks = new List<SinkSettings>() { { sinkSettings } };
            TraceEventServiceConfiguration svcConfiguration = new TraceEventServiceConfiguration(sinks);
            TestScenario.WithConfiguration(
                svcConfiguration,
                () =>
                {
                    sink.WaitSignalCondition = () => sink.EventWrittenCount == 100;
                    for (int n = 0; n < 100; n++)
                    {
                        logger.LogSomeMessage("some message" + n.ToString());
                    }

                    sink.WaitOnAsyncEvents.WaitOne(TimeSpan.FromSeconds(10));
                });

            StringAssert.Contains(sink.ToString(), "some message99");
        }
    }
}
