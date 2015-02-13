// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Configuration.Install;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Security.Principal;
using System.ServiceProcess;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw.Service.Properties;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw.Service
{
    internal class ParameterOptions
    {
        internal const string AccountParameterKey = "account";

        private readonly TimeSpan startServiceTiemout = TimeSpan.FromSeconds(5);
        private readonly TraceSource logSource = new TraceSource("SemanticLogging-svc");

        internal ParameterOptions()
        {
            this.ExitCode = ApplicationExitCode.Success;
        }

        public ApplicationExitCode ExitCode { get; private set; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Exception is logged. Only used when in console model")]
        public void Install(IEnumerable<Tuple<string, string>> arguments)
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
                var installerArgs = new List<string>();

                var accountArgument = arguments.FirstOrDefault(a => a.Item1 == AccountParameterKey);
                if (accountArgument != null)
                {
                    installerArgs.Add("/account=" + accountArgument.Item2);
                }

                installerArgs.Add(Assembly.GetExecutingAssembly().Location);

                ManagedInstallerClass.InstallHelper(installerArgs.ToArray());
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
                ManagedInstallerClass.InstallHelper(new[] { "/u", Assembly.GetExecutingAssembly().Location });
            }
            catch (Exception e)
            {
                this.logSource.TraceEvent(TraceEventType.Error, (int)ApplicationExitCode.RuntimeError, e.Message);
                this.ExitCode = ApplicationExitCode.RuntimeError;
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Exception is logged. Only used when in console model")]
        public void Start(IEnumerable<Tuple<string, string>> arguments)
        {
            if (false == IsServiceInstalled())
            {
                this.Install(arguments);
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
                using (var service = new TraceEventServiceHost())
                {
                    service.Start();
                    Console.WriteLine();
                    Console.WriteLine(Resources.StopServiceMessage);
                    Console.ReadLine();
                }
            }
            catch (ReflectionTypeLoadException rtle)
            {
                var loaderExceptions = rtle.LoaderExceptions;
                foreach (var e in loaderExceptions)
                {
                    Console.WriteLine(e.ToString());
                }
                DisplayExceptionOnConsole(rtle);
            }
            catch (Exception e)
            {
                DisplayExceptionOnConsole(e);
            }
        }

        public void ShowHelp(ParameterSet parameters)
        {
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

        private void DisplayExceptionOnConsole(Exception e)
        {
            Console.WriteLine(e.ToString());
            Console.WriteLine();
            Console.WriteLine(Resources.StopServiceMessage);
            Console.ReadLine();
            this.ExitCode = ApplicationExitCode.RuntimeError;
        }

        private bool IsAuthorized()
        {
            var user = WindowsIdentity.GetCurrent();
            if (user == null)
            {
                return false;
            }

            var principal = new WindowsPrincipal(user);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
    }
}
