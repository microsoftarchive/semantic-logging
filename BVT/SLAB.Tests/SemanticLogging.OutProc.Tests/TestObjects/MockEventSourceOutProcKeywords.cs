// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System.Diagnostics.Tracing;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.OutProc.Tests.TestObjects
{
    [EventSource(Name = "MockEventSourceOutProcKeywords")]
    public sealed class MockEventSourceOutProcKeywords : EventSource
    {
        public static readonly MockEventSourceOutProcKeywords Logger = new MockEventSourceOutProcKeywords();

        [Event(1, Level = EventLevel.Informational, Keywords = Keywords.Page)]
        public void InformationalPage(string message)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(1, message); 
            }
        }

        [Event(2, Level = EventLevel.Informational, Keywords = Keywords.Database)]
        public void InformationalDatabase(string message)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(2, message); 
            }
        }

        [Event(3, Level = EventLevel.Informational, Keywords = Keywords.Diagnostic)]
        public void InformationalDiagnostic(string message)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(3, message); 
            }
        }

        public class Keywords
        {
            public const EventKeywords Page = (EventKeywords)1;
            public const EventKeywords Database = (EventKeywords)2;
            public const EventKeywords Diagnostic = (EventKeywords)4;
            public const EventKeywords Perf = (EventKeywords)8;
        }
    }
}
