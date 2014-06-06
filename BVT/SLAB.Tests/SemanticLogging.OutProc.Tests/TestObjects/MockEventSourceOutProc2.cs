// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System.Diagnostics.Tracing;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.OutProc.Tests.TestObjects
{
    [EventSource(Name = "MockEventSourceOutProc2")]
    public sealed class MockEventSourceOutProc2 : EventSource
    {
        public static readonly MockEventSourceOutProc2 Logger = new MockEventSourceOutProc2();

        [Event(8, Level = EventLevel.Informational)]
        public void LogSomeMessage(string message)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(8, message); 
            }
        }
    }
}
