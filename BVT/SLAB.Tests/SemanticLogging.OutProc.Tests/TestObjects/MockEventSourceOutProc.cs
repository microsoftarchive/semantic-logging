// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System.Diagnostics.Tracing;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.OutProc.Tests.TestObjects
{
    [EventSource(Name = "MockEventSourceOutProc")]
    public sealed class MockEventSourceOutProc : EventSource
    {
        public static readonly MockEventSourceOutProc Logger = new MockEventSourceOutProc();

        [Event(2, Level = EventLevel.Critical)]
        public void Critical(string message)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(2, message); 
            }
        }

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