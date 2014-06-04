// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Tracing;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Schema;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Utility;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging
{
    /// <summary>
    /// Represents a entry to log, with additional context information.
    /// </summary>
    public class EventEntry
    {
        /// <summary>
        /// The default date time format for a formatter date values. 
        /// Default as Round-trip value; "yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fffffffzzz".
        /// </summary>
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly", Justification = "Reviewed.")]
        internal const string DefaultDateTimeFormat = "O";

        private static readonly int CurrentProcessId = ProcessPropertyAccess.GetCurrentProcessId();

        private readonly Guid providerId;
        private readonly int eventId;
        private readonly string formattedMessage;
        private readonly ReadOnlyCollection<object> payload;
        private readonly DateTimeOffset timestamp;
        private readonly EventSchema schema;
        private readonly int processId;
        private readonly int threadId;
        private readonly Guid activityId;
        private readonly Guid relatedActivityId;

        /// <summary>
        /// Initializes a new instance of the <see cref="EventEntry" /> class.
        /// </summary>
        /// <param name="sourceId">The source id.</param>
        /// <param name="eventId">The event id.</param>
        /// <param name="formattedMessage">The message.</param>
        /// <param name="payload">The payload.</param>
        /// <param name="timestamp">The timestamp.</param>
        /// <param name="schema">The schema.</param>
        public EventEntry(Guid sourceId, int eventId, string formattedMessage, ReadOnlyCollection<object> payload, DateTimeOffset timestamp, EventSchema schema)
            : this(sourceId, eventId, formattedMessage, payload, timestamp, Guid.Empty, Guid.Empty, schema)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EventEntry" /> class.
        /// </summary>
        /// <param name="sourceId">The source id.</param>
        /// <param name="eventId">The event id.</param>
        /// <param name="formattedMessage">The message.</param>
        /// <param name="payload">The payload.</param>
        /// <param name="timestamp">The timestamp.</param>
        /// <param name="activityId">The activity id.</param>
        /// <param name="relatedActivityId">The related activity id.</param>
        /// <param name="schema">The schema.</param>
        public EventEntry(Guid sourceId, int eventId, string formattedMessage, ReadOnlyCollection<object> payload, DateTimeOffset timestamp, Guid activityId, Guid relatedActivityId, EventSchema schema)
            : this(sourceId, eventId, formattedMessage, payload, timestamp, 0, 0, activityId, relatedActivityId, schema)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EventEntry" /> class.
        /// </summary>
        /// <param name="sourceId">The source id.</param>
        /// <param name="eventId">The event id.</param>
        /// <param name="formattedMessage">The message.</param>
        /// <param name="payload">The payload.</param>
        /// <param name="timestamp">The timestamp.</param>
        /// <param name="processId">The process id.</param>
        /// <param name="threadId">The thread id.</param>
        /// <param name="activityId">The activity id.</param>
        /// <param name="relatedActivityId">The related activity id.</param>
        /// <param name="schema">The schema.</param>
        public EventEntry(Guid sourceId, int eventId, string formattedMessage, ReadOnlyCollection<object> payload, DateTimeOffset timestamp, int processId, int threadId, Guid activityId, Guid relatedActivityId, EventSchema schema)
        {
            this.providerId = sourceId;
            this.eventId = eventId;
            this.formattedMessage = formattedMessage;
            this.payload = payload;
            this.timestamp = timestamp;
            this.processId = processId;
            this.threadId = threadId;
            this.activityId = activityId;
            this.relatedActivityId = relatedActivityId;
            this.schema = schema;
        }

        /// <summary>
        /// Gets the id of the source originating the event.
        /// </summary>
        /// <value>The provider id.</value>
        public Guid ProviderId
        {
            get { return this.providerId; }
        }

        /// <summary>
        /// Gets the event id.
        /// </summary>
        /// <value>The event id.</value>
        public int EventId
        {
            get { return this.eventId; }
        }

        /// <summary>
        /// Gets the event payload.
        /// </summary>
        /// <value>The event payload.</value>
        public ReadOnlyCollection<object> Payload
        {
            get { return this.payload; }
        }

        /// <summary>
        /// Gets the timestamp of the event.
        /// </summary>
        /// <value>The timestamp of the event.</value>
        public DateTimeOffset Timestamp
        {
            get { return this.timestamp; }
        }

        /// <summary>
        /// Gets the event schema.
        /// </summary>
        /// <value>The event schema.</value>
        public EventSchema Schema
        {
            get { return this.schema; }
        }

        /// <summary>
        /// Gets the formatted message.
        /// </summary>
        /// <value>
        /// The formatted message.
        /// </value>
        public string FormattedMessage
        {
            get { return this.formattedMessage; }
        }

        /// <summary>
        /// Gets the process id.
        /// </summary>
        /// <value>
        /// The process id.
        /// </value>
        public int ProcessId
        {
            get { return this.processId; }
        }

        /// <summary>
        /// Gets the thread id.
        /// </summary>
        /// <value>
        /// The thread id.
        /// </value>
        public int ThreadId
        {
            get { return this.threadId; }
        }

        /// <summary>
        /// Gets the activity id.
        /// </summary>
        /// <value>
        /// The activity id.
        /// </value>
        public Guid ActivityId
        {
            get { return this.activityId; }
        }

        /// <summary>
        /// Gets the related activity id.
        /// </summary>
        /// <value>
        /// The related activity id.
        /// </value>
        public Guid RelatedActivityId
        {
            get { return this.relatedActivityId; }
        }

        /// <summary>
        /// Creates a new <see cref="EventEntry"/> instance based on the <paramref name="args"/> and the <paramref name="schema"/>.
        /// </summary>
        /// <param name="args">The <see cref="EventWrittenEventArgs"/> representing the event to log.</param>
        /// <param name="schema">The <see cref="EventSchema"/> for the source originating the event.</param>
        /// <returns>An entry describing the event.</returns>
        public static EventEntry Create(EventWrittenEventArgs args, EventSchema schema)
        {
            Guard.ArgumentNotNull(args, "args");
            Guard.ArgumentNotNull(schema, "schema");

            var timestamp = DateTimeOffset.Now;

            // TODO: validate whether we want to do this pro-actively or should we wait until the
            // last possible moment (as the formatted message might not be used in a sink).
            string formattedMessage = null;
            if (args.Message != null)
            {
                formattedMessage = string.Format(CultureInfo.InvariantCulture, args.Message, args.Payload.ToArray());
            }

            return new EventEntry(
                args.EventSource.Guid,
                args.EventId,
                formattedMessage,
                args.Payload,
                timestamp,
                CurrentProcessId,
                ProcessPropertyAccess.GetCurrentThreadId(),
                ActivityIdPropertyAccess.GetActivityId(args),
                ActivityIdPropertyAccess.GetRelatedActivityId(args),
                schema);
        }

        /// <summary>
        /// Gets the formatted timestamp.
        /// </summary>
        /// <param name="format">The format.</param>
        /// <returns>The formatted string.</returns>
        public string GetFormattedTimestamp(string format)
        {
            return this.timestamp.UtcDateTime.ToString(string.IsNullOrWhiteSpace(format) ? DefaultDateTimeFormat : format, CultureInfo.InvariantCulture);
        }

        private static class ActivityIdPropertyAccess
        {
            private static readonly Func<EventWrittenEventArgs, Guid> ActivityIdAccessor;
            private static readonly Func<EventWrittenEventArgs, Guid> RelatedActivityIdAccessor;

            static ActivityIdPropertyAccess()
            {
                var eventArgsType = typeof(EventWrittenEventArgs);

                ActivityIdAccessor = BuildAccessor(eventArgsType.GetProperty("ActivityId"));
                RelatedActivityIdAccessor = BuildAccessor(eventArgsType.GetProperty("RelatedActivityId"));
            }

            public static Guid GetActivityId(EventWrittenEventArgs args)
            {
                return ActivityIdAccessor(args);
            }

            public static Guid GetRelatedActivityId(EventWrittenEventArgs args)
            {
                return RelatedActivityIdAccessor(args);
            }

            private static Func<EventWrittenEventArgs, Guid> BuildAccessor(PropertyInfo property)
            {
                if (property != null && property.PropertyType == typeof(Guid))
                {
                    var parameter = Expression.Parameter(typeof(EventWrittenEventArgs), "args");

                    try
                    {
                        return
                            Expression.Lambda<Func<EventWrittenEventArgs, Guid>>(
                                Expression.Property(parameter, property),
                                parameter).Compile();
                    }
                    catch (SecurityException)
                    {
                        // fall back to no access to the properties
                    }
                }

                return args => Guid.Empty;
            }
        }

        private static class ProcessPropertyAccess
        {
            private static readonly Func<int> ProcessIdAccessor;
            private static readonly Func<int> CurrentThreadIdAccessor;

            static ProcessPropertyAccess()
            {
                if (AppDomain.CurrentDomain.IsHomogenous && AppDomain.CurrentDomain.IsFullyTrusted)
                {
                    ProcessIdAccessor = GetCurrentProcessIdSafe;
                    CurrentThreadIdAccessor = GetCurrentThreadIdSafe;
                }
                else
                {
                    ProcessIdAccessor = null;
                    CurrentThreadIdAccessor = null;
                }
            }

            public static int GetCurrentProcessId()
            {
                return ProcessIdAccessor != null ? ProcessIdAccessor() : 0;
            }

            public static int GetCurrentThreadId()
            {
                return CurrentThreadIdAccessor != null ? CurrentThreadIdAccessor() : 0;
            }

            [SecuritySafeCritical]
            private static int GetCurrentProcessIdSafe()
            {
                try
                {
                    return SafeNativeMethods.GetCurrentProcessId();
                }
                catch (SecurityException)
                {
                    return 0;
                }
            }

            [SecuritySafeCritical]
            private static int GetCurrentThreadIdSafe()
            {
                try
                {
                    return SafeNativeMethods.GetCurrentThreadId();
                }
                catch (SecurityException)
                {
                    return 0;
                }
            }

            [SuppressUnmanagedCodeSecurity]
            [SecurityCritical]
            private static class SafeNativeMethods
            {
                [DllImport("kernel32.dll")]
                public static extern int GetCurrentProcessId();

                [DllImport("kernel32.dll")]
                public static extern int GetCurrentThreadId();
            }
        }
    }
}
