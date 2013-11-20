// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System.ComponentModel;
using System.Configuration.Install;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw.Service.Properties;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw.Service
{
    /// <summary>
    /// The installer class for <see cref="TraceEventServiceHost"/>.
    /// </summary>
    [RunInstaller(true)]
    public partial class TraceEventServiceHostInstaller : Installer
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TraceEventServiceHostInstaller" /> class.
        /// </summary>
        public TraceEventServiceHostInstaller()
        {
            this.Initialize();
        }

        private void Initialize()
        {
            var serviceProcessInstaller = new ServiceProcessInstaller()
            {
                Account = ServiceAccount.LocalService
            };

            var serviceInstaller = new ServiceInstaller()
            {
                ServiceName = Constants.ServiceName,
                Description = Resources.ServiceDescription,
                DisplayName = Resources.ServiceDisplayName,
                StartType = ServiceStartMode.Manual
            };

            this.Installers.AddRange(new Installer[] { serviceProcessInstaller, serviceInstaller });
        }
    }
}
