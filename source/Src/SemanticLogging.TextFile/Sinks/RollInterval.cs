// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Sinks
{
    /// <summary>
    /// Defines the frequency when the file need to be rolled.
    /// </summary>
    public enum RollInterval
    {
        /// <summary>
        /// None Interval.
        /// </summary>
        None,

        /// <summary>
        /// Minute Interval.
        /// </summary>
        Minute,

        /// <summary>
        /// Hour interval.
        /// </summary>
        Hour,

        /// <summary>
        /// Day Interval.
        /// </summary>
        Day,

        /// <summary>
        /// Week Interval.
        /// </summary>
        Week,

        /// <summary>
        /// Month Interval.
        /// </summary>
        Month,

        /// <summary>
        /// Year Interval.
        /// </summary>
        Year,

        /// <summary>
        /// At Midnight.
        /// </summary>
        Midnight
    }
}
