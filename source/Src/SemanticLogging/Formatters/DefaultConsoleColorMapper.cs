// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Diagnostics.Tracing;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Formatters
{
    /// <summary>
    /// Default console color mapper class.
    /// </summary>
    public class DefaultConsoleColorMapper : IConsoleColorMapper
    {
        /// <summary>
        /// Default color value for when displaying events of level <see cref="EventLevel.LogAlways"/>.
        /// </summary>
        public const ConsoleColor LogAlways = ConsoleColor.White;

        /// <summary>
        /// Default color value for when displaying events of level <see cref="EventLevel.Critical"/>.
        /// </summary>
        public const ConsoleColor Critical = ConsoleColor.Magenta;

        /// <summary>
        /// Default color value for when displaying events of level <see cref="EventLevel.Error"/>.
        /// </summary>
        public const ConsoleColor Error = ConsoleColor.Red;

        /// <summary>
        /// Default color value for when displaying events of level <see cref="EventLevel.Warning"/>.
        /// </summary>
        public const ConsoleColor Warning = ConsoleColor.Yellow;

        /// <summary>
        /// Default color value for when displaying events of level <see cref="EventLevel.Verbose"/>.
        /// </summary>
        public const ConsoleColor Verbose = ConsoleColor.Green;

        /// <summary>
        /// Default color value for when displaying events of level <see cref="EventLevel.Informational"/>.
        /// </summary>
        public const ConsoleColor Informational = ConsoleColor.Gray;

        /// <summary>
        /// Maps the specified <see cref="System.Diagnostics.Tracing.EventLevel"/> to a <see cref="System.ConsoleColor"/>.
        /// </summary>
        /// <param name="eventLevel">The event level.</param>
        /// <returns>The console color.</returns>
        public virtual ConsoleColor? Map(EventLevel eventLevel)
        {
            switch (eventLevel)
            {
                case EventLevel.Critical:
                    return Critical;
                case EventLevel.Error:
                    return Error;
                case EventLevel.Warning:
                    return Warning;
                case EventLevel.Verbose:
                    return Verbose;
                case EventLevel.LogAlways:
                    return LogAlways;
                case EventLevel.Informational:
                    return Informational;
                default:
                    return null;
            }
        }
    }
}
