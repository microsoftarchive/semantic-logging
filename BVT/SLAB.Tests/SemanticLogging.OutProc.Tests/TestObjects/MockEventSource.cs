// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System.Diagnostics.Tracing;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.OutProc.Tests.TestObjects
{
    [EventSource(Name = "TestEventSource")]
    public sealed class MockEventSource : EventSource
    {
        public const int ErrorWithKeywordDiagnosticEventId = 1020;
        public const int CriticalWithKeywordPageEventId = 1021;
        public const int InfoWithKeywordDiagnosticEventId = 1022;
        public const int VerboseWithKeywordPageEventId = 1023;
        public const int CriticalWithTaskNameEventId = 1500;

        public static readonly MockEventSource Logger = new MockEventSource();

        public class Keywords
        {
            public const EventKeywords Page = (EventKeywords)1;
            public const EventKeywords Diagnostic = (EventKeywords)4;
        }

        public class Tasks
        {
            public const EventTask Page = (EventTask)1;
            public const EventTask DBQuery = (EventTask)2;
        }
    }
}
