// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.ComponentModel;
using System.Configuration.Install;
using System.Globalization;
using System.ServiceProcess;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw.Service.Properties;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw.Service
{
    /// <summary>
    /// The installer class for <see cref="TraceEventServiceHost"/>.
    /// </summary>
    [RunInstaller(true)]
    public class TraceEventServiceHostInstaller : Installer
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TraceEventServiceHostInstaller" /> class.
        /// </summary>
        public TraceEventServiceHostInstaller()
        {
            this.Initialize();
        }

        public ServiceProcessInstaller ServiceProcessInstaller { get; set; }

        protected override void OnBeforeInstall(IDictionary savedState)
        {
            this.UpdateServiceAccount();
            base.OnBeforeInstall(savedState);
        }

        private void Initialize()
        {
            this.ServiceProcessInstaller =
                new ServiceProcessInstaller
                {
                    Account = ServiceAccount.LocalService
                };

            var serviceInstaller =
                new ServiceInstaller
                {
                    ServiceName = Constants.ServiceName,
                    Description = Resources.ServiceDescription,
                    DisplayName = Resources.ServiceDisplayName,
                    StartType = ServiceStartMode.Manual
                };

            this.Installers.AddRange(new Installer[] { this.ServiceProcessInstaller, serviceInstaller });
        }

        private void UpdateServiceAccount()
        {
            var accountType = this.Context.Parameters["account"];
            if (accountType != null)
            {
                if (string.Equals(accountType, "user", StringComparison.OrdinalIgnoreCase))
                {
                    this.ServiceProcessInstaller.Account = ServiceAccount.User;
                }
                else if (string.Equals(accountType, "localservice", StringComparison.OrdinalIgnoreCase))
                {
                    this.ServiceProcessInstaller.Account = ServiceAccount.LocalService;
                }
                else if (string.Equals(accountType, "localsystem", StringComparison.OrdinalIgnoreCase))
                {
                    this.ServiceProcessInstaller.Account = ServiceAccount.LocalSystem;
                }
                else
                {
                    throw new InvalidOperationException(
                        string.Format(CultureInfo.CurrentCulture, Resources.ServiceInvalidAccount,
                            this.Context.Parameters["account"]));
                }
            }
        }
    }
}