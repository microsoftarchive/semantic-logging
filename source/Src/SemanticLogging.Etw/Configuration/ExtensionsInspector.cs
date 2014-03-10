// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Configuration;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw.Utility;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw.Configuration
{
    /// <summary>
    /// Inspect extensions without loading assemblies into the current domain.
    /// </summary>
    [Serializable]
    internal class ExtensionsInspector : MarshalByRefObject
    {
        internal IEnumerable<string> ExtensionFiles { get; private set; }

        internal static ExtensionsInspector CreateInstance(IEnumerable<string> filesToInspect)
        {
            var inspectorDomain = AppDomain.CreateDomain("ExtensionsInspector", AppDomain.CurrentDomain.Evidence, AppDomain.CurrentDomain.SetupInformation);
            try
            {
                var instance = (ExtensionsInspector)inspectorDomain.CreateInstanceAndUnwrap(Assembly.GetExecutingAssembly().FullName, typeof(ExtensionsInspector).FullName);

                instance.ProbeExtensions(filesToInspect.ToArray()); // ToArray(): avoid Linq serialization marshalling for cross AppDoamins 

                return new ExtensionsInspector() { ExtensionFiles = new List<string>(instance.ExtensionFiles) };
            }
            finally
            {
                AppDomain.Unload(inspectorDomain);
            }
        }

        internal static Assembly LoadAssembly(string path)
        {
            try
            {
                return Assembly.LoadFrom(path);
            }
            catch (BadImageFormatException)
            {
                return null; // not a managed dll
            }
        }

        internal void ProbeExtensions(string[] files)
        {
            var approvedFiles = new HashSet<string>();

            foreach (var file in files)
            {
                Assembly asm = LoadAssembly(file);
                if (asm != null && !asm.IsDynamic && !asm.IsFrameworkAssembly())
                {
                    foreach (Type type in asm.GetTypes())
                    {
                        if (typeof(ISinkElement).IsAssignableFrom(type) ||
                            typeof(IFormatterElement).IsAssignableFrom(type))
                        {
                            approvedFiles.Add(type.Assembly.Location);
                            break;
                        }
                    }
                }
            }

            this.ExtensionFiles = approvedFiles;
        }
    }
}
