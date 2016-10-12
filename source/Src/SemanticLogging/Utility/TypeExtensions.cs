// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.ComponentModel;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Utility
{
    internal static class TypeExtensions
    {
        public static object Default(this Type type)
        {
            if (type == typeof(string))
            {
                return string.Empty;
            }

            if (type == typeof(byte[]))
            {
                return new byte[] { };
            }

            return Activator.CreateInstance(type);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters", MessageId = "System.ComponentModel.TypeConverter.ConvertFromInvariantString(System.String)", Justification = "Converting numeric value")]
        public static object NotDefault(this Type type)
        {
            if (type == typeof(string))
            {
                return "1";
            }

            if (type == typeof(Guid))
            {
                return Guid.NewGuid();
            }

            if (type == typeof(bool))
            {
                return true;
            }
            
            if (type == typeof(DateTime))
            {
                return DateTime.MaxValue;
            }

            if (type == typeof(byte[]))
            {
                return new byte[] { 1, 2, 3 };
            }

            TypeConverter tc = TypeDescriptor.GetConverter(type);
            if (tc != null && tc.CanConvertFrom(typeof(string)))
            {
                return tc.ConvertFromInvariantString("1");
            }

            return Activator.CreateInstance(type);
        }

        public static bool IsDefault(this object value)
        {
            if (value == null)
            {
                return true;
            }

            return value.Equals(value.GetType().Default());
        }
    }
}
