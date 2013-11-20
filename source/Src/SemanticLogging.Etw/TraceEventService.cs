// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Security.Principal;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw.Configuration;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Utility;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw
{
    /// <summary>
    /// Class for listening ETW trace events sent by an implementation of <see cref="System.Diagnostics.Tracing.EventSource"/> typed event.
    /// </summary>
    public sealed class TraceEventService : IDisposable
    {
        private readonly TraceEventServiceConfiguration configuration;
        private readonly SemanticLoggingEventSource logger = SemanticLoggingEventSource.Log;
        private ServiceStatus status;
        private Dictionary<string, TraceEventServiceWorker> workers;

        /// <summary>
        /// Initializes a new instance of the <see cref="TraceEventService" /> class.
        /// Note that the instance of <see cref="TraceEventServiceConfiguration"/> passed will not be disposed by this class.
        /// </summary>
        /// <param name="configuration">The <see cref="TraceEventServiceConfiguration"/> configuration instance.</param>
        /// <exception cref="ArgumentNullException">Configuration.EventSources; Configuration.Settings.</exception>
        /// <exception cref="ArgumentException">No Event Sources specified; Duplicate provider ID.</exception>
        /// <exception cref="UnauthorizedAccessException">Insufficient privileges.</exception>
        /// <exception cref="ConfigurationException">Configuration validation errors.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">MaxDegreeOfParallelism, BoundedCapacity, EventListenerMaxExecutionTimeInMilliseconds.</exception>
        public TraceEventService(TraceEventServiceConfiguration configuration)
        {
            Guard.ArgumentNotNull(configuration, "configuration");
            ThrowOnUnAuthorizedAccess();

            this.configuration = configuration;
            this.Status = ServiceStatus.NotStarted;
            this.configuration.SinkSettings.CollectionChanged += this.OnSinkSettingsChanged;
        }

        /// <summary>Event raised when the <see cref="Status"/> changed.</summary>
        public event EventHandler<StatusChangedEventArgs> StatusChanged;

        /// <summary>
        /// Gets the status of this instance.
        /// </summary>
        /// <value>
        /// The service status.
        /// </value>
        public ServiceStatus Status
        {
            get
            {
                return this.status;
            }

            private set
            {
                this.status = value;
                this.OnStatusChanged();
            }
        }

        /// <summary>
        /// Creates an event trace session and start listening ETW events.
        /// </summary>
        /// <exception cref="System.ObjectDisposedException">The instance was disposed.</exception>
        public void Start()
        {
            if (this.Status == ServiceStatus.Started)
            {
                return;
            }

            if (this.Status == ServiceStatus.Disposed)
            {
                throw new ObjectDisposedException(this.GetType().Name);
            }

            this.workers = new Dictionary<string, TraceEventServiceWorker>();

            // Initialize all workers
            if (!this.AddWorkers(this.configuration.SinkSettings, notifyChanges: false))
            {
                this.logger.TraceEventServiceConfigurationWithFaults();
            }

            this.Status = ServiceStatus.Started;
        }

        /// <summary>
        /// Stops listening ETW events and removes the created event trace session.
        /// Any pending buffered event will be lost and any resource used by the
        /// configured event listeners will be released only after disposing this instance.
        /// </summary>
        /// <exception cref="System.ObjectDisposedException">The service was disposed.</exception>
        public void Stop()
        {
            if (this.Status != ServiceStatus.Started)
            {
                return;
            }

            if (this.Status == ServiceStatus.Disposed)
            {
                throw new ObjectDisposedException(this.GetType().Name);
            }

            this.Status = ServiceStatus.Stopping;

            foreach (var w in this.workers.Values)
            {
                w.Dispose();
            }

            // Dispose all listener instances in configuration
            this.configuration.Dispose();

            this.Status = ServiceStatus.Stopped;
        }

        /// <summary>
        /// Will dispose all resources owned by this class. 
        /// Any external resource like <see cref="TraceEventServiceConfiguration"/> will not be disposed.
        /// </summary>
        public void Dispose()
        {
            if (this.Status != ServiceStatus.Disposed)
            {
                this.Stop();
                this.Status = ServiceStatus.Disposed;
            }
        }

        private static void ThrowOnUnAuthorizedAccess()
        {
            WindowsIdentity indentity = WindowsIdentity.GetCurrent();

            var isAuthorizedUser = indentity.User.IsWellKnown(WellKnownSidType.LocalSystemSid) ||
                                   indentity.User.IsWellKnown(WellKnownSidType.LocalServiceSid) ||
                                   indentity.User.IsWellKnown(WellKnownSidType.NetworkServiceSid);

            if (isAuthorizedUser)
            {
                return;
            }

            var isAuthorizedGroup = indentity.Groups.Cast<SecurityIdentifier>().Any(si =>
                                   si.IsWellKnown(WellKnownSidType.BuiltinAdministratorsSid) ||
                                   si.IsWellKnown(WellKnownSidType.BuiltinPerformanceLoggingUsersSid));

            if (false == isAuthorizedGroup)
            {
                throw new UnauthorizedAccessException(Properties.Resources.InsufficientPrivileges);
            }
        }

        private void OnStatusChanged()
        {
            if (this.StatusChanged != null)
            {
                this.StatusChanged(this, new StatusChangedEventArgs(this.Status));
            }
        }

        private void OnSinkSettingsChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            bool success = true;

            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    success = this.AddWorkers(e.NewItems);
                    break;
                case NotifyCollectionChangedAction.Remove:
                    success = this.RemoveWorkers(e.OldItems);
                    break;
                case NotifyCollectionChangedAction.Replace:
                    success = this.UpdateWorkers(e.NewItems);
                    break;
            }

            if (!success)
            {
                this.logger.TraceEventServiceConfigurationWithFaults();
            }
        }

        private bool AddWorkers(IList newSinks, bool notifyChanges = true)
        {
            bool success = true;
            foreach (SinkSettings sink in newSinks)
            {
                success &= this.HandleException(sink.Name, () =>
                {
                    this.workers.Add(sink.Name, new TraceEventServiceWorker(sink, this.configuration.Settings));
                    if (notifyChanges)
                    {
                        this.logger.TraceEventServiceConfigurationChanged(sink.Name, Properties.Resources.SinkAddedFromReconfiguration);
                    }
                });
            }

            return success;
        }

        private bool RemoveWorkers(IList oldSinks)
        {
            bool success = true;
            foreach (SinkSettings settings in oldSinks)
            {
                success &= this.HandleException(settings.Name, () =>
                {
                    this.workers[settings.Name].Dispose();
                    this.workers.Remove(settings.Name);
                    settings.Sink.OnCompleted();
                    this.logger.TraceEventServiceConfigurationChanged(settings.Name, Properties.Resources.SinkRemovedFromReconfiguration);
                });
            }

            return success;
        }

        private bool UpdateWorkers(IList updatedSinks)
        {
            bool success = true;

            foreach (SinkSettings settings in updatedSinks)
            {
                success &= this.HandleException(settings.Name, () =>
                {
                    this.workers[settings.Name].UpdateSession(settings.EventSources);
                    this.logger.TraceEventServiceConfigurationChanged(settings.Name, Properties.Resources.SinkUpdatedFromReconfiguration);
                });
            }

            return success;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Exception is logged")]
        private bool HandleException(string callerName, Action body)
        {
            try
            {
                body();
                return true;
            }
            catch (Exception exception)
            {
                this.logger.TraceEventServiceConfigurationFault(callerName, exception.ToString());
                return false;
            }
        }
    }
}
