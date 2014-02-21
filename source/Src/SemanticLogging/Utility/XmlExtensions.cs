// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Utility
{
    using System;
    using System.Threading;
    using System.Xml.Linq;

    public static class XmlExtensions
    {
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
