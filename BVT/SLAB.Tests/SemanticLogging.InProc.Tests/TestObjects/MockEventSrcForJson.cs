// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System.Diagnostics.Tracing;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.InProc.Tests.TestObjects
{
    public class MockEventSrcForJson : EventSource
    {
        public const int UsingKeywordsEventID = 1;
        public const int LogUsingMessageEventID = 2;
        public const string LogMessage = @" Test Message";

        public static readonly MockEventSrcForJson Logger = new MockEventSrcForJson();

        public class Keywords
        {
            public const EventKeywords Errors = (EventKeywords)0x0001;
            public const EventKeywords Trace = (EventKeywords)0x0002;
        }

        [Event(UsingKeywordsEventID, Level = EventLevel.Informational, Opcode = EventOpcode.Start, Keywords = Keywords.Errors)]
        public void UsingKeywords(string message, long longArg)
        {
            if (this.IsEnabled(EventLevel.Informational, Keywords.Errors))
            {
                this.WriteEvent(UsingKeywordsEventID, message, longArg);
            }
        }

        [Event(LogUsingMessageEventID, Level = EventLevel.Informational, Opcode = EventOpcode.Start, Message = LogMessage)]
        public void LogUsingMessage(string message)
        {
            if (this.IsEnabled(EventLevel.Informational, Keywords.Errors))
            {
                this.WriteEvent(LogUsingMessageEventID, message);
            }
        }
    }
}
