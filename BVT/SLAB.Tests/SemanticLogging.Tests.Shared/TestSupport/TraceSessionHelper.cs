// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Session;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.Shared.TestSupport
{
    public class TraceSessionHelper
    {
        public static bool WaitAndAssertCountOfSessions(string sessionNamePrefix, int countOfSessions)
        {
            var allSessionsHaveStarted = PollUntil(sessionNames => sessionNames.Count(name => name.StartsWith(sessionNamePrefix)) == countOfSessions);
            if (allSessionsHaveStarted)
            {
                return true;
            }

            throw new TimeoutException(countOfSessions + " sessions with the prefix '" + sessionNamePrefix + "' have not started within time. These are the active sessions: " + string.Join(", ", TraceEventSession.GetActiveSessionNames()));
        }

        private static bool PollUntil(Func<IEnumerable<string>, bool> pollUntilCondition)
        {
            var timeoutToWaitUntilEventIsReceived = DateTime.UtcNow.AddSeconds(10);
            while (DateTime.UtcNow < timeoutToWaitUntilEventIsReceived)
            {
                var activeSessionNames = TraceEventSession.GetActiveSessionNames();
                if (pollUntilCondition(activeSessionNames))
                {
                    return true;
                }

                Task.Delay(200).Wait();
            }

            return false;
        }
    }
}
