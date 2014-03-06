// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Xml.Linq;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Utility
{
    /// <summary>
    /// Xml extensions for configuration extensibility support
    /// </summary>
    public static class XmlExtensions
    {
        /// <summary>
        /// XAttribute configuration extension to convert seconds to a TimeSpan format
        /// </summary>
        /// <param name="attribute"></param>
        /// <returns></returns>
        public static TimeSpan? ToTimeSpan(this XAttribute attribute)
        {
            int? bufferingIntervalInSeconds = (int?)attribute;
            if (!bufferingIntervalInSeconds.HasValue)
            {
                return (TimeSpan?)null;
            }

            return bufferingIntervalInSeconds.Value == -1 ? Timeout.InfiniteTimeSpan : TimeSpan.FromSeconds(bufferingIntervalInSeconds.Value);
        }
    }
}
