// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Diagnostics.Tracing;

namespace QuickStart
{
    [EventSource(Name = "RxFloodQuickStart")]
    public class RxFloodQuickStartEventSource : EventSource
    {
        private static readonly Lazy<RxFloodQuickStartEventSource> log = new Lazy<RxFloodQuickStartEventSource>(() => new RxFloodQuickStartEventSource());

        public static RxFloodQuickStartEventSource Log
        {
            get { return log.Value; }
        }

        [Event(4, Level = EventLevel.Error)]
        internal void UnknownError(string errorMessage)
        {
            if (this.IsEnabled()) this.WriteEvent(4, errorMessage);
        }

        [Event(5, Level = EventLevel.Error)]
        internal void UpdateAccountBalanceFailed(int customerId, string errorMessage)
        {
            if (this.IsEnabled()) this.WriteEvent(5, customerId, errorMessage);
        }

        [Event(6, Level = EventLevel.Informational, Message = "Updating account balance for customer with id {0}.")]
        internal void UpdatingAccountBalance(int customerId)
        {
            if (this.IsEnabled()) this.WriteEvent(6, customerId);
        }
    }
}
