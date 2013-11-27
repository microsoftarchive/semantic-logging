// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Diagnostics.Tracing;
using Diagnostics.Tracing.Parsers;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw.Configuration;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw.Utility;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Utility;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw
{
    internal sealed class TraceEventServiceWorker : IDisposable
    {
        private readonly SemanticLoggingEventSource logger = SemanticLoggingEventSource.Log;
        private readonly TraceEventSchemaCache schemaCache = new TraceEventSchemaCache();
        private readonly IObserver<EventEntry> sink;
        private readonly List<EventSourceSettings> eventSources;
        private readonly string sessionName;
        private TraceEventManifestsCache manifestCache;
        private ETWTraceEventSource source;
        private TraceEventSession session;
        private Task workerTask;
        private volatile bool disposing;
        private bool disposed;
        private int eventsLost;

        public TraceEventServiceWorker(SinkSettings sinkSettings, TraceEventServiceSettings serviceSettings)
        {
            Guard.ArgumentNotNull(sinkSettings, "sinkSettings");
            Guard.ArgumentNotNull(serviceSettings, "serviceSettings");

            this.sink = sinkSettings.Sink;
            this.eventSources = new List<EventSourceSettings>(sinkSettings.EventSources);
            this.sessionName = serviceSettings.SessionNamePrefix + "-" + sinkSettings.Name;
            this.Initialize();
        }

        public void UpdateSession(IEnumerable<EventSourceSettings> updatedEventSources)
        {
            Guard.ArgumentNotNull(updatedEventSources, "updatedEventSources");

            var eventSourceComparer = new EventSourceSettingsEqualityComparer(nameOnly: true);

            // updated sources
            foreach (var currentSource in this.eventSources.Intersect(updatedEventSources, eventSourceComparer).ToArray())
            {
                var updatedSource = updatedEventSources.Single(s => s.Name == currentSource.Name);
                if (updatedSource.Level != currentSource.Level ||
                    updatedSource.MatchAnyKeyword != currentSource.MatchAnyKeyword)
                {
                    TraceEventUtil.EnableProvider(this.session, updatedSource.EventSourceId, updatedSource.Level, updatedSource.MatchAnyKeyword, sendManifest: false);
                    currentSource.Level = updatedSource.Level;
                    currentSource.MatchAnyKeyword = updatedSource.MatchAnyKeyword;
                }
            }

            // new sources
            foreach (var newSource in updatedEventSources.Except(this.eventSources, eventSourceComparer).ToArray())
            {
                TraceEventUtil.EnableProvider(this.session, newSource.EventSourceId, newSource.Level, newSource.MatchAnyKeyword, sendManifest: true);
                this.eventSources.Add(newSource);
            }

            // removed sources
            foreach (var removedSource in this.eventSources.Except(updatedEventSources, eventSourceComparer).ToArray())
            {
                this.session.DisableProvider(removedSource.EventSourceId);
                this.eventSources.Remove(removedSource);
            }
        }

        public void Dispose()
        {
            if (!this.disposed)
            {
                this.disposing = true;

                if (this.eventsLost > 0)
                {
                    this.logger.TraceEventServiceProcessEventsLost(this.sessionName, this.eventsLost);
                }

                // By disposing source we force this.source.Process() to exit and end workerTask
                // Note that source reference is not released rigth after Dispose() to avoid 'CallbackOnCollectedDelegate'exception
                // that might be thrown before Process() ends.
                this.source.Dispose();
                this.workerTask.Wait();
                this.session.Dispose();
                this.session = null;
                this.source = null;              
                
                this.disposed = true;
            }
        }

        private void Initialize()
        {
            this.session = TraceEventUtil.CreateSession(this.sessionName);

            // Hook up the ETWTraceEventSource to the specified session
            this.source = new ETWTraceEventSource(this.sessionName, TraceEventSourceType.Session);

            this.manifestCache = new TraceEventManifestsCache(this.source.Dynamic);

            // get any previously cached manifest
            this.manifestCache.Read();

            // hook up to all incoming events and filter out manifests
            this.source.Dynamic.All += e => this.ProcessEvent(e);

            // listen to new manifests
            this.source.Dynamic.ManifestReceived += m => this.OnManifestReceived(m);

            // We collect all the manifests and save/terminate process when done
            this.source.UnhandledEvent += e => this.ProcessUnhandledEvent(e);

            foreach (var eventSource in this.eventSources)
            {
                // Bind the provider (EventSource/EventListener) with the session
                TraceEventUtil.EnableProvider(this.session, eventSource.EventSourceId, eventSource.Level, eventSource.MatchAnyKeyword);
            }

            // source.Process() is blocking so we need to launch it on a separate thread.
            this.workerTask = Task.Factory.StartNew(() => this.source.Process(), TaskCreationOptions.LongRunning).
                                           ContinueWith(t => this.HandleProcessTaskFault(t));
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Exception is logged")]
        private void ProcessEvent(TraceEvent evt)
        {
            try
            {
                this.sink.OnNext(this.CreateEventEntry(evt));
            }
            catch (Exception exception)
            {
                this.logger.TraceEventServiceSinkUnhandledFault(this.sessionName, exception.ToString());
            }
        }

        private void OnManifestReceived(ProviderManifest providerManifest)
        {
            if (providerManifest.Error != null)
            {
                this.logger.TraceEventServiceManifestGenerationFault(providerManifest.Guid, providerManifest.Error.ToString());
                return;
            }

            // Update schemas for this provider 
            this.schemaCache.UpdateSchemaFromManifest(providerManifest.Guid, providerManifest.Manifest);

            // Update manifest cache so any new worker instance may pick up the updates version on start up by calling "this.manifestCache.Read()".                
            // Notice that we are refreshing the manifest with the latest version received so any older version will be overwritten.
            this.manifestCache.Write();
        }

        private void ProcessUnhandledEvent(TraceEvent evt)
        {
            if (evt.ID != TraceEventUtil.ManifestEventID)
            {
                // Simply notify and count lost events since we don't have a manifest yet to parse this event.
                this.NotifyEventLost();
            }
        }

        private EventEntry CreateEventEntry(TraceEvent traceEvent)
        {
            return new EventEntry(traceEvent.ProviderGuid,
                (int)traceEvent.ID,
                traceEvent.FormattedMessage,
                this.CreatePayload(traceEvent),
                DateTimeOffset.FromFileTime(traceEvent.TimeStamp100ns),
                traceEvent.ActivityID,
                traceEvent.RelatedActivityID,
                this.schemaCache.GetSchema(traceEvent));
        }

        private ReadOnlyCollection<object> CreatePayload(TraceEvent traceEvent)
        {
            List<object> payloadValues = new List<object>();

            for (int i = 0; i < traceEvent.PayloadNames.Length; i++)
            {
                payloadValues.Add(traceEvent.PayloadValue(i));
            }

            return new ReadOnlyCollection<object>(payloadValues);
        }

        private void HandleProcessTaskFault(Task task)
        {
            if (task.IsFaulted)
            {
                // set as observed exception
                var exception = task.Exception;

                if (!this.disposing)
                {
                    // The process stopped because of a non-transient exception so log it. 
                    this.logger.TraceEventServiceProcessTaskFault(this.sessionName, exception.Flatten().ToString());

                    // The worker will be left in a stopped state and resources will be released on Dispose().
                    // Stop session since we are not listening any new incoming event.
                    this.session.Stop(noThrow: true);
                }
            }
        }

        private void NotifyEventLost()
        {
            // The event could not be parsed because there's no manifest cached yet.
            if (this.eventsLost == 0)
            {
                this.logger.TraceEventServiceEventsWillBeLost(this.sessionName);
            }
            else if (this.eventsLost == int.MaxValue)
            {
                this.logger.TraceEventServiceProcessEventsLost(this.sessionName, this.eventsLost);
                this.eventsLost = 0;
            }

            this.eventsLost++;
        }
    }
}
