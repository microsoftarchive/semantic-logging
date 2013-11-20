// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Configuration.Install;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Security.Principal;
using System.ServiceProcess;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw.Service.Properties;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw.Utility;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw.Service
{
    internal class ParameterOptions
    {
        private readonly TimeSpan startServiceTiemout = TimeSpan.FromSeconds(5);
        private readonly TraceSource logSource = new TraceSource("SemanticLogging-svc");

        internal ParameterOptions()
        {
            this.ExitCode = ApplicationExitCode.Success;
        }

        public ApplicationExitCode ExitCode { get; private set; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Exception is logged. Only used when in console model")]
        public void Install()
        {
            // Check for admin rights
            if (false == this.IsAuthorized())
            {
                this.logSource.TraceEvent(TraceEventType.Error, (int)ApplicationExitCode.SecurityError, Resources.InsufficientAccessPermission);
                this.ExitCode = ApplicationExitCode.SecurityError;
                return;
            }

            if (IsServiceInstalled())
            {
                this.logSource.TraceInformation(Resources.AlreadyInstalledService);
                return;
            }

            try
            {
                ManagedInstallerClass.InstallHelper(new string[] { Assembly.GetExecutingAssembly().Location });
            }
            catch (Exception e)
            {
                this.logSource.TraceEvent(TraceEventType.Error, (int)ApplicationExitCode.RuntimeError, e.Message);
                this.ExitCode = ApplicationExitCode.RuntimeError;
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Exception is logged. Only used when in console model")]
        public void Uninstall()
        {
            // Check for admin rights
            if (false == this.IsAuthorized())
            {
                this.logSource.TraceEvent(TraceEventType.Error, (int)ApplicationExitCode.SecurityError, Resources.InsufficientAccessPermission);
                this.ExitCode = ApplicationExitCode.SecurityError;
                return;
            }

            if (false == IsServiceInstalled())
            {
                this.logSource.TraceEvent(TraceEventType.Error, (int)ApplicationExitCode.InputError, Resources.NotInstalledService);
                this.ExitCode = ApplicationExitCode.InputError;
                return;
            }

            try
            {
                ManagedInstallerClass.InstallHelper(new string[] { "/u", Assembly.GetExecutingAssembly().Location });
            }
            catch (Exception e)
            {
                this.logSource.TraceEvent(TraceEventType.Error, (int)ApplicationExitCode.RuntimeError, e.Message);
                this.ExitCode = ApplicationExitCode.RuntimeError;
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Exception is logged. Only used when in console model")]
        public void Start()
        {
            if (false == IsServiceInstalled())
            {
                this.Install();
                if (this.ExitCode != ApplicationExitCode.Success)
                {
                    return;
                }
            }

            ServiceController controller = GetController();

            switch (controller.Status)
            {
                case ServiceControllerStatus.Running:
                    this.logSource.TraceInformation(Resources.ServiceAlreadyStarted);
                    return;
                case ServiceControllerStatus.Stopped:
                    try
                    {
                        controller.Start();
                        controller.WaitForStatus(ServiceControllerStatus.Running, this.startServiceTiemout);
                        this.logSource.TraceInformation(Resources.ServiceStarted);
                        return;
                    }
                    catch (System.ServiceProcess.TimeoutException)
                    {
                        logSource.TraceEvent(TraceEventType.Warning, (int)ApplicationExitCode.RuntimeError, Resources.ServiceNotStartedTimeout);
                        break;
                    }
                    catch (Exception e)
                    {
                        this.logSource.TraceEvent(TraceEventType.Error, (int)ApplicationExitCode.RuntimeError, e.Message);
                        break;
                    }

                default:
                    this.logSource.TraceEvent(TraceEventType.Error, 0, Resources.ServiceNotStarted, controller.Status);
                    break;
            }

            this.ExitCode = ApplicationExitCode.RuntimeError;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Exception is logged. Only used when in console model")]
        public void ConsoleMode()
        {
            try
            {
                using (TraceEventServiceHost service = new TraceEventServiceHost())
                {
                    service.Start();
                    Console.WriteLine();
                    Console.WriteLine(Resources.StopServiceMessage);
                    Console.ReadLine();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                Console.WriteLine();
                Console.WriteLine(Resources.StopServiceMessage);
                Console.ReadLine();
                this.ExitCode = ApplicationExitCode.RuntimeError;
            }
        }

        public void ShowHelp(ParameterSet parameters)
        {
            this.ShowHeader();

            foreach (var parameter in parameters)
            {
                Console.WriteLine(parameter.Description);
            }
        }

        public void ShowHeader()
        {
            Console.WriteLine(Assembly.GetExecutingAssembly().ToProductString());
        }

        private static bool IsServiceInstalled()
        {
            return GetController() != null;
        }

        private static ServiceController GetController()
        {
            return ServiceController.GetServices().FirstOrDefault(s => s.ServiceName.Equals(Constants.ServiceName, StringComparison.OrdinalIgnoreCase));
        }

        private bool IsAuthorized()
        {
            var user = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(user);

            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
    }
}
