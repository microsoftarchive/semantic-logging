// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Formatters;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Observable;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Utility
{
    /// <summary>
    /// Extensions for <see cref="EventEntry"/>.
    /// </summary>
    public static class EventEntryExtensions
    {
        /// <summary>
        /// Subscribes an <see cref="IObserver{String}"/> sink by doing a straight projection of a sequence of <see cref="EventEntry"/> and applying
        /// a format using a <see cref="IEventTextFormatter"/> instance to convert it to a <see cref="string"/> message.
        /// </summary>
        /// <param name="source">The original stream of events.</param>
        /// <param name="formatter">The formatter to use.</param>
        /// <param name="sink">The underlying sink.</param>
        /// <returns>A subscription token to unsubscribe to the event stream.</returns>
        /// <remarks>When using Reactive Extensions (Rx), this is equivalent to doing a Select statement on the <paramref name="source"/> to convert it to <see cref="IObservable{String}"/> and then
        /// calling Subscribe on it.
        /// </remarks>
        public static IDisposable SubscribeWithFormatter(this IObservable<EventEntry> source, IEventTextFormatter formatter, IObserver<string> sink)
        {
            return source.CreateSubscription(sink, entry => entry.TryFormatAsString(formatter));
        }

        /// <summary>
        /// Subscribes a sink by doing a straight projection of a sequence of <see cref="EventEntry"/> and applying
        /// a format using a <see cref="IEventTextFormatter"/> and a <see cref="IConsoleColorMapper"/> instances to convert it 
        /// to a sequence of entries for that sink.
        /// </summary>
        /// <param name="source">The original stream of events.</param>
        /// <param name="formatter">The formatter to use.</param>
        /// <param name="colorMapper">The color mapper.</param>
        /// <param name="sink">The underlying sink.</param>
        /// <returns>A subscription token to unsubscribe to the event stream.</returns>
        /// <remarks>When using Reactive Extensions (Rx), this is equivalent to doing a Select statement on the <paramref name="source"/> to convert it to a <see cref="Tuple"/> and then
        /// calling Subscribe on it.
        /// </remarks>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1006:DoNotNestGenericTypesInMemberSignatures", Justification = "By design")]
        public static IDisposable SubscribeWithFormatterAndColor(this IObservable<EventEntry> source, IEventTextFormatter formatter, IConsoleColorMapper colorMapper, IObserver<Tuple<string, ConsoleColor?>> sink)
        {
            return source.CreateSubscription(sink, entry => entry.TryFormatAsStringAndColor(formatter, colorMapper));
        }

        /// <summary>
        /// Formats an <see cref="EventEntry"/> as a string using an <see cref="IEventTextFormatter"/>.
        /// </summary>
        /// <param name="entry">The entry to format.</param>
        /// <param name="formatter">The formatter to use.</param>
        /// <returns>A formatted entry, or <see langword="null"/> if an exception is thrown by the <paramref name="formatter"/>.</returns>
        public static string TryFormatAsString(this EventEntry entry, IEventTextFormatter formatter)
        {
            try
            {
                return formatter.WriteEvent(entry);
            }
            catch (Exception e)
            {
                SemanticLoggingEventSource.Log.FormatEntryAsStringFailed(e.ToString());
            }

            return null;
        }

        /// <summary>
        /// Formats an <see cref="EventEntry"/> as a string using an <see cref="IEventTextFormatter"/>, and
        /// assigns a <see cref="ConsoleColor"/> depending on the event level.
        /// </summary>
        /// <param name="entry">The entry to format.</param>
        /// <param name="formatter">The formatter to use.</param>
        /// <param name="colorMapper">The color mapper to user.</param>
        /// <returns>A formatted entry with its color, or <see langword="null"/> if an exception is thrown by the <paramref name="formatter"/>.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1006:DoNotNestGenericTypesInMemberSignatures", Justification = "By design")]
        public static Tuple<string, ConsoleColor?> TryFormatAsStringAndColor(this EventEntry entry, IEventTextFormatter formatter, IConsoleColorMapper colorMapper)
        {
            Guard.ArgumentNotNull(entry, "entry");
            Guard.ArgumentNotNull(formatter, "formatter");
            Guard.ArgumentNotNull(colorMapper, "colorMapper");

            var message = TryFormatAsString(entry, formatter);
            if (message != null)
            {
                try
                {
                    var color = colorMapper.Map(entry.Schema.Level);
                    return Tuple.Create(message, color);
                }
                catch (Exception e)
                {
                    SemanticLoggingEventSource.Log.MapEntryLevelToColorFailed((int)entry.Schema.Level, e.ToString());
                    return Tuple.Create(message, (ConsoleColor?)null);
                }
            }

            return null;
        }
    }
}
