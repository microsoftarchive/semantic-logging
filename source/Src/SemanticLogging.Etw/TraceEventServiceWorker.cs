// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Session;
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
            var updatedSources = updatedEventSources as EventSourceSettings[] ?? updatedEventSources.ToArray();

            var eventSourceNameComparer = new EventSourceSettingsEqualityComparer(nameOnly: true);
            var eventSourceFullComparer = new EventSourceSettingsEqualityComparer(nameOnly: false);

            // updated sources
            foreach (var currentSource in this.eventSources.Intersect(updatedSources, eventSourceNameComparer).ToArray())
            {
                var updatedSource = updatedSources.Single(s => s.Name == currentSource.Name);
                if (!eventSourceFullComparer.Equals(currentSource, updatedSource))
                {
                    currentSource.CopyValuesFrom(updatedSource);
                    TraceEventUtil.EnableProvider(
                        this.session,
                        currentSource.EventSourceId,
                        currentSource.Level,
                        currentSource.MatchAnyKeyword,
                        currentSource.Arguments,
                        currentSource.ProcessNamesToFilter,
                        sendManifest: false);
                }
            }

            // new sources
            foreach (var newSource in updatedSources.Except(this.eventSources, eventSourceNameComparer).ToArray())
            {
                TraceEventUtil.EnableProvider(
                    this.session,
                    newSource.EventSourceId,
                    newSource.Level,
                    newSource.MatchAnyKeyword,
                    newSource.Arguments,
                    newSource.ProcessNamesToFilter,
                    sendManifest: true);
                this.eventSources.Add(newSource);
            }

            // removed sources
            foreach (var removedSource in this.eventSources.Except(updatedSources, eventSourceNameComparer).ToArray())
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

            // get any previously cached manifest and update the schema cache
            this.UpdateCaches();

            // hook up to all incoming events and filter out manifests
            this.source.Dynamic.All += e => this.ProcessEvent(e);

            // listen to new manifests
            this.source.Dynamic.DynamicProviderAdded += m => this.OnManifestReceived(m);

            // We collect all the manifests and save/terminate process when done
            this.source.UnhandledEvents += e => this.ProcessUnhandledEvent(e);

            foreach (var eventSource in this.eventSources)
            {
                // Bind the provider (EventSource/EventListener) with the session
                TraceEventUtil.EnableProvider(
                    this.session,
                    eventSource.EventSourceId,
                    eventSource.Level,
                    eventSource.MatchAnyKeyword,
                    eventSource.Arguments,
                    eventSource.ProcessNamesToFilter);
            }

            // source.Process() is blocking so we need to launch it on a separate thread.
            this.workerTask = Task.Factory.StartNew(() => this.source.Process(), TaskCreationOptions.LongRunning).
                                           ContinueWith(t => this.HandleProcessTaskFault(t));
        }

        private void UpdateCaches()
        {
            this.manifestCache.Read();
            foreach (var provider in this.source.Dynamic.DynamicProviders)
            {
                this.schemaCache.UpdateSchemaFromManifest(provider.Guid, provider.Manifest);
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Exception is logged")]
        private void ProcessEvent(TraceEvent evt)
        {
            if (evt.ID != (TraceEventID)0xFFFE)
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
        }

        private void OnManifestReceived(ProviderManifest providerManifest)
        {
            // Update schemas for this provider 
            this.schemaCache.UpdateSchemaFromManifest(providerManifest.Guid, providerManifest.Manifest);

            // Update manifest cache so any new worker instance may pick up the updates version on start up by calling "this.manifestCache.Read()".                
            // Notice that we are refreshing the manifest with the latest version received so any older version will be overwritten.
            this.manifestCache.Write();
        }

        private void ProcessUnhandledEvent(TraceEvent evt)
        {
            if (evt.ID == TraceEventUtil.ManifestEventID)
            {
                // manifest event - ignore
                return;
            }

            if (evt.ID == 0)
            {
                // back channel event - log
                this.logger.TraceEventServiceOutOfBandEvent(this.sessionName, evt.ProviderGuid, (EventLevel)evt.Level, (EventKeywords)evt.Keywords, evt.FormattedMessage);
                return;
            }

            // Simply notify and count lost events since we don't have a manifest yet to parse this event.
            this.NotifyEventLost();
        }

        private EventEntry CreateEventEntry(TraceEvent traceEvent)
        {
            return new EventEntry(
                traceEvent.ProviderGuid,
                (int)traceEvent.ID,
                traceEvent.FormattedMessage,
                this.CreatePayload(traceEvent),
                new DateTimeOffset(traceEvent.TimeStamp),
                traceEvent.ProcessID,
                traceEvent.ThreadID,
                traceEvent.ActivityID,
                traceEvent.RelatedActivityID,
                this.schemaCache.GetSchema(traceEvent));
        }

        private ReadOnlyCollection<object> CreatePayload(TraceEvent traceEvent)
        {
            var payloadValues = new List<object>();

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
                    // ReSharper disable once PossibleNullReferenceException - documented not to be null if task.IsFaulted is true
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
