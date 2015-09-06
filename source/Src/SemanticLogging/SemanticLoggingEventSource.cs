// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Diagnostics.Tracing;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging
{
    /// <summary>
    /// An <see cref="EventSource"/> class to notify non-transient faults and internal trace information.
    /// </summary>
    [EventSource(Name = "Microsoft-SemanticLogging", LocalizationResources = "Microsoft.Practices.EnterpriseLibrary.SemanticLogging.SemanticLoggingEventSourceResources")]
    public sealed class SemanticLoggingEventSource : EventSource
    {
        private static readonly Lazy<SemanticLoggingEventSource> Instance = new Lazy<SemanticLoggingEventSource>(() => new SemanticLoggingEventSource());

        private SemanticLoggingEventSource()
        {
        }

        /// <summary>
        /// Gets the singleton instance of <see cref="SemanticLoggingEventSource"/>.
        /// </summary>
        /// <value>The singleton instance.</value>
        public static SemanticLoggingEventSource Log
        {
            get { return Instance.Value; }
        }

        /// <summary>
        /// Trace event that may be used for logging any unhandled exception that occurs in a custom sink.
        /// </summary>
        /// <param name="message">The exception message.</param>
        [Event(1, Level = EventLevel.Error, Keywords = Keywords.Sink, Message = "Unhandled fault in a custom sink. Message: {0}")]
        public void CustomSinkUnhandledFault(string message)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(1, message);
            }
        }

        /// <summary>
        /// Trace event that may be used for logging any unhandled exception that occurs in a custom formatter.
        /// </summary>
        /// <param name="message">The exception message.</param>
        [Event(2, Level = EventLevel.Error, Keywords = Keywords.Formatting, Message = "Unhandled fault in a custom formatter. Message: {0}")]
        public void CustomFormatterUnhandledFault(string message)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(2, message);
            }
        }

        [Event(100, Level = EventLevel.Warning, Keywords = Keywords.Sink, Message = "A transient fault occurred in a database sink. Message: {0}")]
        internal void DatabaseSinkPublishEventsTransientError(string message)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(100, message);
            }
        }

        [Event(101, Level = EventLevel.Error, Keywords = Keywords.Sink, Message = "A database sink failed to publish events. Message: {0}")]
        internal void DatabaseSinkPublishEventsFailed(string message)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(101, message);
            }
        }

        [Event(102, Level = EventLevel.Warning, Keywords = Keywords.Sink, Message = "A database sink discarded {0} events due to failures while attempting to publish a batch.")]
        internal void DatabaseSinkPublishEventsFailedAndDiscardsEntries(int numberOfEntries)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(102, numberOfEntries);
            }
        }

        [Event(103, Level = EventLevel.Warning, Keywords = Keywords.Sink, Message = "A database sink discarded an event with index {1} due to a failure while attempting to publish a batch. Message: {0}")]
        internal void DatabaseSinkPublishEventsFailedAndDiscardSingleEntry(string message, int entryOrder)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(103, message, entryOrder);
            }
        }

        [Event(200, Level = EventLevel.Critical, Keywords = Keywords.Sink, Message = "The console sink failed to write an event. Message: {0}")]
        internal void ConsoleSinkWriteFailed(string message)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(200, message);
            }
        }

        [Event(300, Level = EventLevel.Critical, Keywords = Keywords.Sink, Message = "A flat file sink failed to write an event. Message: {0}")]
        internal void FlatFileSinkWriteFailed(string message)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(300, message);
            }
        }

        [Event(400, Level = EventLevel.Critical, Keywords = Keywords.Sink, Message = "A rolling flat file sink failed to write an event. Message: {0}")]
        internal void RollingFlatFileSinkWriteFailed(string message)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(400, message);
            }
        }

        [Event(500, Level = EventLevel.Critical, Keywords = Keywords.Sink, Message = "An Azure Table sink failed to write a batch of events. Message: {0}")]
        internal void WindowsAzureTableSinkPublishEventsFailed(string message)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(500, message);
            }
        }

        [Event(501, Level = EventLevel.Critical, Keywords = Keywords.Sink, Message = "An Azure Table sink failed to create a table. Message: {0}")]
        internal void WindowsAzureTableSinkTableCreationFailed(string message)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(501, message);
            }
        }

        [Event(502, Level = EventLevel.Warning, Keywords = Keywords.Sink, Message = "A transient fault occurred in an Azure Table sink. Message: {0}")]
        internal void WindowsAzureTableSinkTransientError(string message)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(502, message);
            }
        }

        [Event(503, Level = EventLevel.Critical, Keywords = Keywords.Sink, Message = "An Azure Table sink failed to create an entity. Message: {0}")]
        internal void WindowsAzureTableSinkEntityCreationFailed(string message)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(503, message);
            }
        }

        [Event(504, Level = EventLevel.Error, Keywords = Keywords.Sink, Message = "An Azure Table sink discarded {0} events due to failures while attempting to publish a batch.")]
        internal void WindowsAzureTableSinkPublishEventsFailedAndDiscardsEntries(int numberOfEntries)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(504, numberOfEntries);
            }
        }

        [Event(600, Level = EventLevel.Critical, Keywords = Keywords.Sink, Message = "An Elasticsearch sink failed to create payload for an entity. Message: {0}")]
        internal void ElasticsearchSinkEntityPayloadCreationFailed(string message)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(600, message);
            }
        }

        [Event(601, Level = EventLevel.Critical, Keywords = Keywords.Sink, Message = "An Elasticsearch sink failed to write a batch of events. Message: {0}")]
        internal void ElasticsearchSinkWriteEventsFailed(string message)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(601, message);
            }
        }

        [Event(602, Level = EventLevel.Error, Keywords = Keywords.Sink, Message = "An Elasticsearch sink discarded {0} events due to failures while attempting to write a batch.")]
        internal void ElasticsearchSinkWriteEventsFailedAndDiscardsEntries(int numberOfEntries, string message)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(602, numberOfEntries, message);
            }
        }

        [Event(700, Level = EventLevel.Warning, Keywords = Keywords.Formatting, Message = "The payload for an event could not be serialized. Message: {0}")]
        internal void EventEntrySerializePayloadFailed(string message)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(700, message);
            }
        }

        [Event(701, Level = EventLevel.Warning, Keywords = Keywords.Formatting, Message = "The JSON writer failed to handle an event. Message: {0}")]
        internal void EventEntryJsonWriterFailed(string message)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(701, message);
            }
        }

        [Event(702, Level = EventLevel.Warning, Keywords = Keywords.Formatting, Message = "The XML formatter failed to format an event. Message: {0}")]
        internal void EventEntryXmlWriterFailed(string message)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(702, message);
            }
        }

        [Event(703, Level = EventLevel.Warning, Keywords = Keywords.Formatting, Message = "The text formatter failed to format an event. Message: {0}")]
        internal void EventEntryTextWriterFailed(string message)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(703, message);
            }
        }

        [Event(800, Level = EventLevel.Error, Keywords = Keywords.TraceEvent, Message = "An unhandled exception occurred for the trace session '{0}'. Message: {1}")]
        internal void TraceEventServiceSinkUnhandledFault(string sessionName, string message)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(800, sessionName, message);
            }
        }

        [Event(801, Level = EventLevel.Critical, Keywords = Keywords.TraceEvent, Message = "An unhandled fault was detected in the processing task for the trace session '{0}'. Message: {1}")]
        internal void TraceEventServiceProcessTaskFault(string sessionName, string message)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(801, sessionName, message);
            }
        }

        [Event(802, Level = EventLevel.Informational, Keywords = Keywords.TraceEvent, Message = "The trace session with the name '{0}' was removed.")]
        internal void TraceEventServiceSessionRemoved(string sessionName)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(802, sessionName);
            }
        }

        [Event(803, Level = EventLevel.Error, Keywords = Keywords.TraceEvent, Message = "A fault was detected while processing the configuration for the element '{0}'. Message: {1}")]
        internal void TraceEventServiceConfigurationFault(string faultedElement, string message)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(803, faultedElement, message);
            }
        }

        [Event(804, Level = EventLevel.Informational, Keywords = Keywords.TraceEvent, Message = "The configuration changed for the element '{0}'. Message: {1}")]
        internal void TraceEventServiceConfigurationChanged(string changedElement, string message)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(804, changedElement, message);
            }
        }

        [Event(805, Level = EventLevel.Warning, Keywords = Keywords.TraceEvent, Message = "The configuration was partially successfully loaded. Check logs for further error details.")]
        internal void TraceEventServiceConfigurationWithFaults()
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(805);
            }
        }

        [Event(806, Level = EventLevel.Warning, Keywords = Keywords.TraceEvent, Message = "Some events will be lost because of buffer overruns or schema synchronization delays in trace session: {0}.")]
        internal void TraceEventServiceEventsWillBeLost(string sessionName)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(806, sessionName);
            }
        }

        [Event(807, Level = EventLevel.Warning, Keywords = Keywords.TraceEvent, Message = "The loss of {1} events was detected in trace session '{0}'.")]
        internal void TraceEventServiceProcessEventsLost(string sessionName, int eventsLost)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(807, sessionName, eventsLost);
            }
        }

        [Event(808, Level = EventLevel.Error, Keywords = Keywords.TraceEvent, Message = "A fault was detected while shutting down the configured listeners. Message: {0}")]
        internal void TraceEventServiceConfigurationShutdownFault(string message)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(808, message);
            }
        }

        [Event(809, Level = EventLevel.Error, Keywords = Keywords.TraceEvent, Message = "A fault was detected while loading the configuration file. Message: {0}")]
        internal void TraceEventServiceConfigurationFileLoadFault(string message)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(809, message);
            }
        }

        [Event(810, Level = EventLevel.Critical, Keywords = Keywords.TraceEvent, Message = "An improperly generated manifest was received for provider {0}. Message: {1}")]
        internal void TraceEventServiceManifestGenerationFault(Guid providerId, string message)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(810, providerId, message);
            }
        }

        [Event(811, Level = EventLevel.LogAlways, Keywords = Keywords.TraceEvent, Message = "Out of band event level {2} for provider {1} on session {0}. Message: {4}")]
        internal void TraceEventServiceOutOfBandEvent(string sessionName, Guid providerId, EventLevel eventLevel, EventKeywords eventKeywords, string eventMessage)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(811, sessionName, providerId, eventLevel, eventKeywords, eventMessage);
            }
        }

        [Event(900, Level = EventLevel.Warning, Keywords = Keywords.Sink, Message = "The buffering capacity of {0} events for the sink {1} has been reached. New entries will be discarded.")]
        internal void BufferedEventPublisherCapacityOverloaded(int bufferBoundedCapacity, string sinkId)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(900, bufferBoundedCapacity, sinkId);
            }
        }

        [Event(901, Level = EventLevel.Informational, Keywords = Keywords.Sink, Message = "The buffering capacity for the sink {0} was restored.")]
        internal void BufferedEventPublisherCapacityRestored(string sinkId)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(901, sinkId);
            }
        }

        [Event(902, Level = EventLevel.Warning, Keywords = Keywords.Sink, Message = "{0} events could not be sent to the sink {1} and will be lost.")]
        internal void BufferedEventPublisherEventsLostWhileDisposing(int eventsLost, string sinkId)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(902, eventsLost, sinkId);
            }
        }

        [Event(903, Level = EventLevel.Critical, Keywords = Keywords.Sink, Message = "An unobserved fault was detected in the sink {0}. Message: {1}")]
        internal void BufferedEventPublisherUnobservedTaskFault(string sinkId, string message)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(903, sinkId, message);
            }
        }

        [Event(1000, Level = EventLevel.Error, Keywords = Keywords.Sink, Message = "Parsing the manifest for provider '{0}' to handle the event with ID {1} failed. Message: {2}")]
        internal void ParsingEventSourceManifestFailed(string providerName, int eventId, string message)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(1000, providerName, eventId, message);
            }
        }

        [Event(1100, Level = EventLevel.Critical, Keywords = Keywords.Sink, Message = "Formatting an entry failed. Message: {0}")]
        internal void FormatEntryAsStringFailed(string message)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(1100, message);
            }
        }

        [Event(1101, Level = EventLevel.Warning, Keywords = Keywords.Sink, Message = "Mapping the event level {0} to a color failed. Message: {0}")]
        internal void MapEntryLevelToColorFailed(int eventLevel, string message)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(1101, eventLevel, message);
            }
        }

        /// <summary>
        /// Custom defined event keywords.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1034:NestedTypesShouldNotBeVisible", Justification = "As designed, part of the code pattern to author an event source.")]
        public static class Keywords
        {
            /// <summary>
            /// Keyword for sink.
            /// </summary>
            public const EventKeywords Sink = (EventKeywords)0x0001;

            /// <summary>
            /// Keyword for formatting.
            /// </summary>
            public const EventKeywords Formatting = (EventKeywords)0x0002;

            /// <summary>
            /// Keyword for trace event.
            /// </summary>
            public const EventKeywords TraceEvent = (EventKeywords)0x0004;
        }
    }
}
