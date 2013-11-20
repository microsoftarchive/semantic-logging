// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Xml.Schema;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.Etw
{
    [TestClass]
    public class given_configurationException_initializer
    {
        [TestMethod]
        public void when_creating_instance_with_null_message()
        {
            var sut = new ConfigurationException((string)null);

            Assert.AreEqual("Exception of type '" + typeof(ConfigurationException).FullName + "' was thrown.", sut.Message);
            Assert.AreEqual(0, sut.InnerExceptions.Count);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void when_creating_instance_with_null_innerExceptions()
        {
            new ConfigurationException((IEnumerable<Exception>)null);
        }

        [TestMethod]
        public void when_creating_instance_with_empty_innerExceptions()
        {
            var sut = new ConfigurationException(Enumerable.Empty<Exception>());

            Assert.IsNotNull(sut.Message);
            Assert.IsNull(sut.InnerException);
            Assert.AreEqual(0, sut.InnerExceptions.Count);
        }

        [TestMethod]
        public void when_creating_instance_with_innerExceptions()
        {
            var inner = new ArgumentException("args");
            var sut = new ConfigurationException(new Exception[] { inner });

            Assert.IsNotNull(sut.Message);
            Assert.AreEqual(inner, sut.InnerException);
            Assert.AreEqual(1, sut.InnerExceptions.Count);
            Assert.AreEqual(inner, sut.InnerExceptions.First());
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void when_creating_instance_with_a_null_exception_within_innerExceptions()
        {
            new ConfigurationException(new Exception[] { new Exception(), null, new Exception() });
        }

        [TestMethod]
        public void when_calling_toString()
        {
            List<Exception> inners = new List<Exception>();
            inners.Add(new ArgumentException("Msg1"));
            inners.Add(new InvalidOperationException("Inv2"));
            inners.Add(new XmlSchemaValidationException("Validation", new Exception("inner"), 111, 222));

            var sut = new ConfigurationException(inners);
            var toStr = sut.ToString();

            StringAssert.Contains(toStr, "Msg1");
            StringAssert.Contains(toStr, "Inv2");
            StringAssert.Contains(toStr, "Validation");
            StringAssert.Contains(toStr, "inner");
            StringAssert.Contains(toStr, "111");
            StringAssert.Contains(toStr, "222");
        }

        [TestMethod]
        public void when_calling_toString_with_configurationFile()
        {
            var sut = new ConfigurationException("test") { ConfigurationFile = "myConfigfile" };

            var toStr = sut.ToString();
            StringAssert.Contains(toStr, "test");
            StringAssert.Contains(toStr, "myConfigfile");
        }

        [TestMethod]
        public void when_serializing_with_inner_shoulld_deserialize_back()
        {
            List<Exception> inners = new List<Exception>();
            inners.Add(new ArgumentException("Msg1"));
            inners.Add(new InvalidOperationException("Inv2"));
            inners.Add(new XmlSchemaValidationException("Validation", new Exception("inner"), 111, 222));

            var sut = new ConfigurationException(inners);
            var str = sut.ToString();

            BinaryFormatter formatter = new BinaryFormatter();
            using (MemoryStream stream = new MemoryStream())
            {
                formatter.Serialize(stream, sut);
                stream.Position = 0;
                var deserialized = formatter.Deserialize(stream) as ConfigurationException;

                Assert.AreEqual(str, deserialized.ToString());
            }
        }
    }
}
