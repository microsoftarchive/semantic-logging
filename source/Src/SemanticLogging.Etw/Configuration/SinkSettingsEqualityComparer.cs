// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Utility;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw.Configuration
{
    internal class SinkSettingsEqualityComparer : IEqualityComparer<SinkSettings>
    {
        public bool Equals(SinkSettings x, SinkSettings y)
        {
            if (x == null || y == null)
            {
                return false;
            }

            return x.Name == y.Name;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1062:Validate arguments of public methods", MessageId = "0", Justification = "Validated with Guard class")]
        public int GetHashCode(SinkSettings obj)
        {
            Guard.ArgumentNotNull(obj, "obj");

            return obj.Name.GetHashCode();
        }
    }
}
