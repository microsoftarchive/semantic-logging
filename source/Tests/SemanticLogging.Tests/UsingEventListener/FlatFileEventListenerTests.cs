// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Formatters;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Schema;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Sinks;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.TestObjects;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using System;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.EventListeners
{
    [TestClass]
    public class FlatFileEventListenerTests
    {
        private static readonly TestEventSource Logger = TestEventSource.Log;
        private string fileName;
        private EventTextFormatter eventTextFormatter;
        private ObservableEventListener listener;

        [TestInitialize]
        public void SetUp()
        {
            AppDomain.CurrentDomain.SetData("APPBASE", Environment.CurrentDirectory);
            this.fileName = Path.ChangeExtension(Guid.NewGuid().ToString("N"), ".log");
            this.eventTextFormatter = new EventTextFormatter(EventTextFormatter.DashSeparator);
            this.listener = new ObservableEventListener();
        }

        [TestCleanup]
        public void TearDown()
        {
            this.listener.Dispose();
            if (File.Exists(this.fileName)) { File.Delete(this.fileName); }
        }

        //[TestMethod]
        //[ExpectedException(typeof(ArgumentNullException))]
        //public void ThrowOnNullFileName()
        //{
        //    listener.LogToFlatFile(null);
        //}

        //[TestMethod]
        //[ExpectedException(typeof(ArgumentException))]
        //public void ThrowOnEmptyFileName()
        //{
        //    listener.LogToFlatFile(string.Empty);
        //}

        [TestMethod]
        [ExpectedException(typeof(DirectoryNotFoundException))]
        public void ThrowOnDirectoryNotFound()
        {
            listener.LogToFlatFile(@"Z:\Foo\foo.log");
        }

        [TestMethod]
        public void ThrowOnInvalidFileChars()
        {
            foreach (var c in Path.GetInvalidFileNameChars())
            {
                AssertEx.Throws<Exception>(() => listener.LogToFlatFile(c.ToString()));
            }

            foreach (var c in Path.GetInvalidPathChars())
            {
                AssertEx.Throws<Exception>(() => listener.LogToFlatFile(c.ToString()));
            }
        }

        [TestMethod]
        public void ThrowOnInvalidOSFileNames()
        {
            AssertEx.Throws<ArgumentException>(() => listener.LogToFlatFile("PRN.log"));
            AssertEx.Throws<ArgumentException>(() => listener.LogToFlatFile("AUX.log"));
            AssertEx.Throws<ArgumentException>(() => listener.LogToFlatFile("CON.log"));
        }

        [TestMethod]
        public void ThrowOnPathNavigationFileName()
        {
            AssertEx.Throws<ArgumentException>(() => listener.LogToFlatFile("."));
            AssertEx.Throws<ArgumentException>(() => listener.LogToFlatFile(@"..\"));
            AssertEx.Throws<ArgumentException>(() => listener.LogToFlatFile(@"..\..\.."));
            AssertEx.Throws<ArgumentException>(() => listener.LogToFlatFile(@"C:\Test\..\"));
        }

        [TestMethod]
        public void CreatesFlatFile()
        {
            listener.LogToFlatFile(this.fileName, this.eventTextFormatter);
            listener.EnableEvents(Logger, EventLevel.LogAlways);
            Logger.Informational("Test payload");

            Assert.IsTrue(File.Exists(this.fileName));

            var entries = Regex.Split(ReadFileWithoutLock(this.fileName), this.eventTextFormatter.Header).Where(c => !string.IsNullOrWhiteSpace(c));

            Assert.AreEqual<int>(1, entries.Count());
        }

        [TestMethod]
        public void AppendsEntriesToFlatFile()
        {
            listener.LogToFlatFile(this.fileName, this.eventTextFormatter);
            listener.EnableEvents(Logger, EventLevel.LogAlways);

            Logger.Informational("First message");
            Logger.Informational("Second message");
            Logger.Informational("Third message");

            var entries = Regex.Split(ReadFileWithoutLock(this.fileName), this.eventTextFormatter.Header).Where(c => !string.IsNullOrWhiteSpace(c));
            Assert.AreEqual<int>(3, entries.Count());
        }

        [TestMethod]
        public void ConcurrentAppendsEntriesToFlatFile()
        {
            const int MaxLoggedEntries = 10;

            listener.LogToFlatFile(this.fileName, this.eventTextFormatter);
            listener.EnableEvents(Logger, EventLevel.LogAlways);

            Parallel.For(0, MaxLoggedEntries, i => Logger.Informational("Info " + i));

            var entries = Regex.Split(ReadFileWithoutLock(this.fileName), this.eventTextFormatter.Header).Where(c => !string.IsNullOrWhiteSpace(c));
            Assert.AreEqual<int>(MaxLoggedEntries, entries.Count());
        }

        [TestMethod]
        public void AppendsEntriesFromDifferentSourcesToFlatFile()
        {
            var formatter = new MockFormatter();
            listener.LogToFlatFile(this.fileName, formatter);
            listener.EnableEvents(Logger, EventLevel.LogAlways, EventKeywords.None);
            listener.EnableEvents(MyCompanyEventSource.Log, EventLevel.LogAlways, Keywords.All);

            Logger.Informational("From TestEventSource");
            Logger.EventWithoutPayloadNorMessage();
            MyCompanyEventSource.Log.PageStart(5, "http://test");

            Assert.AreEqual<int>(3, formatter.WriteEventCalls.Count);
        }

        [TestMethod]
        public void AppendsEntriesFromDifferentSourcesUsesCorrectSchema()
        {
            var formatter = new MockFormatter();
            listener.LogToFlatFile(this.fileName, formatter);
            listener.EnableEvents(Logger, EventLevel.LogAlways, EventKeywords.None);
            listener.EnableEvents(MyCompanyEventSource.Log, EventLevel.LogAlways, Keywords.All);

            Logger.Informational("From TestEventSource");
            Logger.EventWithoutPayloadNorMessage();
            MyCompanyEventSource.Log.PageStart(5, "http://test");

            Assert.AreEqual(EventSourceSchemaCache.Instance.GetSchema(TestEventSource.InformationalEventId, Logger), formatter.WriteEventCalls[0].Schema);
            Assert.AreEqual(EventSourceSchemaCache.Instance.GetSchema(TestEventSource.EventWithoutPayloadNorMessageId, Logger), formatter.WriteEventCalls[1].Schema);
            Assert.AreEqual(EventSourceSchemaCache.Instance.GetSchema(3, MyCompanyEventSource.Log), formatter.WriteEventCalls[2].Schema);
        }

        [TestMethod]
        public void NonDefaultMessageIsLogged()
        {
            listener.LogToFlatFile(this.fileName, new EventTextFormatter(verbosityThreshold: EventLevel.LogAlways));
            listener.EnableEvents(Logger, EventLevel.Verbose);
            Logger.NonDefaultOpcodeNonDefaultVersionEvent(1, 3, 5);

            var fileContent = ReadFileWithoutLock(this.fileName);
            StringAssert.Contains(fileContent, "Message : arg1- 1,arg2- 3,arg3- 5");
            StringAssert.Contains(fileContent, "Task : 2");
        }

        [TestMethod]
        public void AppendsEntriesToFlatFileWithJsonFormatter()
        {
            listener.LogToFlatFile(this.fileName, new JsonEventTextFormatter());
            listener.EnableEvents(Logger, EventLevel.LogAlways);

            Logger.Informational("First message");
            Logger.Informational("Second message");
            Logger.Informational("Third message");

            var entries = JsonConvert.DeserializeObject<TestEventEntry[]>("[" + ReadFileWithoutLock(this.fileName) + "]");
            Assert.AreEqual<int>(3, entries.Count());
        }

        [TestMethod]
        public void AppendsEntriesToFlatFileWithXmlFormatter()
        {
            listener.LogToFlatFile(this.fileName, new XmlEventTextFormatter());
            listener.EnableEvents(Logger, EventLevel.LogAlways);

            Logger.Informational("First message");
            Logger.Informational("Second message");
            Logger.Informational("Third message");

            var entries = XDocument.Parse("<Events>" + ReadFileWithoutLock(this.fileName) + "</Events>").Root.Elements();
            Assert.AreEqual<int>(3, entries.Count());
        }

        [TestMethod]
        public void AppendsEntriesToFlatFileWithNoInvalidEntries()
        {
            var formatter = new MockFormatter() { AfterWriteEventAction = (f) => { if (f.WriteEventCalls.Count == 1) { throw new InvalidOperationException(); } } };
            listener.LogToFlatFile(this.fileName, formatter);
            listener.EnableEvents(Logger, EventLevel.LogAlways);

            Logger.Informational("Will throw error");
            Logger.Informational("Valid message");

            Assert.AreEqual("Valid message", ReadFileWithoutLock(this.fileName));
        }

        private static string ReadFileWithoutLock(string fileName)
        {
            using (var reader = new StreamReader(new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
            {
                return reader.ReadToEnd();
            }
        }
    }
}
