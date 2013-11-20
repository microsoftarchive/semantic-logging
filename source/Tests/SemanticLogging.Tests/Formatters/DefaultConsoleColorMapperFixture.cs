// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Formatters;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.Formatters
{
    [TestClass]
    public class DefaultConsoleColorMapperFixture
    {
        [TestClass]
        public class given_default_colors : ContextBase
        {
            private IConsoleColorMapper sut;
            private Dictionary<EventLevel, ConsoleColor?> results;

            protected override void Given()
            {
                sut = new DefaultConsoleColorMapper();
                results = new Dictionary<EventLevel, ConsoleColor?>();
            }

            protected override void When()
            {
                foreach (EventLevel level in Enum.GetValues(typeof(EventLevel)))
                {
                    results.Add(level, sut.Map(level));
                }
            }

            [TestMethod]
            public void then_all_eventlevels_should_be_mapped()
            {
                Assert.AreEqual(DefaultConsoleColorMapper.LogAlways, results[EventLevel.LogAlways]);
                Assert.AreEqual(DefaultConsoleColorMapper.Critical, results[EventLevel.Critical]);
                Assert.AreEqual(DefaultConsoleColorMapper.Error, results[EventLevel.Error]);
                Assert.AreEqual(DefaultConsoleColorMapper.Warning, results[EventLevel.Warning]);
                Assert.AreEqual(DefaultConsoleColorMapper.Verbose, results[EventLevel.Verbose]);
                Assert.AreEqual(DefaultConsoleColorMapper.Informational, results[EventLevel.Informational]);
            }
        }
    }
}
