// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Linq;
using Microsoft.WindowsAzure.ServiceRuntime;

namespace SlabReconfigurationWebRole
{
    public class WebRole : RoleEntryPoint
    {
        public override bool OnStart()
        {
            Trace.TraceInformation("Initializing role");

            RoleEnvironment.Changing += RoleEnvironment_Changing;

            return base.OnStart();
        }

        public override void Run()
        {
            Trace.TraceInformation("Running role");

            base.Run();
        }

        void RoleEnvironment_Changing(object sender, RoleEnvironmentChangingEventArgs e)
        {
            Trace.TraceInformation("Change notification");

            if (e.Changes.OfType<RoleEnvironmentConfigurationSettingChange>()
                .Any(c => !string.Equals(c.ConfigurationSettingName, MvcApplication.AzureLoggingVerbositySettingName, StringComparison.Ordinal)))
            {
                Trace.TraceInformation("Cancelling instance");
                e.Cancel = true;
            }
            else
            {
                Trace.TraceInformation("Handling change without recycle");
            }
        }
    }
}
