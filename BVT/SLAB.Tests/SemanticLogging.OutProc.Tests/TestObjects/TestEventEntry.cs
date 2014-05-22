// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.OutProc.Tests.TestObjects
{
    public class TestEventEntry
    {
        public int EventId { get; set; }

        public Guid ProviderId { get; set; }

        public string ProviderName { get; set; }

        public string Message { get; set; }

        public EventKeywords EventKeywords { get; set; }

        public EventLevel Level { get; set; }

        public EventOpcode Opcode { get; set; }

        public EventTask Task { get; set; }

        public byte Version { get; set; }

        public Dictionary<string, object> Payload { get; set; }

        public DateTimeOffset Timestamp { get; set; }

        public int ProcessId { get; set; }

        public int ThreadId { get; set; }
    }

    public class TestEventEntryCustomTimeStamp
    {
        public int EventId { get; set; }

        public Guid ProviderId { get; set; }

        public string ProviderName { get; set; }

        public string Message { get; set; }

        public EventKeywords EventKeywords { get; set; }

        public EventLevel Level { get; set; }

        public EventOpcode Opcode { get; set; }

        public EventTask Task { get; set; }

        public byte Version { get; set; }

        public Dictionary<string, object> Payload { get; set; }

        public string Timestamp { get; set; }

        public int ProcessId { get; set; }

        public int ThreadId { get; set; }
    }
}
