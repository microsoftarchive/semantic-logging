// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics.Tracing;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Schema;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.TestObjects
{
    public class MockEventListener : EventListener
    {
        public ConcurrentBag<EventEntry> WrittenEntries = new ConcurrentBag<EventEntry>();

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            WrittenEntries.Add(EventEntry.Create(eventData, EventSourceSchemaCache.Instance.GetSchema(eventData.EventId, eventData.EventSource)));
        }
    }
}
