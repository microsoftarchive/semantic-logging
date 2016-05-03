// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw.Configuration;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw.Service.Properties;
using System.Reflection;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw.Service
{
    /// <summary>
    /// The windows service host class for <see cref="TraceEventService"/> class.
    /// </summary>
    public partial class TraceEventServiceHost : ServiceBase
    {
        private const string EtwConfigurationFileNameKey = "EtwConfigurationFileName";
        private const string NonTransientErrorsEventLevelKey = "NonTransientErrorsEventLevel";

        // Capture non-transient errors from internal SLAB EventSource
        private EventListener slabNonTransientErrors;

        private TraceEventService service;
        private bool consoleMode;

        /// <summary>
        /// Initializes a new instance of the <see cref="TraceEventServiceHost" /> class.
        /// </summary>
        public TraceEventServiceHost()
        {
            this.Initialize();
        }

        // For running in Console mode
        internal void Start()
        {
            this.consoleMode = true;
            this.OnStart(null);
        }

        /// <summary>
        /// When implemented in a derived class, executes when a Start command is sent to the service by the Service Control Manager (SCM) or when the operating system starts (for a service that starts automatically). Specifies actions to take when the service starts.
        /// </summary>
        /// <param name="args">Data passed by the start command.</param>
        protected override void OnStart(string[] args)
        {
            try
            {
                string configFile = GetConfigFileFullPath();
                var configuration = TraceEventServiceConfiguration.Load(configFile, monitorChanges: true);
                configuration.Settings.PropertyChanged += this.OnTraceEventServiceSettingsChanged;
                this.service = new TraceEventService(configuration);

                if (this.consoleMode)
                {
                    this.service.StatusChanged += this.OnServiceStatusChanged;
                }

                this.ShowConfiguration(configuration, configFile);
                this.EnableNonTransientErrorsHandling();

                this.service.Start();
            }
            catch (Exception e)
            {
                // log and rethrow to notify SCM
                if (!this.consoleMode)
                {
                    LogException(e);
                }

                throw;
            }
        }

        /// <summary>
        /// When implemented in a derived class, executes when a Stop command is sent to the service by the Service Control Manager (SCM). Specifies actions to take when a service stops running.
        /// </summary>
        protected override void OnStop()
        {
            // Short-cut if TraceEventService was not started or is already stopped.
            if (this.service == null)
            {
                return;
            }

            try
            {
                this.service.Dispose();
                this.service = null;
                this.DisableNonTransientErrorsHandling();
                if (!this.consoleMode)
                {
                    this.EventLog.WriteEntry(Resources.ServiceStoppedMessage);
                }
            }
            catch (Exception e)
            {
                // Notify in console error handling
                if (this.consoleMode)
                {
                    throw;
                }

                // Do not rethrow in Service mode so SCM can mark this service as stopped (this will allow to uninstall the service properly) 
                this.EventLog.WriteEntry(e.ToString(), EventLogEntryType.Error);
            }
        }

        /// <summary>
        /// Disposes of the resources (other than memory) used by the <see cref="T:System.ServiceProcess.ServiceBase" />.
        /// </summary>
        /// <param name="disposing">True to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
        [SuppressMessage("Microsoft.Usage", "CA2213:DisposableFieldsShouldBeDisposed", MessageId = "configWatcher", Justification = "Disposed in OnStop()"),
         SuppressMessage("Microsoft.Usage", "CA2213:DisposableFieldsShouldBeDisposed", MessageId = "service", Justification = "Disposed in OnStop()"),
         SuppressMessage("Microsoft.Usage", "CA2213:DisposableFieldsShouldBeDisposed", MessageId = "slabNonTransientErrors", Justification = "Disposed in OnStop()")]
        protected override void Dispose(bool disposing)
        {
            this.OnStop();
            base.Dispose(disposing);
        }

        private static string GetConfigFileFullPath()
        {
            string configFile = ConfigurationManager.AppSettings[EtwConfigurationFileNameKey];

            if (string.IsNullOrWhiteSpace(configFile))
            {
                throw new ArgumentException(Resources.ConfigFileNameNotFoundError);
            }

            if (Path.IsPathRooted(configFile))
            {
                return configFile;
            }

            return Path.GetFullPath(configFile);
        }

        private void Initialize()
        {
            this.AutoLog = false;
            this.ServiceName = Constants.ServiceName;

            // Set current folder to this instance location so any relative file path
            // referenced in any EventListener will point to this folder.
            Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);
        }

        private void ShowConfiguration(TraceEventServiceConfiguration configuration, string file)
        {
            var sb = new StringBuilder();

            sb.AppendFormat(Resources.LoadedConfigurationMessage, file);
            sb.AppendLine();
            sb.AppendLine();

            if (configuration.SinkSettings.Count > 0)
            {
                sb.AppendFormat(Resources.EventSessionMessage, configuration.Settings.SessionNamePrefix);
                sb.AppendLine();

                foreach (var sink in configuration.SinkSettings)
                {
                    sb.AppendLine();
                    sb.AppendFormat(Resources.SinkNameMessage, sink.Name);
                    sb.AppendLine();
                    sb.AppendLine(Resources.EventSourceListMessage);

                    foreach (var eventSource in sink.EventSources)
                    {
                        sb.AppendFormat(Resources.EventSourceNameMessage, eventSource.EventSourceId);
                        sb.AppendLine();
                        sb.AppendFormat(Resources.EventSourceDescriptionMessage, eventSource.Name, eventSource.Level, eventSource.MatchAnyKeyword);
                        sb.AppendLine();

                        if (eventSource.Arguments.Any())
                        {
                            sb.AppendFormat(
                                Resources.EventSourceArgumentsMessage,
                                string.Join(
                                    ", ",
                                    eventSource.Arguments.Select(kvp => string.Format("\"{0}\" = \"{1}\"", kvp.Key, kvp.Value))));
                            sb.AppendLine();
                        }

                        if (eventSource.ProcessNamesToFilter.Any())
                        {
                            sb.AppendFormat(Resources.EventSourceProcessNamesMessage, string.Join(", ", eventSource.ProcessNamesToFilter));
                            sb.AppendLine();
                        }
                    }
                }
            }
            else
            {
                sb.AppendLine(Resources.UpdateConfigFileMessage);
            }

            if (this.consoleMode)
            {
                Console.WriteLine(sb.ToString());
            }
            else
            {
                this.EventLog.WriteEntry(sb.ToString(), EventLogEntryType.Information);
            }
        }

        private void OnServiceStatusChanged(object sender, StatusChangedEventArgs e)
        {
            switch (e.Status)
            {
                case ServiceStatus.Started:
                    Console.WriteLine(Resources.StartedServiceMessage);
                    break;
                case ServiceStatus.Stopping:
                    Console.WriteLine(Resources.ShutdownServiceMessage);
                    break;
                default:
                    Console.WriteLine(e.Status);
                    break;
            }
        }

        private void OnTraceEventServiceSettingsChanged(object sender, PropertyChangedEventArgs e)
        {
            // We should recycle on any settings changes
            if (this.consoleMode)
            {
                Console.WriteLine(Resources.RecyclingServiceOnConfigChanged);
            }
            else
            {
                this.EventLog.WriteEntry(Resources.RecyclingServiceOnConfigChanged, EventLogEntryType.Information);
            }

            // schedule recycle to decouple from current context
            Task.Run(() => this.RecycleService());
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Exception is logged to event log and service is shut down.")]
        private void RecycleService()
        {
            try
            {
                this.OnStop();
                this.OnStart(null);
            }
            catch (Exception e)
            {
                if (this.consoleMode)
                {
                    Console.WriteLine(e.ToString());
                }
                else
                {
                    this.EventLog.WriteEntry(e.ToString(), EventLogEntryType.Error);
                }

                Environment.Exit((int)ApplicationExitCode.InputError);
            }
        }

        private void EnableNonTransientErrorsHandling()
        {
            var observable = new ObservableEventListener();
            this.slabNonTransientErrors = observable;

            if (this.consoleMode)
            {
                observable.LogToConsole();
            }
            else
            {
                observable.Subscribe(new ServiceEventLogSink(this.EventLog));
            }

            EventLevel level;
            if (!Enum.TryParse(ConfigurationManager.AppSettings[NonTransientErrorsEventLevelKey], out level))
            {
                level = EventLevel.LogAlways;
            }

            this.slabNonTransientErrors.EnableEvents(SemanticLoggingEventSource.Log, level, Keywords.All);

            // Capture any unhandled error in worker threads
            AppDomain.CurrentDomain.UnhandledException += this.OnAppDomainUnhandledException;

            // Handle unobserved task exceptions
            TaskScheduler.UnobservedTaskException += this.OnUnobservedTaskException;
        }

        private void DisableNonTransientErrorsHandling()
        {
            if (this.slabNonTransientErrors != null)
            {
                this.slabNonTransientErrors.DisableEvents(SemanticLoggingEventSource.Log);
                this.slabNonTransientErrors.Dispose();
                this.slabNonTransientErrors = null;
            }

            AppDomain.CurrentDomain.UnhandledException -= this.OnAppDomainUnhandledException;
            TaskScheduler.UnobservedTaskException -= this.OnUnobservedTaskException;
        }

        private void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            this.NotifyError((Exception)e.ExceptionObject);
        }

        private void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            e.SetObserved();
            this.NotifyError(e.Exception);
        }

        private void NotifyError(Exception error)
        {
            if (this.consoleMode)
            {
                Console.WriteLine(error.ToString());
            }
            else
            {
                this.EventLog.WriteEntry(error.ToString(), EventLogEntryType.Error);
            }
        }

        private void LogException(Exception e)
        {
            var rtle = e as ReflectionTypeLoadException;
            if (rtle != null)
            {
                LogLoaderExceptions(rtle);
            }

            this.EventLog.WriteEntry(e.ToString(), EventLogEntryType.Error);
        }

        private void LogLoaderExceptions(ReflectionTypeLoadException e)
        {
            foreach (var loaderException in e.LoaderExceptions)
            {
                this.EventLog.WriteEntry(loaderException.ToString(), EventLogEntryType.Error);
            }
        }
    }
}
