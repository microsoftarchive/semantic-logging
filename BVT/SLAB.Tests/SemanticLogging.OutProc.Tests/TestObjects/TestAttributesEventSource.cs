// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System.Diagnostics.Tracing;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.OutProc.Tests.TestObjects
{
    [EventSource(Name = "TestProvideraaa", Guid = "B4F8149D-6DD2-4EE2-A46A-45584A942D1C")]
    public class TestAttributesEventSource : EventSource
    {
        public static readonly TestAttributesEventSource Logger = new TestAttributesEventSource();

        [Event(105)]
        public void NoTaskSpecfied2(int arg1, int arg2, int arg3)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(105, arg1, arg3, arg3);
            }
        }
    }
}
