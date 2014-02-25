// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Sinks;
using System;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.InProc.Tests.TestObjects
{
    public class MockDateTimeProvider : RollingFlatFileSink.DateTimeProvider
    {
        public DateTime? OverrideCurrentDateTime = null;

        public override DateTime CurrentDateTime
        {
            get
            {
                if (this.OverrideCurrentDateTime != null)
                {
                    return this.OverrideCurrentDateTime.Value; 
                }

                return base.CurrentDateTime;
            }
        }
    }
}
