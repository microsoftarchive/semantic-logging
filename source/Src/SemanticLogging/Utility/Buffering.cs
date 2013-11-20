// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Utility
{
    /// <summary>
    /// Buffering constants.
    /// </summary>
    public static class Buffering
    {
        /// <summary>
        /// The default buffering count.
        /// </summary>
        public const int DefaultBufferingCount = 1000;

        /// <summary>
        /// The default buffering interval.
        /// </summary>
        public static readonly TimeSpan DefaultBufferingInterval = TimeSpan.FromSeconds(30);

        /// <summary>
        /// The maximum number of entries that can be buffered while it's sending to database before the sink starts dropping entries.
        /// </summary>
        public const int DefaultMaxBufferSize = 30000;
    }
}