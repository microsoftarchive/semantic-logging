// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Configuration
{
    /// <summary>
    /// Configuration constants and default values.
    /// </summary>
    public static class Constants
    {
        /// <summary>
        /// The configuration namespace.
        /// </summary>
        public const string Namespace = "http://schemas.microsoft.com/practices/2013/entlib/semanticlogging/etw";

        /// <summary>
        /// The default session name prefix.
        /// </summary>
        public const string DefaultSessionNamePrefix = "Microsoft-SemanticLogging-Etw";

        /// <summary>
        /// The default max timeout for flushing all pending events in the buffer.
        /// </summary>
        public static readonly TimeSpan DefaultBufferingFlushAllTimeout = TimeSpan.FromSeconds(5);
    }
}
