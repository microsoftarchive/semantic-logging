// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.TestObjects
{
    public class TestEventEntry
    {
        public int EventId { get; set; }

        public Guid ProviderId { get; set; }

        public string EventSourceName { get; set; }

        public string Message { get; set; }

        public long EventKeywords { get; set; }

        public int Level { get; set; }

        public int Opcode { get; set; }

        public int Task { get; set; }

        public int Version { get; set; }

        public Dictionary<string, object> Payload { get; set; }

        public DateTimeOffset Timestamp { get; set; }
    }
}
