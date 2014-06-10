// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.Tracing;
using System.Linq;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Schema;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.TestSupport
{
    internal static class EventEntryTestHelper
    {
        public static EventEntry Create(
            int eventId = 0,
            Guid providerId = default(Guid),
            string providerName = null,
            EventLevel level = default(EventLevel),
            EventTask task = default(EventTask),
            string taskName = null,
            EventOpcode opcode = default(EventOpcode),
            string opcodeName = null,
            EventKeywords keywords = default(EventKeywords),
            string keywordsDescription = null,
            int version = 0,
            IEnumerable<string> payloadNames = null,
            string formattedMessage = null,
            IEnumerable<object> payload = null,
            DateTimeOffset timestamp = default(DateTimeOffset),
            Guid activityId = default(Guid),
            Guid relatedActivityId = default(Guid),
            int processId = 0,
            int threadId = 0)
        {
            return
                new EventEntry(
                    providerId,
                    eventId,
                    formattedMessage,
                    new ReadOnlyCollection<object>((payload ?? Enumerable.Empty<object>()).ToList()),
                    timestamp != default(DateTimeOffset) ? timestamp : DateTimeOffset.UtcNow,
                    processId,
                    threadId,
                    activityId,
                    relatedActivityId,
                    new EventSchema(
                        eventId, 
                        providerId, 
                        providerName, 
                        level, 
                        task, 
                        taskName, 
                        opcode, 
                        opcodeName,
                        keywords, 
                        keywordsDescription, 
                        version, 
                        (payloadNames ?? Enumerable.Empty<string>())));
        }
    }
}
