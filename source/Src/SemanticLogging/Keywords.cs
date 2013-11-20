// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System.Diagnostics.Tracing;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging
{
    /// <summary>
    /// EventKeywords additional constants for <see cref="System.Diagnostics.Tracing.EventKeywords"/>.
    /// </summary>
    public static class Keywords
    {
        /// <summary>
        /// Keyword flags to enable all the events. 
        /// </summary>
        public const EventKeywords All = (EventKeywords)(-1);
    }
}
