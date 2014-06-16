using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.OutProc.Tests.TestObjects;

namespace ProcessToSample
{
    internal class Program
    {
        private static ManualResetEvent waitObject = new ManualResetEvent(false);
        private static void Main(string[] args)
        {
            var oldActivityId = EventSource.CurrentThreadActivityId;
            Console.Read();

            EventSource.SetCurrentThreadActivityId(new Guid("FBA40C13-6725-42A7-92F2-47EEA6E1AD5B"));
            TriggerEventSource.Logger.TriggerEvent("Trigger event from process");
            Task.Run(async () => await LogEventsAsync());
            waitObject.WaitOne();
            EventSource.SetCurrentThreadActivityId(oldActivityId);
        }

        public static async Task LogEventsAsync()
        {
            SamplingEventSource.Logger.BeforeEventToSample("Message 1 from process");
            await Task.Delay(10);
            SamplingEventSource.Logger.AfterEventToSample("Message 2 from process");
            waitObject.Set();
        }
    }
}
