// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Formatters;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.InProc.Tests.TestObjects;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.InProc.Tests.TestScenarios;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Schema;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.Shared.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.InProc.Tests.Sinks
{
    [TestClass]
    public class FlatFileSinkFixture
    {
        [TestMethod]
        public void WhenFileDoesNotExist()
        {
            var fileName = "newflatfile.log";
            File.Delete(fileName);
            var formatter = new EventTextFormatter(EventTextFormatter.DashSeparator);
            var logger = MockEventSource.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToFlatFile(fileName, formatter);
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    logger.Informational("Message 1");
                });

            Assert.IsTrue(File.Exists(fileName));
            var entries = Regex.Split(ReadFileWithoutLock(fileName), formatter.Header).Where(c => !string.IsNullOrWhiteSpace(c));
            Assert.AreEqual<int>(1, entries.Count());
        }

        [TestMethod]
        public void WhenFileIsDeletedBetweenSessions()
        {
            var fileName = "newflatfile.log";
            File.Delete(fileName);
            var formatter = new EventTextFormatter(EventTextFormatter.DashSeparator);
            var logger = MockEventSource.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToFlatFile(fileName, formatter);
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    logger.Informational("Message 1");
                });

            Assert.IsTrue(File.Exists(fileName));
            var entries = Regex.Split(ReadFileWithoutLock(fileName), formatter.Header).Where(c => !string.IsNullOrWhiteSpace(c));
            Assert.AreEqual<int>(1, entries.Count());

            File.Delete(fileName);
            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToFlatFile(fileName, formatter);
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    logger.Informational("Message 1");
                });

            Assert.IsTrue(File.Exists(fileName));
        }

        [TestMethod]
        public void WhenMultipleEventsAreRaised()
        {
            var fileName = "multipleMessages.log";
            File.Delete(fileName);
            var formatter = new EventTextFormatter("------======------");
            var logger = MockEventSource.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToFlatFile(fileName, formatter);
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    for (int n = 0; n < 300; n++)
                    {
                        logger.Informational("some message to flat file " + n.ToString());
                    }
                });

            Assert.IsTrue(File.Exists(fileName));
            var entries = Regex.Split(ReadFileWithoutLock(fileName), formatter.Header).Where(c => !string.IsNullOrWhiteSpace(c));
            Assert.AreEqual<int>(300, entries.Count());
            StringAssert.Contains(entries.First(), "some message to flat file 0");
            StringAssert.Contains(entries.Last(), "some message to flat file 299");
        }

        [TestMethod]
        public void WhenFileExistsEventIsAppended()
        {
            var fileName = "sampleflatfile.log";
            File.Delete(fileName);
            var formatter = new EventTextFormatter(EventTextFormatter.DashSeparator);
            var logger = MockEventSource.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToFlatFile(fileName, formatter);
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    logger.Informational("Message 1");
                });

            Assert.IsTrue(File.Exists(fileName));
            var entries = Regex.Split(ReadFileWithoutLock(fileName), formatter.Header).Where(c => !string.IsNullOrWhiteSpace(c));
            Assert.AreEqual<int>(1, entries.Count());

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToFlatFile(fileName, formatter);
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    logger.Informational("Message 2");
                });

            Assert.IsTrue(File.Exists(fileName));
            var entries2 = Regex.Split(ReadFileWithoutLock(fileName), formatter.Header).Where(c => !string.IsNullOrWhiteSpace(c));
            Assert.AreEqual<int>(2, entries2.Count());
        }

        [TestMethod]
        public void WhenAbsolutePathFileNameIsUsed()
        {
            var absolutePath = Directory.GetCurrentDirectory();
            string fileName = Path.Combine(absolutePath, "newflatfileAbsPath.log");
            File.Delete(fileName);
            var formatter = new EventTextFormatter(EventTextFormatter.DashSeparator);
            var logger = MockEventSource.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToFlatFile(fileName, formatter);
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    logger.Informational("Message 1");
                });

            Assert.IsTrue(File.Exists(fileName));
            var entries = Regex.Split(ReadFileWithoutLock(fileName), formatter.Header).Where(c => !string.IsNullOrWhiteSpace(c));
            Assert.AreEqual<int>(1, entries.Count());
        }

        [TestMethod]
        public void WhenRelativePathFileNameIsUsed()
        {
            var fileName = "../../newflatfileRelativePath.log";
            File.Delete(fileName);
            var formatter = new EventTextFormatter(EventTextFormatter.DashSeparator);
            var logger = MockEventSource.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToFlatFile(fileName, formatter);
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    logger.Informational("Message 1");
                });

            Assert.IsTrue(File.Exists(fileName));
            var entries = Regex.Split(ReadFileWithoutLock(fileName), formatter.Header).Where(c => !string.IsNullOrWhiteSpace(c));
            Assert.AreEqual<int>(1, entries.Count());
            File.Delete(fileName);
        }

        [TestMethod]
        public void WhenDriveDoesNotExist()
        {
            string filepath = string.Empty;
            string[] strDrives = Directory.GetLogicalDrives();
            string strAlphabets = "A B C D E F G H I J K L M N O P Q R S T U V W X Y Z";
            string[] alphabet = strAlphabets.Split(' ');
            foreach (string noDriveExists in alphabet)
            {
                bool notExists = strDrives.Contains(noDriveExists + ":\\");
                if (!notExists)
                {
                    // Drive does not exist 
                    filepath = noDriveExists + ":\\";
                    break;
                }
            }

            var fileName = filepath + "newflatfileDriveNotExists.log";
            var formatter = new EventTextFormatter(EventTextFormatter.DashSeparator);
            try
            {
                using (var listener = new ObservableEventListener())
                {
                    listener.LogToFlatFile(fileName, formatter);
                }
            }
            catch (DirectoryNotFoundException ex)
            {
                StringAssert.StartsWith(ex.Message, "Could not find a part of the path '" + filepath);
            }
        }

        [TestMethod]
        public void WhenFileIsReadOnlyErrorOccursDuringEarly()
        {
            string fileName = "newflatfileReadOnly1.log";
            DeleteReadOnlyFile(fileName);
            File.Create(fileName);
            File.SetAttributes(fileName, FileAttributes.ReadOnly);
            string path = Directory.GetCurrentDirectory();
            var formatter = new EventTextFormatter(EventTextFormatter.DashSeparator);

            try
            {
                using (var listener = new ObservableEventListener())
                {
                    listener.LogToFlatFile(fileName, formatter);
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                Assert.AreEqual("Access to the path '" + path + "\\" + fileName + "' is denied.", ex.Message);
            }
        }

        [TestMethod]
        public void WhenFileIsReadOnlyErrorOccursEarly2()
        {
            var fileName = "newflatfileReadOnly2.log";
            DeleteReadOnlyFile(fileName);
            string path = Directory.GetCurrentDirectory();
            var formatter = new EventTextFormatter(EventTextFormatter.DashSeparator);
            var logger = MockEventSource.Logger;

            try
            {
                TestScenario.With1Listener(
                    logger,
                    listener =>
                    {
                        listener.LogToFlatFile(fileName, formatter);
                        listener.EnableEvents(logger, EventLevel.LogAlways);
                        logger.Informational("Message 1");
                        listener.DisableEvents(logger);

                        Assert.IsTrue(File.Exists(fileName));
                        var entries = Regex.Split(ReadFileWithoutLock(fileName), formatter.Header).Where(c => !string.IsNullOrWhiteSpace(c));
                        Assert.AreEqual<int>(1, entries.Count());

                        // Make file readonly
                        File.SetAttributes(fileName, FileAttributes.ReadOnly);

                        TestScenario.With1Listener(
                            logger,
                            listener2 =>
                            {
                                listener2.LogToFlatFile(fileName, formatter);

                                // Try logging into readonly file
                                listener.EnableEvents(logger, EventLevel.LogAlways);
                                logger.Verbose("Message 2");
                            });

                        var entries2 = Regex.Split(ReadFileWithoutLock(fileName), formatter.Header).Where(c => !string.IsNullOrWhiteSpace(c));
                        Assert.AreEqual<int>(1, entries2.Count());
                    });
            }
            catch (UnauthorizedAccessException ex)
            {
                Assert.AreEqual("Access to the path '" + path + "\\" + fileName + "' is denied.", ex.Message);
            }
        }

        [TestMethod]
        public void WhenFileNameHasSpace()
        {
            var fileName = "newflatfile With Space.log";
            File.Delete(fileName);
            var formatter = new EventTextFormatter(EventTextFormatter.DashSeparator);
            var logger = MockEventSource.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToFlatFile(fileName, formatter);
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    logger.Informational("Message 1");
                });

            Assert.IsTrue(File.Exists(fileName));
            var entries = Regex.Split(ReadFileWithoutLock(fileName), formatter.Header).Where(c => !string.IsNullOrWhiteSpace(c));
            Assert.AreEqual<int>(1, entries.Count());
        }

        [TestMethod]
        public void WhenFolderNameHasSpace()
        {
            var fileName = "newflatfileInFolderNameWithSpace.log";
            var folderName = "Folder With Space";
            var completePath = DeleteFolder(folderName);
            Directory.CreateDirectory(completePath);
            var folderfilePath = Path.Combine(completePath, fileName);
            var formatter = new EventTextFormatter(EventTextFormatter.DashSeparator);
            var logger = MockEventSource.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToFlatFile(folderfilePath, formatter);
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    logger.Informational("Message 1");
                });

            Assert.IsTrue(File.Exists(folderName + "\\" + fileName));
            var entries = Regex.Split(ReadFileWithoutLock(folderName + "\\" + fileName), formatter.Header).Where(c => !string.IsNullOrWhiteSpace(c));
            Assert.AreEqual<int>(1, entries.Count());
            fileName = folderName + "\\" + fileName;
        }

        [TestMethod]
        public void WhenNonExistingFolder()
        {
            var fileName = "CreateFlatFileInNonExistingFolder.log";
            var folderName = "CreateNewFolder";
            var completePath = DeleteFolder(folderName);
            var folderfilePath = Path.Combine(completePath, fileName);
            var formatter = new EventTextFormatter(EventTextFormatter.DashSeparator);
            var logger = MockEventSource.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToFlatFile(folderfilePath, formatter);
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    logger.Informational("Message 1");
                });

            Assert.IsTrue(File.Exists(folderName + "\\" + fileName));
            var entries = Regex.Split(ReadFileWithoutLock(folderName + "\\" + fileName), formatter.Header).Where(c => !string.IsNullOrWhiteSpace(c));
            Assert.AreEqual<int>(1, entries.Count());
        }

        [TestMethod]
        public void WhenReadOnlyFolder()
        {
            var fileName = "newflatfileInReadOnlyFolder.log";
            var folderName = "ReadOnlyFolder";
            string path = Directory.GetCurrentDirectory();
            string completePath = Path.Combine(path, folderName);
            DirectoryInfo dir = new DirectoryInfo(completePath);
            var formatter = new EventTextFormatter(EventTextFormatter.DashSeparator);
            if (Directory.Exists(completePath))
            {
                dir.Attributes &= ~FileAttributes.ReadOnly;
                Directory.Delete(completePath, true);
            }

            try
            {
                Directory.CreateDirectory(completePath);
                dir.Attributes |= FileAttributes.ReadOnly;
                var folderfilePath = Path.Combine(completePath, fileName);
                using (var listener = new ObservableEventListener())
                {
                    listener.LogToFlatFile(folderfilePath, formatter);
                }

                Assert.IsFalse(File.Exists(fileName));
            }
            finally
            {
                dir.Attributes &= ~FileAttributes.ReadOnly;
                Directory.Delete(completePath, true);
            }
        }

        [TestMethod]
        public void WhenNoAccessFolder()
        {
            var fileName = "newflatfileInFolderWithNoAccess.log";
            var folderName = "FolderWithNoAccessToLoggedInUser";
            string path = Directory.GetCurrentDirectory();
            string completePath = Path.Combine(path, folderName);
            var formatter = new EventTextFormatter(EventTextFormatter.DashSeparator);
            var logger = MockEventSource.Logger;
            DirectoryInfo dir = new DirectoryInfo(completePath);
            if (Directory.Exists(completePath))
            {
                // Give full access to the folder for the loggedin user
                DirectorySecurity dirSecurity = dir.GetAccessControl();
                dirSecurity.AddAccessRule(new FileSystemAccessRule(System.Security.Principal.WindowsIdentity.GetCurrent().Name, FileSystemRights.FullControl, AccessControlType.Allow));
                dir.SetAccessControl(dirSecurity);
                Directory.Delete(completePath, true);
            }

            Directory.CreateDirectory(completePath);

            try
            {
                DirectorySecurity dirSecurityNew = dir.GetAccessControl();
                // Deny access to the folder for the loggedin user
                dirSecurityNew.AddAccessRule(new FileSystemAccessRule(System.Security.Principal.WindowsIdentity.GetCurrent().Name, FileSystemRights.ReadPermissions, AccessControlType.Deny));
                dir.SetAccessControl(dirSecurityNew);
                var folderfilePath = completePath + "\\" + fileName;
                try
                {
                    TestScenario.With1Listener(
                        logger,
                        listener =>
                        {
                            listener.LogToFlatFile(folderfilePath, formatter);
                            listener.EnableEvents(logger, EventLevel.LogAlways);
                            logger.Informational("Message 1");
                        });

                    Assert.IsTrue(File.Exists(folderfilePath));
                    var entries = Regex.Split(ReadFileWithoutLock(folderfilePath), formatter.Header).Where(c => !string.IsNullOrWhiteSpace(c));
                    Assert.AreEqual<int>(1, entries.Count());
                }
                catch (UnauthorizedAccessException ex)
                {
                    Assert.AreEqual("Access to the path '" + folderfilePath + "' is denied.", ex.Message);
                }
            }
            finally
            {
                DirectorySecurity dirSecurity = dir.GetAccessControl();
                dirSecurity.AddAccessRule(new FileSystemAccessRule(System.Security.Principal.WindowsIdentity.GetCurrent().Name, FileSystemRights.FullControl, AccessControlType.Allow));
                dir.SetAccessControl(dirSecurity);
                Directory.Delete(completePath, true);
            }
        }

        [TestMethod]
        public void WhenUsingTextFormatter()
        {
            var fileName = "newflatfileCheckTextFormatter.log";
            File.Delete(fileName);
            var formatter = new EventTextFormatter(EventTextFormatter.DashSeparator);
            formatter.VerbosityThreshold = EventLevel.LogAlways;
            var logger = MockEventSource.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToFlatFile(fileName, formatter);
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    string strDateTime = DateTimeOffset.Now.ToString();
                    logger.Verbose("Message 1");
                });

            Assert.IsTrue(File.Exists(fileName));
            string strLog = ReadFileWithoutLock(fileName);
            StringAssert.Contains(strLog, "----------------------------------------\r\nProviderId : ");
            // Task used to be "Task : 65530"
            StringAssert.Contains(strLog, "\r\nEventId : 4\r\nKeywords : None\r\nLevel : Verbose\r\nMessage : Functional Test\r\nOpcode : Info\r\nTask : 1\r\nVersion : 1\r\nPayload : [message : Message 1] \r\nEventName : PageInfo\r\nTimestamp :");
        }

        [TestMethod]
        public void WhenErrorInLogging()
        {
            var fileName = "ErrorInFormatterIsHandled.log";
            File.Delete(fileName);
            var logger = TestEventSourceNonTransient.Logger;

            TestScenario.With1Listener(
                logger,
                (listener, errorsListener) =>
                {
                    listener.LogToFlatFile(fileName, new MockFormatter(true));
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    logger.EventWithPayload("payload1", 100);

                    StringAssert.Contains(errorsListener.ToString(), "Payload : [message : System.InvalidOperationException: Operation is not valid due to the current state of the object.");
                });
        }

        [TestMethod]
        public void WhenExceptionInFormatter()
        {
            var fileName = "OtherExceptionInFormatterIsHandled.log";
            File.Delete(fileName);
            var logger = TestEventSourceNonTransient.Logger;

            TestScenario.With1Listener(
                logger,
                (listener, errorsListener) =>
                {
                    listener.LogToFlatFile(fileName, new MockFormatter2(true));
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    logger.EventWithPayload("TryingToLog", 100);

                    StringAssert.Contains(errorsListener.ToString(), "Payload : [message : System.ObjectDisposedException: Cannot write to a closed TextWriter.");
                });
        }

        [TestMethod]
        public void WhenErrorAndValidMessageAfterError()
        {
            var fileName = "AfterExceptionInFormatterResultIsOk.log";
            File.Delete(fileName);
            var logger = TestEventSourceNonTransient.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToFlatFile(fileName, new MockFormatter3("header"));
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    logger.EventWithPayload("error", 100);
                    logger.EventWithPayload("not an error", 100);
                });

            Assert.IsTrue(File.Exists(fileName));
            string strLog = ReadFileWithoutLock(fileName);
            //There should not be two headers
            Assert.IsFalse(strLog.Contains("This is an entry containing and error and should not be logged"));
        }

        [TestMethod]
        public void WhenDisposeFileIsUnlocked()
        {
            var fileName = "newflatfileLockingDeletion.log";
            File.Delete(fileName);
            string path = Directory.GetCurrentDirectory();
            var formatter = new EventTextFormatter(EventTextFormatter.DashSeparator);
            var logger = MockEventSource.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToFlatFile(fileName, formatter);
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    logger.Informational("Message 1");
                    logger.Verbose("Message 2");
                });

            File.Delete(fileName);
            Assert.IsFalse(File.Exists(fileName));
        }

        [TestMethod]
        public void WhenFileIsAppendedToAfterDispose()
        {
            var fileName = "newflatfileLockingEditing.log";
            File.Delete(fileName);
            string path = Directory.GetCurrentDirectory();
            var formatter = new EventTextFormatter(EventTextFormatter.DashSeparator);
            var logger = MockEventSource.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToFlatFile(fileName, formatter);
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    logger.Informational("Message 1");
                    logger.Verbose("Message 2");
                });

            // Edit file
            File.AppendText(fileName);
            Assert.IsTrue(File.Exists(fileName));
        }

        [TestMethod]
        public void WhenFilePathLengthExceedsMaxExceptionOccursEarly()
        {
            try
            {
                var folderName = "a";
                string completePath = string.Empty;
                int countLength = Directory.GetCurrentDirectory().Length;
                int folderNameLength = 246 - countLength;
                for (int l = 1; l < folderNameLength; l++)
                {
                    folderName = folderName + "a";
                }

                completePath = DeleteFolder(folderName);
                Directory.CreateDirectory(completePath);
                var fileName = "longpath.log";
                var folderfilePath = completePath + "\\" + fileName;

                var formatter = new EventTextFormatter(EventTextFormatter.DashSeparator);
                using (var listener = new ObservableEventListener())
                {
                    listener.LogToFlatFile(folderfilePath, formatter);
                }

                Assert.IsFalse(File.Exists(folderfilePath));
            }
            catch (PathTooLongException ex)
            {
                Assert.AreEqual(ex.Message, "The specified path, file name, or both are too long. The fully qualified file name must be less than 260 characters, and the directory name must be less than 248 characters.");
            }
        }

        [TestMethod]
        public void WhenSourceIsRenabled()
        {
            var fileName = @".\Renabled.Log";
            File.Delete(fileName);
            var logger = BasicTestEventSource.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToFlatFile(fileName, new EventTextFormatter());
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    logger.RaiseBasicTestEventSourceEvent("Test message1");
                    listener.DisableEvents(logger);

                    logger.RaiseBasicTestEventSourceEvent("Test message2");

                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    logger.RaiseBasicTestEventSourceEvent("Test message3");
                });

            var fileContents = LogFileReader.GetEntries(fileName);
            Assert.IsTrue(fileContents.Count == 2);
            Assert.AreEqual<string>("[message : Test message1]", fileContents[0]["Payload"]);
            Assert.AreEqual<string>("[message : Test message3]", fileContents[1]["Payload"]);
        }

        [TestMethod]
        public void WhenVerbosityIsLowFilteringOccurs()
        {
            var fileName = @".\Verbosity.Log";
            File.Delete(fileName);
            var logger = BasicTestEventSource.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToFlatFile(fileName, new EventTextFormatter());
                    listener.EnableEvents(logger, EventLevel.Error);
                    logger.RaiseEventWithMaxVerbosityAsError("Error message");
                    logger.RaiseEventWithMaxVerbosityAsInformational("Informational message");
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    logger.RaiseEventWithMaxVerbosityAsCritical("Critical message");
                });

            var fileContents = LogFileReader.GetEntries(fileName);
            Assert.IsTrue(fileContents.Count == 2);
            Assert.AreEqual<string>("[message : Error message]", fileContents[0]["Payload"]);
            Assert.AreEqual<string>("[message : Critical message]", fileContents[1]["Payload"]);
            Assert.AreEqual<string>("Error", fileContents[0]["Level"]);
            Assert.AreEqual<string>("Critical", fileContents[1]["Level"]);
        }

        [TestMethod]
        public void WhenFileNameIsNull()
        {
            try
            {
                var formatter = new EventTextFormatter(EventTextFormatter.DashSeparator);
                using (var listener = new ObservableEventListener())
                {
                    listener.LogToFlatFile(null, formatter);
                }
            }
            catch (ArgumentNullException ex)
            {
                Assert.AreEqual("Value cannot be null.\r\nParameter name: fileName", ex.Message);
            }
        }

        [TestMethod]
        public void WhenFileNameIsEmpty()
        {
            try
            {
                var formatter = new EventTextFormatter(EventTextFormatter.DashSeparator);
                using (var listener = new ObservableEventListener())
                {
                    listener.LogToFlatFile(string.Empty, formatter);
                }
            }
            catch (ArgumentException ex)
            {
                Assert.AreEqual("Argument is empty\r\nParameter name: fileName", ex.Message);
            }
        }

        [TestMethod]
        public void WhenInvalidCharactersInFileName()
        {
            try
            {
                var formatter = new EventTextFormatter(EventTextFormatter.DashSeparator);
                using (var listener = new ObservableEventListener())
                {
                    listener.LogToFlatFile(@">", formatter);
                }
            }
            catch (ArgumentException ex)
            {
                Assert.AreEqual("Illegal characters in path.", ex.Message);
            }
        }

        [TestMethod]
        public void WhenInvalidCharactersInFileName1()
        {
            try
            {
                var formatter = new EventTextFormatter(EventTextFormatter.DashSeparator);
                using (var listener = new ObservableEventListener())
                {
                    listener.LogToFlatFile(@"|", formatter);
                }
            }
            catch (ArgumentException ex)
            {
                Assert.AreEqual("Illegal characters in path.", ex.Message);
            }
        }

        [TestMethod]
        public void WhenInvalidCharactersInFileName2()
        {
            try
            {
                var formatter = new EventTextFormatter(EventTextFormatter.DashSeparator);
                using (var listener = new ObservableEventListener())
                {
                    listener.LogToFlatFile(@"..\", formatter);
                }
            }
            catch (ArgumentException ex)
            {
                Assert.AreEqual("A file name with a relative path is not allowed. Provide only the file name or the full path of the file.", ex.Message);
            }
        }

        [TestMethod]
        public void WhenFileNameIsFolder()
        {
            try
            {
                var formatter = new EventTextFormatter(EventTextFormatter.DashSeparator);
                using (var listener = new ObservableEventListener())
                {
                    listener.LogToFlatFile(".", formatter);
                }
            }
            catch (ArgumentException ex)
            {
                Assert.AreEqual("A file name with a relative path is not allowed. Provide only the file name or the full path of the file.", ex.Message);
            }
        }

        [TestMethod]
        public void WhenUsingNullFormatter()
        {
            var fileName = "textFormatterAsNull.log";
            File.Delete(fileName);
            var formatter = new EventTextFormatter(EventTextFormatter.DashSeparator);
            var logger = MockEventSource.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToFlatFile(fileName, null);
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    logger.Informational("Message 1");
                });

            Assert.IsTrue(File.Exists(fileName));
            var entries = Regex.Split(ReadFileWithoutLock(fileName), formatter.Header).Where(c => !string.IsNullOrWhiteSpace(c));
            Assert.AreEqual<int>(1, entries.Count());
            Assert.IsFalse(File.ReadAllText(fileName).Contains(EventTextFormatter.DashSeparator));
        }

        [TestMethod]
        public void WhenFormatterIsNotSpecified()
        {
            var fileName = "NotextFormatter.log";
            File.Delete(fileName);
            var formatter = new EventTextFormatter(EventTextFormatter.DashSeparator);
            var logger = MockEventSource.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToFlatFile(fileName);
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    logger.Informational("Message 1");
                });

            Assert.IsTrue(File.Exists(fileName));
            var entries = Regex.Split(ReadFileWithoutLock(fileName), formatter.Header).Where(c => !string.IsNullOrWhiteSpace(c));
            Assert.AreEqual<int>(1, entries.Count());
            Assert.IsFalse(File.ReadAllText(fileName).Contains(EventTextFormatter.DashSeparator));
        }

        [TestMethod]
        public void WhenPayloadIsLarge()
        {
            var fileName = "MaxString.log";
            File.Delete(fileName);
            var formatter = new EventTextFormatter(EventTextFormatter.DashSeparator);
            var logger = MockEventSource.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToFlatFile(fileName);
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    logger.Informational(new string('a', 50000000));
                });

            Assert.IsTrue(File.ReadAllText(fileName).Contains(new string('a', 50000000)));
        }

        [TestMethod]
        public void WhenSingleLineTextFormatter()
        {
            var fileName = "singleLine.log";
            File.Delete(fileName);
            var formatter = new EventTextFormatter(EventTextFormatter.DashSeparator, EventTextFormatter.DashSeparator);
            var logger = MockEventSource.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToFlatFile(fileName, formatter);
                    listener.EnableEvents(logger, EventLevel.Error);
                    logger.Informational("Message 1");
                    logger.Error("Error 1");
                    logger.Critical("Critical 1");
                });

            Assert.IsTrue(File.Exists(fileName));
            string fileText = File.ReadAllText(fileName);
            Assert.IsFalse(fileText.Contains("Message 1"));
            Assert.IsTrue(fileText.Contains("Error 1"));
            Assert.IsTrue(fileText.Contains("Critical 1"));
            Assert.IsTrue(fileText.Contains("Payload : [message : Error 1]"));
        }

        [TestMethod]
        public void WhenKeywordsAllIsEnabled()
        {
            var fileName = "KeywordsAll.log";
            File.Delete(fileName);
            var formatter = new EventTextFormatter(EventTextFormatter.DashSeparator, EventTextFormatter.DashSeparator, EventLevel.LogAlways);
            var logger = MockEventSource.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToFlatFile(fileName, formatter);
                    listener.EnableEvents(logger, EventLevel.LogAlways, Keywords.All);
                    logger.VerboseWithKeywordPage("VerboseWithKeywordPage");
                    logger.InfoWithKeywordDiagnostic("InfoWithKeywordDiagnostic");
                });

            Assert.IsTrue(File.Exists(fileName));
            string fileText = ReadFileWithoutLock(fileName);
            Assert.IsTrue(fileText.Contains("Keywords : 1"));
            Assert.IsTrue(fileText.Contains("Keywords : 4"));
        }

        [TestMethod]
        public void WhenKeywordIsNotSpecifedWhenEnabled()
        {
            var fileName = "KeywordsAllKeywordAsNotNone.log";
            File.Delete(fileName);
            var formatter = new EventTextFormatter(EventTextFormatter.DashSeparator, EventTextFormatter.DashSeparator);
            formatter.VerbosityThreshold = EventLevel.Error;
            var logger = MockEventSource.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToFlatFile(fileName, formatter);
                    listener.EnableEvents(logger, EventLevel.Error);
                    logger.ErrorWithKeywordDiagnostic("ErrorWithKeywordDiagnostic");
                });

            Assert.IsTrue(File.Exists(fileName));
            string fileText = File.ReadAllText(fileName);
            Assert.IsTrue(fileText.Contains(string.Empty));
        }

        [TestMethod]
        public void WhenVerbosityThresholdIsSet()
        {
            var fileName = "CheckAllProperties.log";
            File.Delete(fileName);
            var formatter = new EventTextFormatter(EventTextFormatter.DashSeparator, EventTextFormatter.DashSeparator);
            formatter.VerbosityThreshold = EventLevel.Error;
            var logger = MockEventSource.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToFlatFile(fileName, formatter);
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    logger.Error("Error message");
                    logger.Warning("Warning message");
                    logger.Critical("Critical message");
                });

            Assert.IsTrue(File.Exists(fileName));
            string fileText = File.ReadAllText(fileName);
            // Task used to be "Task : 65531"
            Assert.IsTrue(fileText.Contains("\r\nEventId : 3\r\nKeywords : None\r\nLevel : Error\r\nMessage : Test Error\r\nOpcode : Stop\r\nTask : 2\r\nVersion : 3\r\nPayload : [message : Error message] \r\nEventName : DBQueryStop\r\nTimestamp :"));
            // Task used to be "Task : 65532"
            Assert.IsTrue(fileText.Contains("\r\nEventId : 2\r\nKeywords : None\r\nLevel : Critical\r\nMessage : Functional Test\r\nOpcode : Info\r\nTask : 2\r\nVersion : 0\r\nPayload : [message : Critical message] \r\nEventName : DBQueryInfo\r\nTimestamp :"));
            Assert.IsTrue(fileText.Contains("----------------------------------------\r\nEventId : 6, Level : Warning, Message : Test Warning, Payload : [message : Warning message] , EventName : DBQuerySend, Timestamp :"));
        }

        [TestMethod]
        public void WhenEventWithTaskName()
        {
            var fileName = "CheckTaskName.log";
            File.Delete(fileName);
            var formatter = new EventTextFormatter(EventTextFormatter.DashSeparator, EventTextFormatter.DashSeparator, EventLevel.LogAlways);
            var logger = MockEventSource.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToFlatFile(fileName, formatter);
                    listener.EnableEvents(logger, EventLevel.LogAlways, Keywords.All);
                    logger.CriticalWithTaskName("CriticalWithTaskName");
                    logger.InfoWithKeywordDiagnostic("InfoWithKeywordDiagnostic");
                });

            Assert.IsTrue(File.Exists(fileName));
            string fileText = ReadFileWithoutLock(fileName);
            Assert.IsTrue(fileText.Contains("Task : 1\r\nVersion : 0\r\nPayload : [message : CriticalWithTaskName] \r\nEventName : PageSuspend"));
            Assert.IsTrue(fileText.Contains("Task : 64512\r\nVersion : 0\r\nPayload : [message : InfoWithKeywordDiagnostic] \r\nEventName : InfoWithKeywordDiagnosticInfo"));
        }

        [TestMethod]
        public void WhenSinkIsAsync()
        {
            var fileName = "CanLogToFlatFileAsync.log";
            int count = 2;
            File.Delete(fileName);
            var formatter = new EventTextFormatter(EventTextFormatter.DashSeparator, EventTextFormatter.DashSeparator);
            var logger = MockEventSource.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToFlatFile(fileName, formatter, true);
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    var loggerTasks = new Task[count];
                    for (int i = 0; i < count; i++)
                    {
                        loggerTasks[i] = Task.Run(() => logger.Informational(i + "some message in parallel"));
                    }

                    Task.WaitAll(loggerTasks);
                });

            var entries = Regex.Split(ReadFileWithoutLock(fileName), formatter.Header).Where(c => !string.IsNullOrWhiteSpace(c));
            Assert.AreEqual(count, entries.Count());
        }

        [TestMethod]
        public void WhenDisposeFlushOccurs()
        {
            var fileName = "TestDispose.log";
            File.Delete(fileName);
            var logger = MockEventSource.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToFlatFile(fileName);
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    Thread[] threads = new Thread[10];
                    for (int i = 0; i < 10; i++)
                    {
                        threads[i] = new Thread(new ThreadStart(() =>
                        {
                            for (int j = 0; j < 1000; j++)
                            {
                                logger.Critical("TestMsg " + j);
                            }
                        }));

                        threads[i].Start();
                    }

                    for (int i = 0; i < 10; i++)
                    {
                        threads[i].Join();
                    }
                });

            Assert.IsTrue(File.ReadAllText(fileName).Contains("TestMsg 0"));
            Assert.IsTrue(File.ReadAllText(fileName).Contains("TestMsg 999"));
        }

        [TestMethod]
        public void WhenUsingStaticCreate()
        {
            var fileName = "TestingCreateListener.log";
            File.Delete(fileName);
            var formatter = new EventTextFormatter("------");
            var logger = MockEventSource.Logger;

            using (var listener = FlatFileLog.CreateListener(fileName, formatter, false))
            {
                try
                {
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    for (int n = 0; n < 2; n++)
                    {
                        logger.Informational("some message to flat file " + n.ToString());
                    }
                }
                finally
                {
                    listener.DisableEvents(logger);
                }
            }

            Assert.IsTrue(File.Exists(fileName));
            string fileContents = File.ReadAllText(fileName);
            Assert.IsTrue(fileContents.Contains("------\r\nEventId : 1, Level : Informational, Message : , Payload : [message : some message to flat file 0] , EventName : InformationalInfo, Timestamp :"), "File contents: " + fileContents);
        }

        [TestMethod]
        public void WhenPositionalParametersInMessageInText()
        {
            var fileName = "Task1557.log";
            File.Delete(fileName);
            var formatter = new EventTextFormatter(EventTextFormatter.DashSeparator);
            var logger = TestEventSourceNoAttributes.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToFlatFile(fileName, formatter);
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    logger.ObjectArrayEvent4(10, "stringarg1", 20, "stringarg3", 30);
                });

            Assert.IsTrue(File.Exists(fileName));
            var entries = Regex.Split(ReadFileWithoutLock(fileName), formatter.Header).Where(c => !string.IsNullOrWhiteSpace(c));
            Assert.AreEqual<int>(1, entries.Count());
            string readFile = File.ReadAllText(fileName);
            Assert.IsTrue(readFile.Contains("Check if it is logged"));
            Assert.IsTrue(readFile.Contains("[arg0 : 10] [arg1 : stringarg1] [arg2 : 20] [arg3 : stringarg3] [arg4 : 30]"));
        }

        [TestMethod]
        public void WhenPositionalParametersInMessageInJson()
        {
            var fileName = "Task1557.log";
            File.Delete(fileName);
            var formatter = new EventTextFormatter(EventTextFormatter.DashSeparator);
            var jsonformatter = new JsonEventTextFormatter();
            jsonformatter.DateTimeFormat = "dd/MM/yyyy";
            var logger = TestEventSourceNoAttributes.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToFlatFile(fileName, jsonformatter);
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    logger.ObjectArrayEvent4(10, "stringarg1", 20, "stringarg3", 30);
                });

            Assert.IsTrue(File.Exists(fileName));
            var entries = Regex.Split(ReadFileWithoutLock(fileName), formatter.Header).Where(c => !string.IsNullOrWhiteSpace(c));
            Assert.AreEqual<int>(1, entries.Count());
            string readFile = File.ReadAllText(fileName);
            Assert.IsTrue(readFile.Contains("Check if it is logged"));
            Assert.IsTrue(readFile.Contains("{\"arg0\":10,\"arg1\":\"stringarg1\",\"arg2\":20,\"arg3\":\"stringarg3\",\"arg4\":30}"));
        }

        [TestMethod]
        public void WhenPositonalParametersInMessageInXml()
        {
            var fileName = "Task1557.log";
            File.Delete(fileName);
            var formatter = new EventTextFormatter(EventTextFormatter.DashSeparator);
            var xmlformat = new XmlEventTextFormatter();
            xmlformat.DateTimeFormat = "dd/MM/yyyy";
            var logger = TestEventSourceNoAttributes.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToFlatFile(fileName, xmlformat);
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    logger.ObjectArrayEvent4(20, "stringarg1", 30, "stringarg3", 40);
                });

            Assert.IsTrue(File.Exists(fileName));
            var entries = Regex.Split(ReadFileWithoutLock(fileName), formatter.Header).Where(c => !string.IsNullOrWhiteSpace(c));
            Assert.AreEqual<int>(1, entries.Count());
            string readFile = File.ReadAllText(fileName);
            Assert.IsTrue(readFile.Contains("<Message>Check if it is logged</Message>"));
            Assert.IsTrue(readFile.Contains("<Data Name=\"arg0\">20</Data><Data Name=\"arg1\">stringarg1</Data><Data Name=\"arg2\">30</Data><Data Name=\"arg3\">stringarg3</Data><Data Name=\"arg4\">40</Data>"));
        }

        // Bug 916
        [TestMethod]
        public void WhenFileAttributesChangeDuringLogging()
        {
            var fileName = "CheckDeny_Then_ReenableAccess_LogsData.log";
            DeleteReadOnlyFile(fileName);
            string path = Directory.GetCurrentDirectory();
            var formatter = new EventTextFormatter(EventTextFormatter.DashSeparator);
            var logger = MockEventSource.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToFlatFile(fileName, formatter);
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    logger.Informational("Message 1");
                });

            Assert.IsTrue(File.Exists(fileName));
            var entries = Regex.Split(ReadFileWithoutLock(fileName), formatter.Header).Where(c => !string.IsNullOrWhiteSpace(c));
            Assert.AreEqual<int>(1, entries.Count());

            File.SetAttributes(fileName, FileAttributes.ReadOnly);
            Task.Delay(TimeSpan.FromSeconds(1)).Wait();
            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    File.SetAttributes(fileName, FileAttributes.Normal);
                    Task.Delay(TimeSpan.FromSeconds(1)).Wait();
                    listener.LogToFlatFile(fileName, formatter);
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    logger.Verbose("Message 2");
                });

            var entries2 = Regex.Split(ReadFileWithoutLock(fileName), formatter.Header).Where(c => !string.IsNullOrWhiteSpace(c));
            Assert.AreEqual<int>(2, entries2.Count());
        }

        [TestMethod]
        public void WhenEnumsInPayload()
        {
            var fileName = "TextFormatterAndEnums.log";
            File.Delete(fileName);
            var formatter = new EventTextFormatter(EventTextFormatter.DashSeparator, EventTextFormatter.DashSeparator);
            var logger = MockEventSourceInProcEnum.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToFlatFile(fileName, formatter, true);
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    logger.SendEnumsEvent16(MockEventSourceInProcEnum.MyColor.Green, MockEventSourceInProcEnum.MyFlags.Flag2);
                });

            var entries = Regex.Split(ReadFileWithoutLock(fileName), formatter.Header).Where(c => !string.IsNullOrWhiteSpace(c));
            Assert.AreEqual(1, entries.Count());
            StringAssert.Contains(entries.First(), "Payload : [a : Green] [b : Flag2]");
        }

        [TestMethod]
        public void WhenMultipleSourcesAreEnabledForSameListener()
        {
            EventTextFormatter eventTextFormatter = new EventTextFormatter(EventTextFormatter.DashSeparator);
            var fileName = "newflatfile.log";
            File.Delete(fileName);
            var logger1 = MockEventSource.Logger;
            var logger2 = MockEventSource2.Logger;
            var logger3 = MockEventSource3.Logger;

            TestScenario.With1Listener(
                new EventSource[] { logger1, logger2, logger3 },
                listener =>
                {
                    listener.LogToFlatFile(fileName, eventTextFormatter);
                    listener.EnableEvents(logger1, EventLevel.LogAlways);
                    listener.EnableEvents(logger2, EventLevel.LogAlways);
                    listener.EnableEvents(logger3, EventLevel.LogAlways);
                    logger1.Informational("message 1");
                    logger2.Error("error 1");
                    logger3.Critical("critical 1");
                });

            Assert.IsTrue(File.Exists(fileName));
            var entries = Regex.Split(LogFileReader.ReadFileWithoutLock(fileName), eventTextFormatter.Header).Where(c => !string.IsNullOrWhiteSpace(c));
            Assert.AreEqual<int>(3, entries.Count());
            StringAssert.Contains(entries.ToArray()[0], "Informational");
            StringAssert.Contains(entries.ToArray()[0], "message 1");
            StringAssert.Contains(entries.ToArray()[1], "Error");
            StringAssert.Contains(entries.ToArray()[1], "error 1");
            StringAssert.Contains(entries.ToArray()[2], "Critical");
            StringAssert.Contains(entries.ToArray()[2], "critical 1");
        }

        [TestMethod]
        public void WhenSourceHasNoAttributes()
        {
            string fileName = @".\NoAttribEvent1.log";
            File.Delete(fileName);
            var formatter = new EventTextFormatter(verbosityThreshold: EventLevel.LogAlways);
            var logger = TestEventSourceNoAttributes.Logger;

            List<EventSchema> events = new List<EventSchema>();
            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToFlatFile(fileName);
                    listener.EnableEvents(logger, EventLevel.Informational);
                    for (int i = 1; i < 16; i++)
                    {
                        events.Add(EventSourceSchemaCache.Instance.GetSchema(i, logger));
                    }
                });

            Assert.AreEqual<string>("NoArgEvent1", events[0].TaskName);
            Assert.AreEqual<string>("IntArgEvent2", events[1].TaskName);
            Assert.AreEqual<string>("LongArgEvent3", events[2].TaskName);
            Assert.AreEqual<string>("ObjectArrayEvent4", events[3].TaskName);
            Assert.AreEqual<string>("StringArgEvent5", events[4].TaskName);
            Assert.AreEqual<string>("TwoIntArgEvent6", events[5].TaskName);
            Assert.AreEqual<string>("TwoLongArgEvent7", events[6].TaskName);
            Assert.AreEqual<string>("StringAndIntArgEvent8", events[7].TaskName);
            Assert.AreEqual<string>("StringAndLongArgEvent9", events[8].TaskName);
            Assert.AreEqual<string>("StringAndStringArgEvent10", events[9].TaskName);
            Assert.AreEqual<string>("ThreeIntArgEvent11", events[10].TaskName);
            Assert.AreEqual<string>("ThreeLongArgEvent12", events[11].TaskName);
            Assert.AreEqual<string>("StringAndTwoIntArgEvent13", events[12].TaskName);
            Assert.AreEqual<string>("ThreeStringArgEvent14", events[13].TaskName);
            Assert.AreEqual<string>("SendEnumsEvent15", events[14].TaskName);
        }

        [TestMethod]
        public void WhenNoArgPayload()
        {
            string fileName = @".\NoArgEvent.log";
            File.Delete(fileName);
            var formatter = new EventTextFormatter(verbosityThreshold: EventLevel.LogAlways);
            var logger = TestEventSourceNoAttributes.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToFlatFile(fileName, formatter);
                    listener.EnableEvents(logger, EventLevel.Informational);
                    logger.NoArgEvent1();
                });

            var fileContents = LogFileReader.GetEntries(fileName);
            Assert.AreEqual<string>("1", fileContents[0]["EventId"]);
            Assert.AreEqual<string>(string.Empty, fileContents[0]["Payload"]);
        }

        [TestMethod]
        public void WhenIntArgPayload()
        {
            string fileName = @".\IntArgEventIsLogged.log";
            File.Delete(fileName);
            var formatter = new EventTextFormatter(verbosityThreshold: EventLevel.LogAlways);
            var logger = TestEventSourceNoAttributes.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToFlatFile(fileName, formatter);
                    listener.EnableEvents(logger, EventLevel.Informational);
                    logger.IntArgEvent2(10);
                });

            var fileContents = LogFileReader.GetEntries(fileName);
            Assert.AreEqual<string>("2", fileContents[0]["EventId"]);
            var payLoad = PayloadParser.GetPayload(fileContents[0]["Payload"]);
            Assert.AreEqual<string>("10", payLoad["arg"]);
        }

        [TestMethod]
        public void WhenLongArgPayload()
        {
            string fileName = @".\LongArgEventIsLogged";
            File.Delete(fileName);
            var formatter = new EventTextFormatter(verbosityThreshold: EventLevel.LogAlways);
            var logger = TestEventSourceNoAttributes.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToFlatFile(fileName, formatter);
                    listener.EnableEvents(logger, EventLevel.Informational);
                    logger.LongArgEvent3((long)10);
                });

            var fileContents = LogFileReader.GetEntries(fileName);
            Assert.AreEqual<string>("3", fileContents[0]["EventId"]);
            var payLoad = PayloadParser.GetPayload(fileContents[0]["Payload"]);
            Assert.AreEqual<string>("10", payLoad["arg"]);
        }

        [TestMethod]
        public void WhenObjectArgPayload()
        {
            string fileName = @".\ObjectArgsEventIsLogged";
            File.Delete(fileName);
            var formatter = new EventTextFormatter(verbosityThreshold: EventLevel.LogAlways);
            var logger = TestEventSourceNoAttributes.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToFlatFile(fileName, formatter);
                    listener.EnableEvents(logger, EventLevel.Informational);
                    logger.ObjectArrayEvent4(10, "stringarg1", 20, "stringarg3", 30);
                });

            var fileContents = LogFileReader.GetEntries(fileName);
            Assert.AreEqual<string>("4", fileContents[0]["EventId"]);
            var payLoad = PayloadParser.GetPayload(fileContents[0]["Payload"]);
            Assert.AreEqual<string>("10", payLoad["arg0"]);
            Assert.AreEqual<string>("stringarg1", payLoad["arg1"]);
            Assert.AreEqual<string>("20", payLoad["arg2"]);
            Assert.AreEqual<string>("stringarg3", payLoad["arg3"]);
            Assert.AreEqual<string>("30", payLoad["arg4"]);
        }

        [TestMethod]
        public void WhenTwoIntArgPayload()
        {
            string fileName = @".\TwoIntArgsEventIsLogged";
            File.Delete(fileName);
            var formatter = new EventTextFormatter(verbosityThreshold: EventLevel.LogAlways);
            var logger = TestEventSourceNoAttributes.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToFlatFile(fileName, formatter);
                    listener.EnableEvents(logger, EventLevel.Informational);
                    logger.TwoIntArgEvent6(10, 30);
                });

            var fileContents = LogFileReader.GetEntries(fileName);
            Assert.AreEqual<string>("6", fileContents[0]["EventId"]);
            var payLoad = PayloadParser.GetPayload(fileContents[0]["Payload"]);
            Assert.AreEqual<string>("10", payLoad["arg1"]);
            Assert.AreEqual<string>("30", payLoad["arg2"]);
        }

        [TestMethod]
        public void WhenThreeStringPayload()
        {
            string fileName = @".\ThreeStringArgsIsLogged";
            File.Delete(fileName);
            var formatter = new EventTextFormatter(verbosityThreshold: EventLevel.LogAlways);
            var logger = TestEventSourceNoAttributes.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToFlatFile(fileName, formatter);
                    listener.EnableEvents(logger, EventLevel.Informational);
                    logger.ThreeStringArgEvent14("message1", "message2", "message3");
                });

            var fileContents = LogFileReader.GetEntries(fileName);
            Assert.AreEqual<string>("14", fileContents[0]["EventId"]);
            var payLoad = PayloadParser.GetPayload(fileContents[0]["Payload"]);
            Assert.AreEqual<string>("message1", payLoad["arg1"]);
            Assert.AreEqual<string>("message2", payLoad["arg2"]);
            Assert.AreEqual<string>("message3", payLoad["arg3"]);
        }

        [TestMethod]
        public void WhenStringAndLongPayload()
        {
            string fileName = @".\StringAndLongArgEventIsLogged";
            File.Delete(fileName);
            var formatter = new EventTextFormatter(verbosityThreshold: EventLevel.LogAlways);
            var logger = TestEventSourceNoAttributes.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToFlatFile(fileName, formatter);
                    listener.EnableEvents(logger, EventLevel.Informational);
                    logger.StringAndLongArgEvent9("message1", 20);
                });

            var fileContents = LogFileReader.GetEntries(fileName);
            Assert.AreEqual<string>("9", fileContents[0]["EventId"]);
            var payLoad = PayloadParser.GetPayload(fileContents[0]["Payload"]);
            Assert.AreEqual<string>("message1", payLoad["arg1"]);
            Assert.AreEqual<string>("20", payLoad["arg2"]);
        }

        [TestMethod]
        public void WhenEnumAndFlagPayload()
        {
            string fileName = @".\EnumAndFlagIsLogged";
            File.Delete(fileName);
            var formatter = new EventTextFormatter(verbosityThreshold: EventLevel.LogAlways);
            var logger = TestEventSourceNoAttributes.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToFlatFile(fileName, formatter);
                    listener.EnableEvents(logger, EventLevel.Informational);
                    logger.SendEnumsEvent15(MyColor.Green, MyFlags.Flag1 | MyFlags.Flag3);
                });

            var fileContents = LogFileReader.GetEntries(fileName);
            Assert.AreEqual<string>("15", fileContents[0]["EventId"]);
            var payLoad = PayloadParser.GetPayload(fileContents[0]["Payload"]);
            Assert.AreEqual<string>(((int)MyColor.Green).ToString(), payLoad["color"]);
            Assert.AreEqual<string>("5", payLoad["flags"]);
        }

        [TestMethod]
        public void WhenSourceEventHasNoTask()
        {
            string fileName = @".\EventAttributeUsedAndNoTaskSpecified.log";
            File.Delete(fileName);
            var formatter = new EventTextFormatter(verbosityThreshold: EventLevel.LogAlways);
            var logger = TestAttributesEventSource.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToFlatFile(fileName, formatter);
                    listener.EnableEvents(logger, EventLevel.Verbose);
                    logger.NoTaskSpecfied(1, 3, 5);
                });

            var fileContents = LogFileReader.GetEntries(fileName);
            Assert.AreEqual("104", fileContents[0]["EventId"]);
        }

        [TestMethod]
        public void WhenNonDefaultOpCode()
        {
            string fileName = @".\NonDefaultOpCodeIsLogged.log";
            File.Delete(fileName);
            var formatter = new EventTextFormatter(verbosityThreshold: EventLevel.LogAlways);
            var logger = TestAttributesEventSource.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToFlatFile(fileName, formatter);
                    listener.EnableEvents(logger, EventLevel.Verbose);
                    logger.NonDefaultOpcodeNonDefaultVersionEvent(1, 3, 5);
                });

            var fileContents = LogFileReader.GetEntries(fileName);
            Assert.AreEqual("103", fileContents[0]["EventId"]);
            Assert.AreEqual("Reply", fileContents[0]["Opcode"]);
        }

        [TestMethod]
        public void WhenNonDefaultVersion()
        {
            string fileName = @".\NonDefaultVersionIsLogged.log";
            File.Delete(fileName);
            var formatter = new EventTextFormatter(verbosityThreshold: EventLevel.LogAlways);
            var logger = TestAttributesEventSource.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToFlatFile(fileName, formatter);
                    listener.EnableEvents(logger, EventLevel.Verbose);
                    logger.NonDefaultOpcodeNonDefaultVersionEvent(1, 3, 5);
                });

            var fileContents = LogFileReader.GetEntries(fileName);
            Assert.AreEqual("2", fileContents[0]["Version"]);
        }

        [TestMethod]
        public void WhenNonDefaultMessage()
        {
            string fileName = @".\NonDefaultMessageIsLogged.log";
            File.Delete(fileName);
            var formatter = new EventTextFormatter(verbosityThreshold: EventLevel.LogAlways);
            var logger = TestAttributesEventSource.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToFlatFile(fileName, formatter);
                    listener.EnableEvents(logger, EventLevel.Verbose);
                    logger.NonDefaultOpcodeNonDefaultVersionEvent(1, 3, 5);
                });

            var fileContents = LogFileReader.GetEntries(fileName);
            Assert.AreEqual("arg1- 1,arg2- 3,arg3- 5", fileContents[0]["Message"]);
        }

        [TestMethod]
        public void WhenActivityId()
        {
            string fileName = @".\IntArgEventIsLogged.log";
            File.Delete(fileName);
            var formatter = new EventTextFormatter(verbosityThreshold: EventLevel.LogAlways);
            var logger = TestEventSourceNoAttributes.Logger;

            var activityId = Guid.NewGuid();
            var previousActivityId = Guid.Empty;
            EventSource.SetCurrentThreadActivityId(activityId, out previousActivityId);
            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToFlatFile(fileName, formatter);
                    listener.EnableEvents(logger, EventLevel.Informational);
                    logger.IntArgEvent2(10);
                });

            EventSource.SetCurrentThreadActivityId(previousActivityId);

            var fileContents = LogFileReader.GetEntries(fileName);
            Assert.AreEqual<string>("2", fileContents[0]["EventId"]);
            var payLoad = PayloadParser.GetPayload(fileContents[0]["Payload"]);
            Assert.AreEqual<string>("10", payLoad["arg"]);
            Assert.AreEqual(activityId, Guid.Parse(fileContents[0]["ActivityId"]));
            Assert.IsNull(fileContents[0].Keys.SingleOrDefault(k => k == "RelatedActivityId"));
        }

        [TestMethod]
        public void WhenActivityIdAndRelatedActivityId()
        {
            string fileName = @".\IntArgEventIsLogged.log";
            File.Delete(fileName);
            var formatter = new EventTextFormatter(verbosityThreshold: EventLevel.LogAlways);
            var logger = TestEventSourceNoAttributes.Logger;

            var activityId = Guid.NewGuid();
            var relatedActivityId = Guid.NewGuid();
            var previousActivityId = Guid.Empty;
            EventSource.SetCurrentThreadActivityId(activityId, out previousActivityId);
            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToFlatFile(fileName, formatter);
                    listener.EnableEvents(logger, EventLevel.Informational);
                    logger.IntArgEventWithRelatedActivityId(10, relatedActivityId);
                });

            EventSource.SetCurrentThreadActivityId(previousActivityId);

            var fileContents = LogFileReader.GetEntries(fileName);
            Assert.AreEqual<string>("16", fileContents[0]["EventId"]);
            var payLoad = PayloadParser.GetPayload(fileContents[0]["Payload"]);
            Assert.AreEqual<string>("10", payLoad["arg"]);
            Assert.AreEqual(activityId, Guid.Parse(fileContents[0]["ActivityId"]));
            Assert.AreEqual(relatedActivityId, Guid.Parse(fileContents[0]["RelatedActivityId"]));
        }

        [TestMethod]
        public void WhenProcessId()
        {
            string fileName = @".\IntArgEventIsLogged.log";
            File.Delete(fileName);
            var formatter = new EventTextFormatter(verbosityThreshold: EventLevel.LogAlways);
            var logger = TestEventSourceNoAttributes.Logger;

            int processId = System.Diagnostics.Process.GetCurrentProcess().Id;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToFlatFile(fileName, formatter);
                    listener.EnableEvents(logger, EventLevel.Informational);
                    logger.IntArgEvent2(10);
                });

            var fileContents = LogFileReader.GetEntries(fileName);
            Assert.AreEqual<string>("2", fileContents[0]["EventId"]);
            var payLoad = PayloadParser.GetPayload(fileContents[0]["Payload"]);
            Assert.AreEqual<string>("10", payLoad["arg"]);
            Assert.AreEqual(processId, Convert.ToInt32(fileContents[0]["ProcessId"]));
        }

        [TestMethod]
        public void WhenThreadId()
        {
            string fileName = @".\IntArgEventIsLogged.log";
            File.Delete(fileName);
            var formatter = new EventTextFormatter(verbosityThreshold: EventLevel.LogAlways);
            var logger = TestEventSourceNoAttributes.Logger;

            int threadId = ThreadHelper.GetCurrentUnManagedThreadId();

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToFlatFile(fileName, formatter);
                    listener.EnableEvents(logger, EventLevel.Informational);
                    logger.IntArgEvent2(10);
                });

            var fileContents = LogFileReader.GetEntries(fileName);
            Assert.AreEqual<string>("2", fileContents[0]["EventId"]);
            var payLoad = PayloadParser.GetPayload(fileContents[0]["Payload"]);
            Assert.AreEqual<string>("10", payLoad["arg"]);
            Assert.AreEqual(threadId, Convert.ToInt32(fileContents[0]["ThreadId"]));
        }

        private string ReadFileWithoutLock(string fileName)
        {
            using (var reader = new StreamReader(new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
            {
                return reader.ReadToEnd();
            }
        }

        private void DeleteReadOnlyFile(string fileName)
        {
            if (File.Exists(fileName))
            {
                File.SetAttributes(fileName, FileAttributes.Normal);
            }

            File.Delete(fileName);
        }

        private string DeleteFolder(string folderName)
        {
            string path = Directory.GetCurrentDirectory();
            string completePath = Path.Combine(path, folderName);
            if (Directory.Exists(completePath))
            {
                Directory.Delete(completePath, true);
            }

            return completePath;
        }

        public static class PayloadParser
        {
            public static Dictionary<string, string> GetPayload(string payload)
            {
                payload = payload.Trim();
                var retArgs = new Dictionary<string, string>();
                if (payload != null || payload != string.Empty)
                {
                    payload = payload.Replace('[', ' ');
                    payload = payload.Replace(']', ',');
                }
                payload = payload.Trim();
                string[] args = payload.Split(',');
                foreach (var arg in args)
                {
                    if (arg.Contains(':'))
                    {
                        string[] keyValue = arg.Split(':');
                        retArgs[keyValue[0].Trim()] = keyValue[1].Trim();
                    }
                }
                return retArgs;
            }
        }
    }
}
