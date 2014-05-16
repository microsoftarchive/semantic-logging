// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System.Diagnostics.Tracing;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw.Configuration;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.TestObjects;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.Etw
{
    [TestClass]
    public class given_eventSourceSettings
    {
        [TestMethod]
        [ExpectedException(typeof(ConfigurationException))]
        public void when_creating_instance_with_no_values()
        {
            new EventSourceSettings();
        }

        [TestMethod]
        public void when_creating_instance_with_name_only()
        {
            var sut = new EventSourceSettings(MyCompanyEventSource.Log.Name);

            Assert.AreEqual(MyCompanyEventSource.Log.Name, sut.Name);
            Assert.AreEqual(MyCompanyEventSource.Log.Guid, sut.EventSourceId);
            Assert.AreEqual(EventLevel.LogAlways, sut.Level);
            Assert.AreEqual(Keywords.All, sut.MatchAnyKeyword);
        }

        [TestMethod]
        public void when_creating_instance_with_id_only()
        {
            var sut = new EventSourceSettings(eventSourceId: MyCompanyEventSource.Log.Guid);

            Assert.AreEqual(MyCompanyEventSource.Log.Guid.ToString(), sut.Name);
            Assert.AreEqual(MyCompanyEventSource.Log.Guid, sut.EventSourceId);
            Assert.AreEqual(EventLevel.LogAlways, sut.Level);
            Assert.AreEqual(Keywords.All, sut.MatchAnyKeyword);
        }

        [TestMethod]
        [ExpectedException(typeof(ConfigurationException))]
        public void when_creating_instance_with_both_name_and_id()
        {
            new EventSourceSettings(MyCompanyEventSource.Log.Name, MyCompanyEventSource.Log.Guid);
        }
    }
}
