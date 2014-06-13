// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.ServiceProcess;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw.Service.Properties;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw.Service
{
    internal static class Application
    {
        internal static int Main(string[] args)
        {
            if (args.Length == 0 && false == Environment.UserInteractive)
            {
                // Called by SCM
                ServiceBase.Run(new TraceEventServiceHost());
                return (int)ApplicationExitCode.Success;
            }

            var options = new ParameterOptions();
            var parameters = new ParameterSet
            {
                { "i|install", Resources.InstallArgDescription, (p, a) => options.Install(a)  },
                { "u|uninstall", Resources.UninstallArgDescription, (p, a) => options.Uninstall() },
                { "s|start", Resources.StartArgDescription, (p, a) => options.Start(a) },
                { "c|console", Resources.ConsoleArgDescription, (p, a) => options.ConsoleMode() },
                { "h|help|?", Resources.HelpArgDescription, (p, a) => options.ShowHelp(p) },
                { "a|account", Resources.AccountArgDescription, ParameterOptions.AccountParameterKey }
            };

            options.ShowHeader();

            if (!parameters.Parse(args))
            {
                options.ShowHelp(parameters);
                return (int)ApplicationExitCode.InputError;
            }

            return (int)options.ExitCode;
        }
    }
}
