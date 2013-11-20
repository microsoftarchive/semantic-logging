// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw.Service
{
    internal class Parameter
    {
        public Parameter(string name, string description, Action<ParameterSet> action)
        {
            this.Names = new HashSet<string>(name.Split(new string[] { "|" }, StringSplitOptions.RemoveEmptyEntries));
            this.Action = action;
            this.Description = description;
        }

        public ICollection<string> Names { get; set; }

        public Action<ParameterSet> Action { get; set; }

        public string Description { get; set; }
    }
}
