// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.InProc.Tests.TestObjects;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Schema;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.InProc.Tests.SchemaReader
{
    [TestClass]
    public class EventSourceSchemaReaderFixture
    {
        [TestMethod]
        public void EventWithNoTaskIsValid()
        {
            var reader = new EventSourceSchemaReader();

            var schemas = reader.GetSchema(MockEventSourceNoTask.Logger);

            Assert.AreEqual("Informational", schemas[1].TaskName);
            Assert.AreEqual("Test", schemas[4].TaskName);
        }
    }
}
