// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System.Diagnostics.Tracing;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.OutProc.Tests.TestObjects
{
    [EventSource(Name = "MockEventSourceOutProcFiltering")]
    public sealed class MockEventSourceOutProcFiltering : EventSource
    {
        public static readonly MockEventSourceOutProcFiltering Logger = new MockEventSourceOutProcFiltering();

        [Event(1, Level = EventLevel.Informational)]
        public void Informational(string message)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(1, message); 
            }
        }

        [Event(2, Level = EventLevel.Verbose)]
        public void Verbose(string message)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(2, message); 
            }
        }

        [Event(3, Level = EventLevel.Critical)]
        public void Critical(string message)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(3, message); 
            }
        }

        [Event(4, Level = EventLevel.Error)]
        public void Error(string message)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(4, message); 
            }
        }

        [Event(5, Level = EventLevel.Warning)]
        public void Warning(string message)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(5, message); 
            }
        }
    }
}
