using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.OutProc.Tests.TestObjects
{
    public sealed class TriggerEventSource : EventSource
    {
        public static TriggerEventSource Logger = new TriggerEventSource();

        [Event(1, Level = EventLevel.Informational)]
        public void TriggerEvent(string message)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(1, message);
            }
        }
    }
}
