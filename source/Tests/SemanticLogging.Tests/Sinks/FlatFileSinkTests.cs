// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Sinks;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.Sinks
{
    [TestClass]
    public class FlatFileSinkTests
    {
        private string fileName;
        private FlatFileSink sink;

        [TestInitialize]
        public void SetUp()
        {
            AppDomain.CurrentDomain.SetData("APPBASE", Environment.CurrentDirectory);
            this.fileName = Path.ChangeExtension(Guid.NewGuid().ToString("N"), ".log");
            Environment.SetEnvironmentVariable("TESTVAR", "fromtestvariable");
            Environment.SetEnvironmentVariable("INVALIDPATH", @"..\..\");
        }

        [TestCleanup]
        public void TearDown()
        {
            Environment.SetEnvironmentVariable("TESTVAR", null);
            Environment.SetEnvironmentVariable("INVALIDPATH", null);

            if (sink != null)
            {
                this.sink.Dispose();
            }

            if (File.Exists(this.fileName))
            {
                File.Delete(this.fileName);
            }
        }

        [TestMethod]
        [ExpectedException(typeof(DirectoryNotFoundException))]
        public void ThrowOnDirectoryNotFound()
        {
            new FlatFileSink(@"Z:\Foo\foo.log", false);
        }

        [TestMethod]
        public void ThrowOnInvalidFileChars()
        {
            foreach (var c in Path.GetInvalidFileNameChars())
            {
                AssertEx.Throws<ArgumentException>(() => new FlatFileSink(c.ToString(), false));
            }

            foreach (var c in Path.GetInvalidPathChars())
            {
                AssertEx.Throws<ArgumentException>(() => new FlatFileSink(c.ToString(), false));
            }
        }

        [TestMethod]
        public void ThrowOnInvalidOSFileNames()
        {
            AssertEx.Throws<ArgumentException>(() => new FlatFileSink("PRN.log", false));
            AssertEx.Throws<ArgumentException>(() => new FlatFileSink("AUX.log", false));
            AssertEx.Throws<ArgumentException>(() => new FlatFileSink("CON.log", false));
        }

        [TestMethod]
        public void ThrowOnPathNavigationFileName()
        {
            AssertEx.Throws<ArgumentException>(() => new FlatFileSink(".", false));
            AssertEx.Throws<ArgumentException>(() => new FlatFileSink(@"..\", false));
            AssertEx.Throws<ArgumentException>(() => new FlatFileSink(@"..\..\..", false));
            AssertEx.Throws<ArgumentException>(() => new FlatFileSink(@"C:\Test\..\", false));
        }

        [TestMethod]
        public void CreatesFlatFile()
        {
            sink = new FlatFileSink(this.fileName, false);
            sink.OnNext("|1");

            Assert.IsTrue(File.Exists(this.fileName));

            var entries = ReadFileWithoutLock(this.fileName).Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);

            Assert.AreEqual<int>(1, entries.Count());
        }

        [TestMethod]
        public void AppendsEntriesToFlatFile()
        {
            sink = new FlatFileSink(this.fileName, false);
            sink.OnNext("|1");
            sink.OnNext("|2");
            sink.OnNext("|3");

            var entries = ReadFileWithoutLock(this.fileName).Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
            Assert.AreEqual<int>(3, entries.Length);
            Assert.AreEqual("1", entries[0]);
            Assert.AreEqual("2", entries[1]);
            Assert.AreEqual("3", entries[2]);
        }

        [TestMethod]
        public void ConcurrentAppendsEntriesToFlatFile()
        {
            sink = new FlatFileSink(this.fileName, false);
            const int NumberOfEntries = 100;

            Parallel.For(0, NumberOfEntries, i => sink.OnNext("|" + i));

            var entries = ReadFileWithoutLock(this.fileName).Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
            Assert.AreEqual<int>(NumberOfEntries, entries.Length);
            for (int i = 0; i < NumberOfEntries; i++)
            {
                CollectionAssert.Contains(entries, i.ToString());
            }
        }

        [TestMethod]
        [Ignore]    // TODO fix race condition
        public void ConcurrentAppendsEntriesToFlatFileWhenUsingAsync()
        {
            sink = new FlatFileSink(this.fileName, isAsync: true);
            const int NumberOfEntries = 100;

            Parallel.For(0, NumberOfEntries, i => sink.OnNext("|" + i));
            sink.FlushAsync().Wait();
            var entries = ReadFileWithoutLock(this.fileName).Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
            Assert.AreEqual<int>(NumberOfEntries, entries.Length);
            for (int i = 0; i < NumberOfEntries; i++)
            {
                CollectionAssert.Contains(entries, i.ToString());
            }
        }

        [TestMethod]
        public void CreatesDirectoryForLogRecursively()
        {
            string file = @"dir1\dir2\test\patterns\practices\log.xt";
            using (var flatFileSink = new FlatFileSink(file, false))
            {
                Assert.IsTrue(new DirectoryInfo(Path.GetDirectoryName(file)).Exists);
            }

            Directory.Delete(Path.GetDirectoryName(file), true);
        }

        [TestMethod]
        public void ExpandsExistingEnvironmentVariables()
        {
            var tempFileName = this.fileName;
            this.fileName = Path.Combine(Path.Combine(Environment.CurrentDirectory, "fromtestvariable"), tempFileName);

            this.sink = new FlatFileSink(Path.Combine(Environment.CurrentDirectory, @"%TESTVAR%\") + tempFileName, false);
            this.sink.OnNext("|1");
            this.sink.OnNext("|2");
            this.sink.OnNext("|3");

            var entries = ReadFileWithoutLock(this.fileName).Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
            Assert.AreEqual<int>(3, entries.Length);
            Assert.AreEqual("1", entries[0]);
            Assert.AreEqual("2", entries[1]);
            Assert.AreEqual("3", entries[2]);
        }

        [TestMethod]
        public void IgnoresMissingEnvironmentVariables()
        {
            var tempFileName = this.fileName;
            this.fileName = Path.Combine(Environment.CurrentDirectory, tempFileName);

            this.sink = new FlatFileSink(Path.Combine(Environment.CurrentDirectory, @"%MISSINGTESTVAR%\") + tempFileName, false);
            this.sink.OnNext("|1");
            this.sink.OnNext("|2");
            this.sink.OnNext("|3");

            var entries = ReadFileWithoutLock(this.fileName).Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
            Assert.AreEqual<int>(3, entries.Length);
            Assert.AreEqual("1", entries[0]);
            Assert.AreEqual("2", entries[1]);
            Assert.AreEqual("3", entries[2]);
        }

        [TestMethod]
        public void CreatingSinkWithVariableThatResultsInInvalidPathThrows()
        {
            AssertEx.Throws<ArgumentException>(() => new FlatFileSink("%INVALIDPATH%", false));
        }

        private static string ReadFileWithoutLock(string fileName)
        {
            using (var reader = new StreamReader(new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
            {
                return reader.ReadToEnd();
            }
        }
    }

    [TestClass]
    public class ConcurrencyFixture
    {
        private string fileName;

        [TestMethod]
        [Ignore]    // TODO fix race condition
        public void ConcurrentAppendsEntriesToFlatFileWhenUsingAsync()
        {
            AppDomain.CurrentDomain.SetData("APPBASE", Environment.CurrentDirectory);
            const int TimesRepeated = 50;
            const int NumberOfEntries = 100;
            for (int repeat = 0; repeat < TimesRepeated; repeat++)
            {
                this.fileName = Path.ChangeExtension(Guid.NewGuid().ToString("N"), ".log");

                try
                {
                    using (var sink = new FlatFileSink(this.fileName, isAsync: true))
                    {
                        Parallel.For(0, NumberOfEntries, i => sink.OnNext("|" + i));
                        sink.FlushAsync().Wait();
                    }

                    var entriesStr = ReadFileWithoutLock(this.fileName);
                    var entries = entriesStr.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
                    Assert.AreEqual<int>(NumberOfEntries, entries.Length, this.fileName + "|" + entries.Length + "    " + entriesStr);
                }
                finally
                {
                    if (File.Exists(this.fileName))
                    { 
                        File.Delete(this.fileName);
                    }
                }
            }
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
