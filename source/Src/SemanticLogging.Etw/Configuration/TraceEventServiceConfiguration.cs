// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw.Configuration
{
    /// <summary>
    /// Configuration class that provides settings for a <see cref="TraceEventService"/> instance.
    /// </summary>
    public sealed class TraceEventServiceConfiguration : IDisposable
    {
        private readonly TraceEventServiceSettings settings;
        private readonly ObservableCollection<SinkSettings> sinkSettings;
        private FileSystemWatcher watcher;
        private bool watcherDisposed;
        private bool disposed;
        private string monitoredFile;

        /// <summary>
        /// Initializes a new instance of the <see cref="TraceEventServiceConfiguration" /> class.
        /// </summary>
        /// <param name="sinkSettings">The sink settings.</param>
        /// <param name="settings">The settings.</param>
        /// <exception cref="ArgumentNotNull">The EventSources.</exception>
        public TraceEventServiceConfiguration(IEnumerable<SinkSettings> sinkSettings = null, TraceEventServiceSettings settings = null)
        {
            this.sinkSettings = new ObservableCollection<SinkSettings>(sinkSettings ?? Enumerable.Empty<SinkSettings>());
            this.settings = settings ?? new TraceEventServiceSettings();

            // Validate duplicate sinks
            if (this.sinkSettings.GroupBy(i => i.Name).Any(g => g.Count() > 1))
            {
                throw new ConfigurationException(Properties.Resources.DuplicateSinksError);
            }
        }

        /// <summary>
        /// Gets the settings.
        /// </summary>
        /// <value>
        /// The settings.
        /// </value>
        public TraceEventServiceSettings Settings
        {
            get { return this.settings; }
        }

        /// <summary>
        /// Gets the event sources.
        /// </summary>
        /// <value>
        /// The event sources.
        /// </value>
        public ObservableCollection<SinkSettings> SinkSettings
        {
            get { return this.sinkSettings; }
        }

        /// <summary>
        /// Loads the specified file name.
        /// </summary>
        /// <param name="fileName">Name of the file.</param>
        /// <param name="monitorChanges">If set to <c>true</c> monitor file changes.</param>
        /// <param name="createSinks">If set to <c>true</c> [create sinks].</param>
        /// <returns>
        /// The loaded <see cref="TraceEventServiceConfiguration" /> instance.
        /// </returns>
        /// <exception cref="ConfigurationException">All the validation errors detected when opening the file.</exception>
        public static TraceEventServiceConfiguration Load(string fileName, bool monitorChanges = false, bool createSinks = true)
        {
            var configReader = new ConfigurationReader(fileName);
            ConfigurationElement configElement = configReader.Read();

            var serviceSettings = new TraceEventServiceSettings()
            {
                SessionNamePrefix = configElement.TraceEventService.SessionNamePrefix
            };

            var sinkSettings = new List<SinkSettings>();

            foreach (var element in configElement.SinkConfigurationElements)
            {
                var eventSources = element.EventSources.Select(e => new EventSourceSettings(e.Name, e.EventId, e.Level, e.MatchAnyKeyword));
                var sink = createSinks ?
                    new SinkSettings(element.Name, element.SinkPromise.Value, eventSources) :
                    new SinkSettings(element.Name, element.SinkPromise, eventSources);
                sink.SinkConfiguration = element.SinkConfiguration;
                sinkSettings.Add(sink);
            }

            var configuration = new TraceEventServiceConfiguration(sinkSettings, serviceSettings);

            if (monitorChanges)
            {
                configuration.StartConfigurationWatcher(configReader.File);
            }

            return configuration;
        }

        /// <summary>
        /// Dispose listener instances.
        /// </summary>
        public void Dispose()
        {
            if (!this.disposed)
            {
                this.CloseAllListeners();
                if (this.watcher != null)
                {
                    this.watcher.Dispose();
                    this.watcher = null;
                }

                this.disposed = true;
            }
        }

        internal void StartConfigurationWatcher(string file = null)
        {
            this.monitoredFile = file ?? this.monitoredFile;

            this.watcher = new FileSystemWatcher()
            {
                Path = Path.GetDirectoryName(this.monitoredFile),
                Filter = Path.GetFileName(this.monitoredFile),
                NotifyFilter = NotifyFilters.LastWrite
            };

            this.watcher.Changed += this.OnFileChanged;
            this.watcher.Error += this.OnFileError;
            this.watcher.Disposed += (s, e) => this.watcherDisposed = true;
            this.watcherDisposed = false;
            this.watcher.EnableRaisingEvents = true;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Exceptions are stored and logged")]
        private void CloseAllListeners()
        {
            var exceptions = new ConcurrentQueue<Exception>();

            Parallel.ForEach(this.sinkSettings, s =>
            {
                try
                {
                    s.Sink.OnCompleted();
                }
                catch (Exception e)
                {
                    exceptions.Enqueue(e);
                }
            });

            if (exceptions.Count > 0)
            {
                SemanticLoggingEventSource.Log.TraceEventServiceConfigurationShutdownFault(new ConfigurationException(exceptions).ToString());
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Exception is logged")]
        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            try
            {
                //// turn off events to avoid duplicates
                this.watcher.EnableRaisingEvents = false;

                //// Wait to reduce the chance for any transient IO error condition (file locks, etc) 
                Thread.Sleep(500);

                ////Reload updated file and compare configurations
                var updatedConfig = TraceEventServiceConfiguration.Load(e.FullPath, monitorChanges: false, createSinks: false);

                if (!updatedConfig.Settings.Equals(this.Settings))
                {
                    this.Settings.SessionNamePrefix = updatedConfig.Settings.SessionNamePrefix;
                    //// Shortcut for recycling service
                    return;
                }

                var sinkComparer = new SinkSettingsEqualityComparer();
                var eventSourceComparer = new EventSourceSettingsEqualityComparer();

                // removed sinks
                foreach (var removedSink in this.sinkSettings.Except(updatedConfig.SinkSettings, sinkComparer).ToArray())
                {
                    this.sinkSettings.Remove(removedSink);
                }

                // updated sinks
                foreach (var currentSink in this.sinkSettings.Intersect(updatedConfig.SinkSettings, sinkComparer).ToArray())
                {
                    var updatedSink = updatedConfig.SinkSettings.Single(s => s.Name == currentSink.Name);
                    if (updatedSink.SinkConfiguration != currentSink.SinkConfiguration)
                    {
                        // the sink definition was updated so remove/add 
                        this.sinkSettings.Remove(currentSink);
                        this.sinkSettings.Add(updatedSink);
                    }
                    else if (!updatedSink.EventSources.SequenceEqual(currentSink.EventSources, eventSourceComparer))
                    {
                        currentSink.EventSources = updatedSink.EventSources;
                        this.sinkSettings[this.sinkSettings.IndexOf(currentSink)] = currentSink;
                    }
                }

                // new sinks
                foreach (var newSink in updatedConfig.sinkSettings.Except(this.SinkSettings, sinkComparer).ToArray())
                {
                    this.sinkSettings.Add(newSink);
                }
            }
            catch (Exception exception)
            {
                this.OnFileError(sender, new ErrorEventArgs(exception));
            }
            finally
            {
                if (!this.disposed)
                {
                    if (this.watcherDisposed)
                    {
                        //// Regenerate watcher instance because it was disposed and we are still running.
                        this.StartConfigurationWatcher();
                    }
                    else
                    {
                        this.watcher.EnableRaisingEvents = true;
                    }
                }
            }
        }

        private void OnFileError(object sender, ErrorEventArgs e)
        {
            // log error to slab source
            SemanticLoggingEventSource.Log.TraceEventServiceConfigurationFileLoadFault(e.GetException().ToString());
        }
    }
}
