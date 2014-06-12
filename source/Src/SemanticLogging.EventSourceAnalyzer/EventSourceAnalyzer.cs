// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Properties;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Schema;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Utility
{
    /// <summary>
    /// Helper class to validate the correctness of <see cref="EventSource"/> instances. Useful in unit tests.
    /// </summary>
    /// <example>
    ///   <code>EventSourceAnalyzer.InspectAll(MyEventSource.Instance);</code>
    ///   <remarks>Where 'Instance' property returns a singleton instance of 'MyEventSource' class like:
    ///     <code>public static MyEventSource Instance = new MyEventSource();</code>
    ///   </remarks>
    /// </example>
    public sealed class EventSourceAnalyzer
    {
        private const BindingFlags Bindings = BindingFlags.Instance |
                                              BindingFlags.DeclaredOnly |
                                              BindingFlags.InvokeMethod |
                                              BindingFlags.NonPublic |
                                              BindingFlags.Public;

        /// <summary>
        /// Gets or sets a value indicating whether to exclude the internal <see cref="System.Diagnostics.Tracing.EventListener"/> instance to emulate sending events.
        /// </summary>
        /// <remarks>
        /// The analyzer performs a 'probing' execution using an internal <see cref="System.Diagnostics.Tracing.EventListener"/> instance to emulate logging using the inspected <see cref="EventSource"/> instance.
        /// By excluding this analysis, no ETW events will be sent in case of executing the analysis from a running application where ETW events may be monitored.
        /// </remarks>
        /// <value>
        /// <c>true</c> for excluding this analysis, <c>false</c> otherwise.
        /// </value>
        public bool ExcludeEventListenerEmulation { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to exclude an exact type mapping between the event arguments and 'EventSource.WriteEvent' argument.
        /// </summary>
        /// <value>
        /// <c>true</c> for excluding this analysis, <c>false</c> otherwise.
        /// </value>
        public bool ExcludeWriteEventTypeMapping { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to exclude the type order analysis between the event arguments and the 'EventSource.WriteEvent' arguments order.
        /// This process is costly and may be excluded for large event sources with events with many arguments. 
        /// However it is recommended to leave this option off (default) to ensure that all 'EventSource.WriteEvent' arguments are correctly mapped to the event parameters.
        /// </summary>
        /// <value>
        /// <c>true</c> for excluding this analysis, <c>false</c> otherwise.
        /// </value>
        public bool ExcludeWriteEventTypeOrder { get; set; }

        /// <summary>
        /// Inspects the specified <see cref="EventSource" /> for potential runtime errors.
        /// </summary>
        /// <param name="eventSource">The event source instance to inspect.</param>
        /// <example>
        ///   <code>EventSourceAnalyzer.InspectAll(MyEventSource.Instance);</code>
        ///   <remarks>Where 'Instance' property returns a singleton instance of 'MyEventSource' class like:
        ///     <code>public static MyEventSource Instance = new MyEventSource();</code>
        ///   </remarks>
        /// </example>
        /// <exception cref="EventSourceAnalyzerException">Exception thrown if a failure was found in the specified <see cref="System.Diagnostics.Tracing.EventSource" />.</exception>
        /// <exception cref="ArgumentException">Exception thrown if a failure was found in the specified <see cref="System.Diagnostics.Tracing.EventSource" />.</exception>
        public static void InspectAll(EventSource eventSource)
        {
            var instance = new EventSourceAnalyzer();
            instance.Inspect(eventSource);
        }

        /// <summary>
        /// Inspects the specified <see cref="System.Diagnostics.Tracing.EventSource" /> for potential runtime errors 
        /// filtering out validations according to the specified instance properties.
        /// </summary>
        /// <param name="eventSource">The event source instance to inspect.</param>
        /// <example>
        ///   <code>EventSourceAnalyzer.InspectAll(MyEventSource.Instance);</code>
        ///   <remarks>Where 'Instance' property returns a singleton instance of 'MyEventSource' class like:
        ///   <code>public static MyEventSource Instance = new MyEventSource();</code>
        ///   </remarks>
        /// </example>
        /// <exception cref="EventSourceAnalyzerException">Exception thrown if a failure was found in the specified <see cref="System.Diagnostics.Tracing.EventSource" />.</exception>
        /// <exception cref="ArgumentException">Exception thrown if a failure was found in the specified <see cref="System.Diagnostics.Tracing.EventSource" />.</exception>
        public void Inspect(EventSource eventSource)
        {
            Guard.ArgumentNotNull(eventSource, "eventSource");

            // Check internal validation when binding to a listener with EnableEvents().
            this.CheckEnableEvents(eventSource);

            var eventSchemas = this.GetEventSchemas(eventSource);
            if (eventSchemas.Count == 0)
            {
                throw new EventSourceAnalyzerException(Resources.EventSourceAnalyzerNoEventsError);
            }

            if (!this.ExcludeEventListenerEmulation)
            {
                foreach (EventSchema eventSchema in eventSchemas)
                {
                    this.ProbeEvent(eventSchema, eventSource);
                }
            }
        }

        private MethodInfo GetMethodFromSchema(EventSource source, EventSchema schema)
        {
            return source.GetType().GetMethods(Bindings).SingleOrDefault(m => this.IsEvent(m, schema.Id)) ??
                   source.GetType().GetMethod(schema.TaskName, Bindings);
        }

        private bool IsEvent(MethodInfo method, int eventId)
        {
            return method.GetCustomAttribute<EventAttribute>() != null &&
                   method.GetCustomAttribute<EventAttribute>().EventId == eventId;
        }

        private void CheckEnableEvents(EventSource eventSource)
        {
            using (var listener = new ProbeEventListener())
            {
                //// EnableEvents will do a general validation pass
                listener.EnableEvents(eventSource, EventLevel.LogAlways);
                //// Restore source
                listener.DisableEvents(eventSource);
            }
        }

        private ICollection<EventSchema> GetEventSchemas(EventSource eventSource)
        {
            try
            {
                string manifest = EventSource.GenerateManifest(eventSource.GetType(), null);
                this.CheckForBadFormedManifest(manifest);
                return new EventSourceSchemaReader().GetSchema(manifest).Values;
            }
            catch (EventSourceAnalyzerException)
            {
                throw;
            }
            catch (Exception e)
            {
                throw new EventSourceAnalyzerException(string.Format(CultureInfo.CurrentCulture, Properties.Resources.EventSourceAnalyzerManifestGenerationError, e.Message, EventSource.GenerateManifest(eventSource.GetType(), null)));
            }
        }

        private void CheckForBadFormedManifest(string manifest)
        {
            // check for map values for Enum types other than Int21 and Int64.
            if (manifest.IndexOf("\"0x\"", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                throw new EventSourceAnalyzerException(Properties.Resources.EventSourceAnalyzerBadFormedManifestError);
            }
        }

        private void ProbeEvent(EventSchema eventSchema, EventSource source)
        {
            using (var listener = new ProbeEventListener(eventSchema, this))
            {
                try
                {
                    listener.EnableEvents(source, eventSchema.Level, eventSchema.Keywords);
                    MethodInfo method;
                    if (this.TryInvokeMethod(eventSchema, source, listener, out method))
                    {
                        if (listener.EventData == null)
                        {
                            throw new EventSourceAnalyzerException(string.Format(CultureInfo.CurrentCulture, Properties.Resources.EventSourceAnalyzerMissingWriteEventCallError, method.Name));
                        }

                        if (listener.Error != null)
                        {
                            throw listener.Error;
                        }
                    }
                }
                finally
                {
                    listener.DisableEvents(source);
                }
            }
        }

        private bool TryInvokeMethod(EventSchema eventSchema, EventSource source, ProbeEventListener listener, out MethodInfo method)
        {
            //// Find the method that matches event id or task name
            method = this.GetMethodFromSchema(source, eventSchema);

            if (method != null)
            {
                try
                {
                    // Call with default values to perform all checks but order
                    ParameterInfo[] parameters = method.GetParameters();
                    object[] defaultValues = parameters.Select(p => p.ParameterType.Default()).ToArray();
                    method.Invoke(source, defaultValues);

                    if (listener.Error == null && !this.ExcludeWriteEventTypeOrder)
                    {
                        // Invoke the method at most N-1 times, where N == params count.
                        for (int i = 0; i < parameters.Length - 1; i++)
                        {
                            listener.TypeOrderOffset = i;
                            defaultValues[i] = parameters[i].ParameterType.NotDefault();
                            method.Invoke(source, defaultValues);
                            if (listener.Error != null)
                            {
                                break;
                            }
                        }
                    }

                    return true;
                }
                catch (ArgumentException e)
                {
                    throw new EventSourceAnalyzerException(string.Format(CultureInfo.CurrentCulture, Properties.Resources.EventSourceAnalyzerMethodCallError, method.Name), e);
                }
            }

            return false;
        }

        private class ProbeEventListener : EventListener
        {
            private EventSchema eventSchema;
            private EventSourceAnalyzer analyzer;

            internal ProbeEventListener(EventSchema eventSchema = null, EventSourceAnalyzer analyzer = null)
            {
                this.eventSchema = eventSchema;
                this.analyzer = analyzer;
                this.TypeOrderOffset = -1;
            }

            internal EventWrittenEventArgs EventData { get; set; }

            internal Exception Error { get; set; }

            internal int TypeOrderOffset { get; set; }

            [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Exception is stored for further analysis")]
            protected override void OnEventWritten(EventWrittenEventArgs eventData)
            {
                this.EventData = eventData;

                try
                {
                    if (this.TypeOrderOffset != -1)
                    {
                        this.CheckParametersOrder();
                        return;
                    }

                    this.CheckPayload();
                }
                catch (Exception e)
                {
                    this.Error = e;
                }
            }

            private static bool EqualTypes(Type parameterType, Type payloadType)
            {
                return parameterType.IsEnum && !payloadType.IsEnum ?
                    parameterType.GetEnumUnderlyingType() == payloadType :
                    parameterType == payloadType;
            }

            private void CheckParametersOrder()
            {
                MethodInfo eventMethod = this.analyzer.GetMethodFromSchema(this.EventData.EventSource, this.eventSchema);
                ParameterInfo[] eventParameters = eventMethod.GetParameters();
                int payloadParameterOffset = 0;
                if (this.HasRelatedActivityId(eventParameters))
                {
                    if (this.TypeOrderOffset == 0)
                    {
                        // ignore the relatedActivityId parameter (first by convention)
                        return;
                    }

                    payloadParameterOffset = 1;
                }

                //// If we get the default value then order is wrong becase 
                //// we should get a value != default
                if (this.EventData.Payload[this.TypeOrderOffset - payloadParameterOffset].IsDefault())
                {
                    this.Error = new EventSourceAnalyzerException(string.Format(CultureInfo.CurrentCulture,
                        Properties.Resources.EventSourceAnalyzerMismatchParametersOrder,
                        eventParameters[this.TypeOrderOffset].Name,
                        eventMethod.Name));
                }
            }

            private void CheckPayload()
            {
                MethodInfo eventMethod = this.analyzer.GetMethodFromSchema(this.EventData.EventSource, this.eventSchema);
                ParameterInfo[] eventParameters = eventMethod.GetParameters();
                int payloadParameterOffset = this.HasRelatedActivityId(eventParameters) ? 1 : 0;

                if (eventParameters.Length != this.EventData.Payload.Count + payloadParameterOffset)
                {
                    this.Error = new EventSourceAnalyzerException(string.Format(CultureInfo.CurrentCulture, Properties.Resources.EventSourceAnalyzerDifferentParameterCount, eventMethod.Name));
                    return;
                }

                for (int i = 0; i < this.EventData.Payload.Count; i++)
                {
                    if (this.EventData.Payload[i] == null)
                    {
                        // We don't expect null values here so flag it and exit
                        this.Error = new EventSourceAnalyzerException(string.Format(CultureInfo.CurrentCulture,
                            Properties.Resources.EventSourceAnalyzerNullPayloadValue,
                            i,
                            eventParameters[i + payloadParameterOffset].Name,
                            eventMethod.Name));
                        break;
                    }

                    Type payloadType = this.EventData.Payload[i].GetType();

                    //// Check that event args types matches WriteEvent arg types
                    if (!this.analyzer.ExcludeWriteEventTypeMapping &&
                        !EqualTypes(eventParameters[i + payloadParameterOffset].ParameterType, payloadType))
                    {
                        this.Error = new EventSourceAnalyzerException(string.Format(CultureInfo.CurrentCulture,
                            Properties.Resources.EventSourceAnalyzerMismatchParametersType,
                            eventParameters[i + payloadParameterOffset].Name,
                            eventParameters[i + payloadParameterOffset].ParameterType,
                            payloadType,
                            eventMethod.Name));
                        break;
                    }
                }
            }

            private bool HasRelatedActivityId(ParameterInfo[] eventParameters)
            {
                if (eventParameters.Length > 0
                    && eventParameters[0].ParameterType == typeof(Guid)
                    && string.Equals(eventParameters[0].Name, "relatedActivityId", StringComparison.Ordinal)
                    && (this.eventSchema.Opcode == EventOpcode.Send || this.eventSchema.Opcode == EventOpcode.Receive))
                {
                    return true;
                }

                return false;
            }
        }
    }
}
