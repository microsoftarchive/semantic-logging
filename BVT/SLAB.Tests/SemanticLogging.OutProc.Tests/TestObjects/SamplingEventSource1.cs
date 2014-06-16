using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.OutProc.Tests.TestObjects
{
    [EventSource(Name = "SamplingEventSource1")]
    public class SamplingEventSource1 : EventSource
    {
        public static SamplingEventSource1 Logger = new SamplingEventSource1();

        [Event(1, Level = EventLevel.Informational)]
        public void EventToSampleFromOtherSource(string message)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(1, message);
            }
        }
    }
}
