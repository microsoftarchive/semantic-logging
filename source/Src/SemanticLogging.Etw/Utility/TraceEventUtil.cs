// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Session;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw.Utility
{
    internal static class TraceEventUtil
    {
        internal const TraceEventID ManifestEventID = (TraceEventID)0xFFFE;

        internal static TraceEventSession CreateSession(string sessionName)
        {
            if (TraceEventSession.GetActiveSessionNames().Contains(sessionName, StringComparer.OrdinalIgnoreCase))
            {
                // Notify of session removal
                SemanticLoggingEventSource.Log.TraceEventServiceSessionRemoved(sessionName);

                // Remove existing session
                new TraceEventSession(sessionName) { StopOnDispose = true }.Dispose();
            }

            return new TraceEventSession(sessionName, null) { StopOnDispose = true };
        }

        internal static void EnableProvider(TraceEventSession session, Guid providerId, EventLevel level, EventKeywords matchAnyKeyword, bool sendManifest = true)
        {
            // Make explicit the invocation for requesting the manifest from the EventSource (Provider).
            var values = sendManifest ? new Dictionary<string, string>() { { "Command", "SendManifest" } } : null;
            var options =
                new TraceEventProviderOptions
                {
                    Arguments = values
                };

            session.EnableProvider(providerId, (TraceEventLevel)level, (ulong)matchAnyKeyword, options);
        }
    }
}
