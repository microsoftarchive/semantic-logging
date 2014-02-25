// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System.Diagnostics.Tracing;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.InProc.Tests.TestObjects
{
    public class BasicTestEventSource : EventSource
    {
        public static readonly BasicTestEventSource Logger = new BasicTestEventSource();

        [Event(100, Level = EventLevel.LogAlways)]
        public void RaiseBasicTestEventSourceEvent(string message) 
        {
            if (this.IsEnabled())
            { 
                this.WriteEvent(100, message); 
            }
        }

        [Event(200, Level = EventLevel.Error)]
        public void RaiseEventWithMaxVerbosityAsError(string message) 
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(200, message); 
            }
        }

        [Event(300, Level = EventLevel.Informational)]
        public void RaiseEventWithMaxVerbosityAsInformational(string message) 
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(300, message);
            }
        }

        [Event(400, Level = EventLevel.Critical)]
        public void RaiseEventWithMaxVerbosityAsCritical(string message) 
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(400, message);
            }
        }
    }
}
