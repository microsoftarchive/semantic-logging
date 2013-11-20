// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw
{
    /// <summary>
    /// The status of this service instance. 
    /// </summary>
    public enum ServiceStatus
    {
        /// <summary>
        /// The service was not started yet.
        /// </summary>
        NotStarted,

        /// <summary>
        /// The service has started.
        /// </summary>
        Started,

        /// <summary>
        /// The service is stopping.
        /// </summary>
        Stopping,

        /// <summary>
        /// The service was stopped.
        /// </summary>
        Stopped,

        /// <summary>
        /// The service was disposed.
        /// </summary>
        Disposed,

        /// <summary>
        /// The service has faulted.
        /// </summary>
        Faulted,
    }
}
