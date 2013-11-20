// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw.Utility
{
    internal static class AssemblyExtensions
    {
        private static List<byte[]> frameworkTokens = new List<byte[]>()
        {
            new byte[] { 0xb7, 0x7a, 0x5c, 0x56, 0x19, 0x34, 0xe0, 0x89 },
            new byte[] { 0xb0, 0x3f, 0x5f, 0x7f, 0x11, 0xd5, 0x0a, 0x3a }
            //// Key used for Entlib
            ////new byte[] { 0x31, 0xbf, 0x38, 0x56, 0xad, 0x36, 0x4e, 0x35 }
        };

        public static string ToProductString(this Assembly assembly)
        {
            StringBuilder sb = new StringBuilder();

            string assemblyProduct = FromAttribute<AssemblyProductAttribute>(assembly).Product;

            string assemblyCopyright = FromAttribute<AssemblyCopyrightAttribute>(assembly).Copyright;

            if (string.IsNullOrWhiteSpace(assemblyCopyright))
            {
                assemblyCopyright = FromAttribute<AssemblyCompanyAttribute>(assembly).Company;
            }

            string assemblyDescription = FromAttribute<AssemblyDescriptionAttribute>(assembly).Description ?? assembly.GetName().Name;

            sb.AppendLine(assemblyDescription);
            sb.AppendFormat("{0} v{1}", assemblyProduct, assembly.GetName().Version.ToString(2));
            sb.AppendLine();
            sb.AppendLine(assemblyCopyright);
            sb.AppendLine();

            return sb.ToString();
        }

        public static bool IsFrameworkAssembly(this Assembly assembly)
        {
            byte[] publicKeyToken = assembly.GetName().GetPublicKeyToken();

            return publicKeyToken.Length > 0 && frameworkTokens.Any(t => t.SequenceEqual(publicKeyToken));
        }

        private static T FromAttribute<T>(Assembly assembly) where T : Attribute
        {
            return (assembly.GetCustomAttributes(typeof(T), false).FirstOrDefault() as T) ?? Activator.CreateInstance<T>();
        }
    }
}
