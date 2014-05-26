// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw.Service
{
    internal class Parameter
    {
        public Parameter(string name, string description, Action<ParameterSet, IEnumerable<Tuple<string, string>>> action)
        {
            this.Names = new HashSet<string>(name.Split(new[] { "|" }, StringSplitOptions.RemoveEmptyEntries));
            this.Description = description;
            this.Action = action;
        }

        public Parameter(string name, string description, string key)
        {
            this.Names = new HashSet<string>(name.Split(new[] { "|" }, StringSplitOptions.RemoveEmptyEntries));
            this.Description = description;
            this.Key = key;
        }

        public ICollection<string> Names { get; private set; }

        public Action<ParameterSet, IEnumerable<Tuple<string, string>>> Action { get; private set; }

        public string Description { get; private set; }

        public string Key { get; private set; }
    }
}
