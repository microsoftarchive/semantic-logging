using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.Shared.TestSupport
{
    public static class ThreadHelper
    {
        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        public static int GetCurrentUnManagedThreadId()
        {
            return (int)GetCurrentThreadId();
        }
    }
}
