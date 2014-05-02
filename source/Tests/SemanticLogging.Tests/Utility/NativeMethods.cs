// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System.Runtime.InteropServices;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.Utility
{
    internal static class NativeMethods
    {
        [DllImport("kernel32.dll")]
        public static extern int GetCurrentThreadId();
    }
}
