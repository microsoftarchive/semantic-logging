// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Formatters
{
    /// <summary>
    /// Specifies formatting options .
    /// </summary>
    public enum EventTextFormatting
    {
        /// <summary>
        /// No special formatting applied. This is the default.
        /// </summary>
        None,

        /// <summary>
        /// Causes child objects to be indented.
        /// </summary>
        Indented
    }
}
