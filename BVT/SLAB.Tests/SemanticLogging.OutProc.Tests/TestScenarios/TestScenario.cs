using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw.Configuration;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.Shared.TestSupport;
using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.OutProc.Tests.TestScenarios
{
    internal static class TestScenario
    {
        public static void WithConfiguration(TraceEventServiceConfiguration svcConfiguration, Action scenario)
        {
            using (TraceEventService collector = new TraceEventService(svcConfiguration))
            {
                collector.Start();
                try
                {
                    scenario();
                }
                finally
                {
                    collector.Stop();
                }
            }
        }

        public static void WithTempUpdatesInConfiguration(string serviceConfigFile, Func<string, string> updateConfiguration, Action scenario)
        {
            FlatFileHelper.DeleteDirectory(@".\Logs");
            string xmlContent = File.ReadAllText(serviceConfigFile);
            var xmlContentRepl = updateConfiguration(xmlContent);
            try
            {
                File.WriteAllText(serviceConfigFile, xmlContentRepl);
                var svcConfiguration = TraceEventServiceConfiguration.Load(serviceConfigFile);
                TestScenario.WithConfiguration(
                    svcConfiguration,
                    () =>
                    {
                        scenario();
                    });
            }
            finally
            {
                File.WriteAllText(serviceConfigFile, xmlContent);
            }
        }
    }
}
