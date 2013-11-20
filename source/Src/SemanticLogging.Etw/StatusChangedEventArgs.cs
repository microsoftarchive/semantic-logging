// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw
{
    /// <summary>
    /// Provides data for <see cref="TraceEventService.StatusChanged"/> event.
    /// </summary>
    public class StatusChangedEventArgs : EventArgs
    {
        internal StatusChangedEventArgs(ServiceStatus status)
        {
            this.Status = status;
        }

        /// <summary>
        /// Gets the changed status.
        /// </summary>
        /// <value>
        /// The status.
        /// </value>
        public ServiceStatus Status { get; private set; }
    }
}
