// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Sinks;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Threading;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.Sinks
{
    public class TemporaryFolderBasedTest : ArrangeActAssert
    {
        protected string BaseDirectory { get; private set; }

        protected override void Arrange()
        {
            base.Arrange();

            this.BaseDirectory = Guid.NewGuid().ToString("N");
            Directory.CreateDirectory(this.BaseDirectory);
        }

        protected override void Teardown()
        {
            base.Teardown();

            Directory.Delete(this.BaseDirectory, true);
        }
    }

    public class Given_a_directory_with_five_matching_files : TemporaryFolderBasedTest
    {
        protected string baseFileName;

        protected override void Arrange()
        {
            base.Arrange();

            this.baseFileName = "trace.log";

            File.WriteAllText(Path.Combine(this.BaseDirectory, "trace.001.log"), "test1");
            Thread.Sleep(50);
            File.WriteAllText(Path.Combine(this.BaseDirectory, "trace.003.log"), "test1");
            Thread.Sleep(50);
            File.WriteAllText(Path.Combine(this.BaseDirectory, "trace.002.log"), "test1");
            Thread.Sleep(50);
            File.WriteAllText(Path.Combine(this.BaseDirectory, "trace.004.log"), "test1");
            Thread.Sleep(50);
            File.WriteAllText(Path.Combine(this.BaseDirectory, "trace.005.log"), "test1");
        }

        [TestClass]
        public class When_a_purger_with_a_cap_of_six_files_purges
            : Given_a_directory_with_five_matching_files
        {
            protected override void Act()
            {
                new RollingFlatFilePurger(this.BaseDirectory, this.baseFileName, 6).Purge();
            }

            [TestMethod]
            public void Then_No_Files_Are_Deleted()
            {
                Assert.IsTrue(File.Exists(Path.Combine(this.BaseDirectory, "trace.001.log")));
                Assert.IsTrue(File.Exists(Path.Combine(this.BaseDirectory, "trace.003.log")));
                Assert.IsTrue(File.Exists(Path.Combine(this.BaseDirectory, "trace.002.log")));
                Assert.IsTrue(File.Exists(Path.Combine(this.BaseDirectory, "trace.004.log")));
                Assert.IsTrue(File.Exists(Path.Combine(this.BaseDirectory, "trace.005.log")));
            }
        }

        [TestClass]
        public class When_a_purger_with_a_cap_of_five_files_purges
            : Given_a_directory_with_five_matching_files
        {
            protected override void Act()
            {
                new RollingFlatFilePurger(this.BaseDirectory, this.baseFileName, 5).Purge();
            }

            [TestMethod]
            public void Then_No_Files_Are_Deleted()
            {
                Assert.IsTrue(File.Exists(Path.Combine(this.BaseDirectory, "trace.001.log")));
                Assert.IsTrue(File.Exists(Path.Combine(this.BaseDirectory, "trace.003.log")));
                Assert.IsTrue(File.Exists(Path.Combine(this.BaseDirectory, "trace.002.log")));
                Assert.IsTrue(File.Exists(Path.Combine(this.BaseDirectory, "trace.004.log")));
                Assert.IsTrue(File.Exists(Path.Combine(this.BaseDirectory, "trace.005.log")));
            }
        }

        [TestClass]
        public class When_a_purger_with_a_cap_of_four_files_purges
            : Given_a_directory_with_five_matching_files
        {
            protected override void Act()
            {
                new RollingFlatFilePurger(this.BaseDirectory, this.baseFileName, 4).Purge();
            }

            [TestMethod]
            public void Then_The_Oldest_File_Is_Deleted()
            {
                Assert.IsFalse(File.Exists(Path.Combine(this.BaseDirectory, "trace.001.log")));
                Assert.IsTrue(File.Exists(Path.Combine(this.BaseDirectory, "trace.003.log")));
                Assert.IsTrue(File.Exists(Path.Combine(this.BaseDirectory, "trace.002.log")));
                Assert.IsTrue(File.Exists(Path.Combine(this.BaseDirectory, "trace.004.log")));
                Assert.IsTrue(File.Exists(Path.Combine(this.BaseDirectory, "trace.005.log")));
            }
        }

        [TestClass]
        public class When_a_purger_with_a_cap_of_three_files_purges
            : Given_a_directory_with_five_matching_files
        {
            protected override void Act()
            {
                new RollingFlatFilePurger(this.BaseDirectory, this.baseFileName, 3).Purge();
            }

            [TestMethod]
            public void Then_The_Two_Oldest_File_are_deleted()
            {
                Assert.IsFalse(File.Exists(Path.Combine(this.BaseDirectory, "trace.001.log")));
                Assert.IsFalse(File.Exists(Path.Combine(this.BaseDirectory, "trace.003.log")));
                Assert.IsTrue(File.Exists(Path.Combine(this.BaseDirectory, "trace.002.log")));
                Assert.IsTrue(File.Exists(Path.Combine(this.BaseDirectory, "trace.004.log")));
                Assert.IsTrue(File.Exists(Path.Combine(this.BaseDirectory, "trace.005.log")));
            }
        }

        [TestClass]
        public class When_a_purger_with_a_cap_of_three_files_for_a_different_base_FileName_purges
            : Given_a_directory_with_five_matching_files
        {
            protected override void Act()
            {
                new RollingFlatFilePurger(this.BaseDirectory, "some_pattern.log", 3).Purge();
            }

            [TestMethod]
            public void Then_No_Files_Are_Deleted()
            {
                Assert.IsTrue(File.Exists(Path.Combine(this.BaseDirectory, "trace.001.log")));
                Assert.IsTrue(File.Exists(Path.Combine(this.BaseDirectory, "trace.003.log")));
                Assert.IsTrue(File.Exists(Path.Combine(this.BaseDirectory, "trace.002.log")));
                Assert.IsTrue(File.Exists(Path.Combine(this.BaseDirectory, "trace.004.log")));
                Assert.IsTrue(File.Exists(Path.Combine(this.BaseDirectory, "trace.005.log")));
            }
        }

        [TestClass]
        public class When_a_purger_with_a_cap_of_two_files_for_a_base_FileName_with_an_extension_contained_in_the_existing_files
            : Given_a_directory_with_five_matching_files
        {
            protected override void Act()
            {
                new RollingFlatFilePurger(this.BaseDirectory, "trace.lo", 2).Purge();
            }

            [TestMethod]
            public void Then_No_Files_Are_Deleted()
            {
                Assert.IsTrue(File.Exists(Path.Combine(this.BaseDirectory, "trace.001.log")));
                Assert.IsTrue(File.Exists(Path.Combine(this.BaseDirectory, "trace.003.log")));
                Assert.IsTrue(File.Exists(Path.Combine(this.BaseDirectory, "trace.002.log")));
                Assert.IsTrue(File.Exists(Path.Combine(this.BaseDirectory, "trace.004.log")));
                Assert.IsTrue(File.Exists(Path.Combine(this.BaseDirectory, "trace.005.log")));
            }
        }
    }

    public class Given_a_directory_with_five_matching_files_with_long_extensions : TemporaryFolderBasedTest
    {
        protected string directory;
        protected string baseFileName;

        protected override void Arrange()
        {
            base.Arrange();

            this.baseFileName = "trace.logged";

            File.WriteAllText(Path.Combine(this.BaseDirectory, "trace.001.logged"), "test1");
            Thread.Sleep(50);
            File.WriteAllText(Path.Combine(this.BaseDirectory, "trace.003.logged"), "test1");
            Thread.Sleep(50);
            File.WriteAllText(Path.Combine(this.BaseDirectory, "trace.002.logged"), "test1");
            Thread.Sleep(50);
            File.WriteAllText(Path.Combine(this.BaseDirectory, "trace.004.logged"), "test1");
            Thread.Sleep(50);
            File.WriteAllText(Path.Combine(this.BaseDirectory, "trace.005.logged"), "test1");
        }

        [TestClass]
        public class When_a_purger_with_a_cap_of_three_files_purges
            : Given_a_directory_with_five_matching_files_with_long_extensions
        {
            protected override void Act()
            {
                new RollingFlatFilePurger(this.BaseDirectory, this.baseFileName, 3).Purge();
            }

            [TestMethod]
            public void Then_the_two_oldest_files_are_deleted()
            {
                Assert.IsFalse(File.Exists(Path.Combine(this.BaseDirectory, "trace.001.logged")));
                Assert.IsFalse(File.Exists(Path.Combine(this.BaseDirectory, "trace.003.logged")));
                Assert.IsTrue(File.Exists(Path.Combine(this.BaseDirectory, "trace.002.logged")));
                Assert.IsTrue(File.Exists(Path.Combine(this.BaseDirectory, "trace.004.logged")));
                Assert.IsTrue(File.Exists(Path.Combine(this.BaseDirectory, "trace.005.logged")));
            }
        }

        [TestClass]
        public class When_a_purger_with_a_cap_of_three_files_for_a_base_FileName_with_an_three_chars_extension_contained_in_the_existing_files
            : Given_a_directory_with_five_matching_files_with_long_extensions
        {
            protected override void Act()
            {
                new RollingFlatFilePurger(this.BaseDirectory, "trace.log", 3).Purge();
            }

            [TestMethod]
            public void Then_No_Files_Are_Deleted()
            {
                Assert.IsTrue(File.Exists(Path.Combine(this.BaseDirectory, "trace.001.logged")));
                Assert.IsTrue(File.Exists(Path.Combine(this.BaseDirectory, "trace.003.logged")));
                Assert.IsTrue(File.Exists(Path.Combine(this.BaseDirectory, "trace.002.logged")));
                Assert.IsTrue(File.Exists(Path.Combine(this.BaseDirectory, "trace.004.logged")));
                Assert.IsTrue(File.Exists(Path.Combine(this.BaseDirectory, "trace.005.logged")));
            }
        }
    }

    public class Given_a_directory_with_five_matching_files_one_of_them_readonly : TemporaryFolderBasedTest
    {
        protected string baseFileName;

        protected override void Arrange()
        {
            base.Arrange();

            this.baseFileName = "trace.log";

            File.WriteAllText(Path.Combine(this.BaseDirectory, "trace.001.log"), "test1");
            Thread.Sleep(50);
            File.WriteAllText(Path.Combine(this.BaseDirectory, "trace.003.log"), "test1");
            Thread.Sleep(50);
            File.WriteAllText(Path.Combine(this.BaseDirectory, "trace.002.log"), "test1");
            Thread.Sleep(50);
            File.WriteAllText(Path.Combine(this.BaseDirectory, "trace.004.log"), "test1");
            Thread.Sleep(50);
            File.WriteAllText(Path.Combine(this.BaseDirectory, "trace.005.log"), "test1");

            File.SetAttributes(Path.Combine(this.BaseDirectory, "trace.003.log"), FileAttributes.ReadOnly);
        }

        protected override void Teardown()
        {
            File.SetAttributes(Path.Combine(this.BaseDirectory, "trace.003.log"), FileAttributes.Normal);

            base.Teardown();
        }

        [TestClass]
        public class When_a_purger_with_a_cap_of_three_files_purges
            : Given_a_directory_with_five_matching_files_one_of_them_readonly
        {
            protected override void Act()
            {
                new RollingFlatFilePurger(this.BaseDirectory, this.baseFileName, 3).Purge();
            }

            [TestMethod]
            public void Then_only_the_non_readonly_file_is_deleted_among_the_two_oldest_files()
            {
                Assert.IsFalse(File.Exists(Path.Combine(this.BaseDirectory, "trace.001.log")));
                Assert.IsTrue(File.Exists(Path.Combine(this.BaseDirectory, "trace.003.log")));
                Assert.IsTrue(File.Exists(Path.Combine(this.BaseDirectory, "trace.002.log")));
                Assert.IsTrue(File.Exists(Path.Combine(this.BaseDirectory, "trace.004.log")));
                Assert.IsTrue(File.Exists(Path.Combine(this.BaseDirectory, "trace.005.log")));
            }
        }
    }

    public class Given_a_directory_with_five_matching_files_one_of_them_opened : TemporaryFolderBasedTest
    {
        private Stream stream;
        protected string baseFileName;

        protected override void Arrange()
        {
            base.Arrange();

            this.baseFileName = "trace.log";

            File.WriteAllText(Path.Combine(this.BaseDirectory, "trace.001.log"), "test1");
            Thread.Sleep(50);
            File.WriteAllText(Path.Combine(this.BaseDirectory, "trace.003.log"), "test1");
            Thread.Sleep(50);
            File.WriteAllText(Path.Combine(this.BaseDirectory, "trace.002.log"), "test1");
            Thread.Sleep(50);
            File.WriteAllText(Path.Combine(this.BaseDirectory, "trace.004.log"), "test1");
            Thread.Sleep(50);
            File.WriteAllText(Path.Combine(this.BaseDirectory, "trace.005.log"), "test1");

            stream = File.OpenWrite(Path.Combine(this.BaseDirectory, "trace.003.log"));
        }

        protected override void Teardown()
        {
            stream.Close();

            base.Teardown();
        }

        [TestClass]
        public class When_a_purger_with_a_cap_of_three_files_purges
            : Given_a_directory_with_five_matching_files_one_of_them_opened
        {
            protected override void Act()
            {
                new RollingFlatFilePurger(this.BaseDirectory, this.baseFileName, 3).Purge();
            }

            [TestMethod]
            public void Then_only_the_non_opened_file_is_deleted_among_the_two_oldest_files()
            {
                Assert.IsFalse(File.Exists(Path.Combine(this.BaseDirectory, "trace.001.log")));
                Assert.IsTrue(File.Exists(Path.Combine(this.BaseDirectory, "trace.003.log")));
                Assert.IsTrue(File.Exists(Path.Combine(this.BaseDirectory, "trace.002.log")));
                Assert.IsTrue(File.Exists(Path.Combine(this.BaseDirectory, "trace.004.log")));
                Assert.IsTrue(File.Exists(Path.Combine(this.BaseDirectory, "trace.005.log")));
            }
        }
    }

    public class Given_a_purger_configured_for_a_non_existing_directory : ArrangeActAssert
    {
        private RollingFlatFilePurger purger;

        protected override void Arrange()
        {
            base.Arrange();

            this.purger = new RollingFlatFilePurger(Guid.NewGuid().ToString("N"), "trace.log", 4);
        }

        [TestClass]
        public class When_purging : Given_a_purger_configured_for_a_non_existing_directory
        {
            protected override void Act()
            {
                this.purger.Purge();
            }

            [TestMethod]
            public void Then_no_exception_is_thrown()
            {
            }
        }
    }

    public class Given_a_directory_with_files_with_names_containing_base_name_for_purger : TemporaryFolderBasedTest
    {
        protected override void Arrange()
        {
            base.Arrange();

            File.WriteAllText(Path.Combine(this.BaseDirectory, "trace001.log"), "test1");
            File.WriteAllText(Path.Combine(this.BaseDirectory, "trace002.log"), "test1");
        }

        [TestClass]
        public class When_a_purger_purges_for_base_name
            : Given_a_directory_with_files_with_names_containing_base_name_for_purger
        {
            protected override void Act()
            {
                new RollingFlatFilePurger(this.BaseDirectory, "trace.log", 1).Purge();
            }

            [TestMethod]
            public void Then_files_with_names_containing_base_name_are_not_deleted()
            {
                Assert.IsTrue(File.Exists(Path.Combine(this.BaseDirectory, "trace001.log")));
                Assert.IsTrue(File.Exists(Path.Combine(this.BaseDirectory, "trace002.log")));
            }
        }
    }

    public class Given_a_directory_with_five_matching_files_with_the_same_creation_date : TemporaryFolderBasedTest
    {
        protected string baseFileName;

        protected override void Arrange()
        {
            base.Arrange();

            this.baseFileName = "trace.log";
            var creationTime = DateTime.Now;

            File.WriteAllText(Path.Combine(this.BaseDirectory, "trace.001.1.log"), "test1");
            File.SetCreationTime(Path.Combine(this.BaseDirectory, "trace.001.1.log"), creationTime);
            File.WriteAllText(Path.Combine(this.BaseDirectory, "trace.001.14.log"), "test1");
            File.SetCreationTime(Path.Combine(this.BaseDirectory, "trace.001.14.log"), creationTime);
            File.WriteAllText(Path.Combine(this.BaseDirectory, "trace.001.5.log"), "test1");
            File.SetCreationTime(Path.Combine(this.BaseDirectory, "trace.001.5.log"), creationTime);
            File.WriteAllText(Path.Combine(this.BaseDirectory, "trace.001.3.log"), "test1");
            File.SetCreationTime(Path.Combine(this.BaseDirectory, "trace.001.3.log"), creationTime);
            File.WriteAllText(Path.Combine(this.BaseDirectory, "trace.001.21.log"), "test1");
            File.SetCreationTime(Path.Combine(this.BaseDirectory, "trace.001.21.log"), creationTime);
        }

        [TestClass]
        public class When_a_purger_with_a_cap_of_three_files_purges
            : Given_a_directory_with_five_matching_files_with_the_same_creation_date
        {
            protected override void Act()
            {
                new RollingFlatFilePurger(this.BaseDirectory, this.baseFileName, 3).Purge();
            }

            [TestMethod]
            public void Then_the_three_files_with_the_largest_sequence_number_are_kept()
            {
                Assert.IsFalse(File.Exists(Path.Combine(this.BaseDirectory, "trace.001.1.log")));
                Assert.IsFalse(File.Exists(Path.Combine(this.BaseDirectory, "trace.001.3.log")));
                Assert.IsTrue(File.Exists(Path.Combine(this.BaseDirectory, "trace.001.5.log")));
                Assert.IsTrue(File.Exists(Path.Combine(this.BaseDirectory, "trace.001.14.log")));
                Assert.IsTrue(File.Exists(Path.Combine(this.BaseDirectory, "trace.001.21.log")));
            }
        }
    }

    public class Given_a_directory_with_five_matching_files_with_non_integer_sequences_and_the_same_creation_date : TemporaryFolderBasedTest
    {
        protected string baseFileName;

        protected override void Arrange()
        {
            base.Arrange();

            this.baseFileName = "trace.log";
            var creationTime = DateTime.Now;

            File.WriteAllText(Path.Combine(this.BaseDirectory, "trace.001.aaa.log"), "test1");
            File.SetCreationTime(Path.Combine(this.BaseDirectory, "trace.001.aaa.log"), creationTime);
            File.WriteAllText(Path.Combine(this.BaseDirectory, "trace.001.cc.log"), "test1");
            File.SetCreationTime(Path.Combine(this.BaseDirectory, "trace.001.cc.log"), creationTime);
            File.WriteAllText(Path.Combine(this.BaseDirectory, "trace.001.5.log"), "test1");
            File.SetCreationTime(Path.Combine(this.BaseDirectory, "trace.001.5.log"), creationTime);
            File.WriteAllText(Path.Combine(this.BaseDirectory, "trace.001.bbb.log"), "test1");
            File.SetCreationTime(Path.Combine(this.BaseDirectory, "trace.001.bbb.log"), creationTime);
            File.WriteAllText(Path.Combine(this.BaseDirectory, "trace.001.51.log"), "test1");
            File.SetCreationTime(Path.Combine(this.BaseDirectory, "trace.001.51.log"), creationTime);
        }

        [TestClass]
        public class When_a_purger_with_a_cap_of_four_files_purges
            : Given_a_directory_with_five_matching_files_with_non_integer_sequences_and_the_same_creation_date
        {
            protected override void Act()
            {
                new RollingFlatFilePurger(this.BaseDirectory, this.baseFileName, 4).Purge();
            }

            [TestMethod]
            public void Then_the_three_files_with_the_largest_sequences_compared_as_strings_are_kept()
            {
                Assert.IsTrue(File.Exists(Path.Combine(this.BaseDirectory, "trace.001.aaa.log")));
                Assert.IsTrue(File.Exists(Path.Combine(this.BaseDirectory, "trace.001.cc.log")));
                Assert.IsFalse(File.Exists(Path.Combine(this.BaseDirectory, "trace.001.5.log")));
                Assert.IsTrue(File.Exists(Path.Combine(this.BaseDirectory, "trace.001.bbb.log")));
                Assert.IsTrue(File.Exists(Path.Combine(this.BaseDirectory, "trace.001.51.log")));
            }
        }

        [TestClass]
        public class When_a_purger_with_a_cap_of_three_files_purges
            : Given_a_directory_with_five_matching_files_with_non_integer_sequences_and_the_same_creation_date
        {
            protected override void Act()
            {
                new RollingFlatFilePurger(this.BaseDirectory, this.baseFileName, 3).Purge();
            }

            [TestMethod]
            public void Then_the_three_files_with_the_largest_sequences_compared_as_strings_are_kept()
            {
                Assert.IsTrue(File.Exists(Path.Combine(this.BaseDirectory, "trace.001.aaa.log")));
                Assert.IsTrue(File.Exists(Path.Combine(this.BaseDirectory, "trace.001.cc.log")));
                Assert.IsFalse(File.Exists(Path.Combine(this.BaseDirectory, "trace.001.5.log")));
                Assert.IsTrue(File.Exists(Path.Combine(this.BaseDirectory, "trace.001.bbb.log")));
                Assert.IsFalse(File.Exists(Path.Combine(this.BaseDirectory, "trace.001.51.log")));
            }
        }

        [TestClass]
        public class When_a_purger_with_a_cap_of_two_files_purges
            : Given_a_directory_with_five_matching_files_with_non_integer_sequences_and_the_same_creation_date
        {
            protected override void Act()
            {
                new RollingFlatFilePurger(this.BaseDirectory, this.baseFileName, 2).Purge();
            }

            [TestMethod]
            public void Then_the_three_files_with_the_largest_sequences_compared_as_strings_are_kept()
            {
                Assert.IsFalse(File.Exists(Path.Combine(this.BaseDirectory, "trace.001.aaa.log")));
                Assert.IsTrue(File.Exists(Path.Combine(this.BaseDirectory, "trace.001.cc.log")));
                Assert.IsFalse(File.Exists(Path.Combine(this.BaseDirectory, "trace.001.5.log")));
                Assert.IsTrue(File.Exists(Path.Combine(this.BaseDirectory, "trace.001.bbb.log")));
                Assert.IsFalse(File.Exists(Path.Combine(this.BaseDirectory, "trace.001.51.log")));
            }
        }

        [TestClass]
        public class When_a_purger_with_a_cap_of_one_files_purges
            : Given_a_directory_with_five_matching_files_with_non_integer_sequences_and_the_same_creation_date
        {
            protected override void Act()
            {
                new RollingFlatFilePurger(this.BaseDirectory, this.baseFileName, 1).Purge();
            }

            [TestMethod]
            public void Then_the_three_files_with_the_largest_sequences_compared_as_strings_are_kept()
            {
                Assert.IsFalse(File.Exists(Path.Combine(this.BaseDirectory, "trace.001.aaa.log")));
                Assert.IsTrue(File.Exists(Path.Combine(this.BaseDirectory, "trace.001.cc.log")));
                Assert.IsFalse(File.Exists(Path.Combine(this.BaseDirectory, "trace.001.5.log")));
                Assert.IsFalse(File.Exists(Path.Combine(this.BaseDirectory, "trace.001.bbb.log")));
                Assert.IsFalse(File.Exists(Path.Combine(this.BaseDirectory, "trace.001.51.log")));
            }
        }
    }

    [TestClass]
    public partial class Given
    {
        [TestMethod]
        public void Then_creating_a_purger_with_a_null_directory_throws()
        {
            AssertEx.Throws<ArgumentNullException>(() => new RollingFlatFilePurger(null, "trace.log", 10));
        }

        [TestMethod]
        public void Then_creating_a_purger_with_a_null_filename_throws()
        {
            AssertEx.Throws<ArgumentNullException>(() => new RollingFlatFilePurger(Environment.CurrentDirectory, null, 10));
        }

        [TestMethod]
        public void Then_creating_a_purger_with_a_negative_cap_throws()
        {
            AssertEx.Throws<ArgumentOutOfRangeException>(() => new RollingFlatFilePurger(Environment.CurrentDirectory, "trace.log", -10));
        }

        [TestMethod]
        public void Then_creating_a_purger_with_a_zero_cap_throws()
        {
            AssertEx.Throws<ArgumentOutOfRangeException>(() => new RollingFlatFilePurger(Environment.CurrentDirectory, "trace.log", 0));
        }

        [TestMethod]
        public void Then_creating_a_purger_with_a_cap_of_one_does_not_throw()
        {
            new RollingFlatFilePurger(Environment.CurrentDirectory, "trace.log", 1);
        }
    }

    [TestClass]
    public class TestPurgerSequenceExtraction
    {
        [TestMethod]
        public void CanExtractSequenceFromNormalArchiveFileName()
        {
            var sequence = RollingFlatFilePurger.GetSequence("trace.zzzz.1.log");

            Assert.AreEqual("1", sequence);
        }

        [TestMethod]
        public void CanExtractMultiCharSequenceFromNormalArchiveFileName()
        {
            var sequence = RollingFlatFilePurger.GetSequence("trace.zzzz.188.log");

            Assert.AreEqual("188", sequence);
        }

        [TestMethod]
        public void ExtractingSequenceFromNameWithNoDotsReturnsEmpty()
        {
            var sequence = RollingFlatFilePurger.GetSequence("trace");

            Assert.AreEqual(string.Empty, sequence);
        }

        [TestMethod]
        public void ExtractingSequenceFromNameWithSingleDotReturnsEmpty()
        {
            var sequence = RollingFlatFilePurger.GetSequence("trace.log");

            Assert.AreEqual(string.Empty, sequence);
        }

        [TestMethod]
        public void ExtractingSequenceFromNameWithTrailingDotReturnsEmpty()
        {
            var sequence = RollingFlatFilePurger.GetSequence("trace.");

            Assert.AreEqual(string.Empty, sequence);
        }

        [TestMethod]
        public void ExtractingSequenceFromNameWithConsecutiveDotsReturnsEmpty()
        {
            var sequence = RollingFlatFilePurger.GetSequence("trace..log");

            Assert.AreEqual(string.Empty, sequence);
        }

        [TestMethod]
        public void CanExtractSequenceFromArchiveFileNameStartingWithDot()
        {
            var sequence = RollingFlatFilePurger.GetSequence(".10.log");

            Assert.AreEqual("10", sequence);
        }
    }
}
