// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Sinks
{
    /// <summary>
    /// Defines the behavior when the roll file is created.
    /// </summary>
    public enum RollFileExistsBehavior
    {
        /// <summary>
        /// Overwrites the file if it already exists.
        /// </summary>
        Overwrite,

        /// <summary>
        /// Use a sequence number at the end of the generated file if it already exists.
        /// </summary>
        /// <remarks>
        /// If it fails again then increment the sequence until a non existent filename is found.
        /// </remarks>
        Increment
    }
}
