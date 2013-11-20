// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Diagnostics.Tracing;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Formatters
{
    /// <summary>
    /// Provides mapping between an <see cref="EventLevel"/> and a console foreground color.
    /// </summary>
    public interface IConsoleColorMapper
    {
        /// <summary>
        /// Maps the specified <see cref="System.Diagnostics.Tracing.EventLevel"/> to a <see cref="System.ConsoleColor"/>
        /// </summary>
        /// <param name="eventLevel">The <see cref="System.Diagnostics.Tracing.EventLevel"/>.</param>
        /// <returns>The <see cref="System.ConsoleColor"/>.</returns>
        ConsoleColor? Map(EventLevel eventLevel);
    }
}
