// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw.Service
{
    internal class ParameterSet : HashSet<Parameter>
    {
        private readonly Regex nameValueParser = new Regex(@"^(?<f>--|-|/)(?<name>[^:=]+)(?:[:=](?<value>.*))?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public void Add(string name, string description, Action<ParameterSet, IEnumerable<Tuple<string, string>>> action)
        {
            base.Add(new Parameter(name, description, action));
        }

        public void Add(string name, string description, string key)
        {
            base.Add(new Parameter(name, description, key));
        }

        public bool Parse(IEnumerable<string> args)
        {
            // parse all parameters
            var allArgs = new List<Tuple<string, string>>();
            foreach (var arg in args)
            {
                var match = this.nameValueParser.Match(arg);
                if (!match.Success)
                {
                    return false;
                }

                allArgs.Add(Tuple.Create(match.Groups["name"].Value, match.Groups["value"].Value));
            }

            // left join actual arguments with all the available names
            var matchingParams = (from arg in allArgs
                                  join par in this.SelectMany(p => p.Names.Select(n => new { Name = n, Parameter = p }))
                                      on arg.Item1 equals par.Name into gj
                                  from subpar in gj.DefaultIfEmpty()
                                  select new { Argument = arg, Parameter = subpar != null ? subpar.Parameter : default(Parameter) }).ToList();

            if (matchingParams.Any(p => p.Parameter == null))
            {
                return false;
            }

            var actionParams = matchingParams.Where(p => p.Parameter.Action != null).ToList();
            if (actionParams.Count != 1)
            {
                return false;
            }

            // provide extra arguments using a stable key
            var normalizedArguments =
                matchingParams.Where(mp => mp.Parameter.Key != null).Select(mp => Tuple.Create(mp.Parameter.Key, mp.Argument.Item2));

            actionParams[0].Parameter.Action(this, normalizedArguments);

            return true;
        }
    }
}
