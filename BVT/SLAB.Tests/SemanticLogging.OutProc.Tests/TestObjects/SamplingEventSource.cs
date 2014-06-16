using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.OutProc.Tests.TestObjects
{
    [EventSource(Name = "SamplingEventSource")]
    public class SamplingEventSource : EventSource
    {
        public static SamplingEventSource Logger = new SamplingEventSource();

        [Event(1, Level = EventLevel.Informational)]
        public void EventToSample(string message)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(1, message);
            }
        }

        [Event(2, Level = EventLevel.Informational)]
        public void BeforeEventToSample(string message)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(2, message);
            }
        }

        [Event(3, Level = EventLevel.Informational)]
        public void AfterEventToSample(string message)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(3, message);
            }
        }

        [Event(4, Level = EventLevel.Informational)]
        public void EventInATask(string message)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(4, message);
            }
        }
    }
}
