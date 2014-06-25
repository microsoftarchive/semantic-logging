// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.Shared.TestSupport
{
    public static class AssemblyLoaderHelper
    {
        public static void EnsureAllAssembliesAreLoadedForSinkTest()
        {
            EnsureAssemblyIsLoaded("Microsoft.Practices.EnterpriseLibrary.SemanticLogging.WindowsAzure", "Microsoft.Practices.EnterpriseLibrary.SemanticLogging.WindowsAzure.dll");
            EnsureAssemblyIsLoaded("Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Database", "Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Database.dll");
            EnsureAssemblyIsLoaded("Microsoft.Practices.EnterpriseLibrary.SemanticLogging.TextFile", "Microsoft.Practices.EnterpriseLibrary.SemanticLogging.TextFile.dll");
        }

        private static void EnsureAssemblyIsLoaded(string assemblyName, string assemblyFileName)
        {
            var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
            if (!loadedAssemblies.Any(a => a.FullName.StartsWith(assemblyName)))
            {
                var assemblyBytes = File.ReadAllBytes(assemblyFileName);
                AppDomain.CurrentDomain.Load(assemblyBytes);
            }
        }
    }
}
