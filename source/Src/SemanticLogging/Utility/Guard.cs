// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Utility
{
    internal static class Guard
    {
        private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();

        /// <summary>
        /// Throws <see cref="ArgumentNullException"/> if the given argument is null.
        /// </summary>
        /// <exception cref="ArgumentNullException"> If tested value if null.</exception>
        /// <param name="argumentValue">Argument value to test.</param>
        /// <param name="argumentName">Name of the argument being tested.</param>
        public static void ArgumentNotNull(object argumentValue, string argumentName)
        {
            if (argumentValue == null)
            {
                throw new ArgumentNullException(argumentName);
            }
        }

        /// <summary>
        /// Throws an exception if the tested string argument is null or the empty string.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown if string value is null.</exception>
        /// <exception cref="ArgumentException">Thrown if the string is empty</exception>
        /// <param name="argumentValue">Argument value to check.</param>
        /// <param name="argumentName">Name of argument being checked.</param>
        public static void ArgumentNotNullOrEmpty(string argumentValue, string argumentName)
        {
            if (argumentValue == null)
            {
                throw new ArgumentNullException(argumentName);
            }

            if (argumentValue.Length == 0)
            {
                throw new ArgumentException(Properties.Resources.ArgumentIsEmptyError, argumentName);
            }
        }

        /// <summary>
        /// Throws an exception if the argumentValue is less than lowerValue.
        /// </summary>
        /// <typeparam name="T">A type that implements <see cref="IComparable"/>.</typeparam>
        /// <param name="lowerValue">The lower value accepted as valid.</param>
        /// <param name="argumentValue">The argument value to test.</param>
        /// <param name="argumentName">Name of the argument.</param>
        /// <exception cref="System.ArgumentOutOfRangeException">Validation error.</exception>
        public static void ArgumentGreaterOrEqualThan<T>(T lowerValue, T argumentValue, string argumentName) where T : struct, IComparable
        {
            if (argumentValue.CompareTo((T)lowerValue) < 0)
            {
                throw new ArgumentOutOfRangeException(argumentName, argumentValue, string.Format(CultureInfo.CurrentCulture, Properties.Resources.ArgumentNotGreaterOrEqualTo, argumentName, lowerValue));
            }
        }

        /// <summary>
        /// Throws an exception if the argumentValue is great than higherValue.
        /// </summary>
        /// <typeparam name="T">A type that implements <see cref="IComparable"/>.</typeparam>
        /// <param name="higherValue">The higher value accepted as valid.</param>
        /// <param name="argumentValue">The argument value to test.</param>
        /// <param name="argumentName">Name of the argument.</param>
        /// <exception cref="System.ArgumentOutOfRangeException">Validation error.</exception>
        public static void ArgumentLowerOrEqualThan<T>(T higherValue, T argumentValue, string argumentName) where T : struct, IComparable
        {
            if (argumentValue.CompareTo((T)higherValue) > 0)
            {
                throw new ArgumentOutOfRangeException(argumentName, argumentValue, string.Format(CultureInfo.CurrentCulture, Properties.Resources.ArgumentNotLowerOrEqualTo, argumentName, higherValue));
            }
        }

        /// <summary>
        /// Throws an exception if the tested TimeSpam argument is not a valid timeout value.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the argument is not null and is not a valid timeout value.</exception>
        /// <param name="argumentValue">Argument value to check.</param>
        /// <param name="argumentName">Name of argument being checked.</param>
        public static void ArgumentIsValidTimeout(TimeSpan? argumentValue, string argumentName)
        {
            if (argumentValue.HasValue)
            {
                long totalMilliseconds = (long)argumentValue.Value.TotalMilliseconds;
                if (totalMilliseconds < (long)-1 || totalMilliseconds > (long)2147483647)
                {
                    throw new ArgumentOutOfRangeException(string.Format(CultureInfo.CurrentCulture, Properties.Resources.TimeSpanOutOfRangeError, argumentName));
                }
            }
        }

        public static void ValidateTimestampPattern(string timestampPattern, string argumentName)
        {
            Guard.ArgumentNotNullOrEmpty(timestampPattern, argumentName);

            foreach (var item in timestampPattern.ToCharArray())
            {
                if (InvalidFileNameChars.Contains(item))
                {
                    throw new ArgumentException("Timestamp contains invalid characters", argumentName);
                }
            }
        }

        /// <summary>
        /// Validate the date time format.
        /// </summary>
        /// <param name="format">The format.</param>
        /// <param name="argumentName">Name of the argument.</param>
        public static void ValidDateTimeFormat(string format, string argumentName)
        {
            if (format == null)
            {
                return;
            }

            try
            {
                DateTime.Now.ToString(format, CultureInfo.InvariantCulture);
            }
            catch (FormatException e)
            {
                throw new ArgumentException(argumentName, Properties.Resources.InvalidDateTimeFormatError, e);
            }
        }
    }
}
