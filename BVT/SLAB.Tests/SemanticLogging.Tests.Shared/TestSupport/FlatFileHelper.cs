// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.Shared.TestSupport
{
    public class FlatFileHelper
    {
        public static IEnumerable<T> PollUntilJsonEventsAreWritten<T>(string fileName, int eventsToRecieve)
        {
            return PollUntilEventsAreWritten(
                fileName,
                eventsToRecieve,
                (fileContents) => JsonConvert.DeserializeObject<T[]>("[" + fileContents + "]"));
        }

        public static IEnumerable<XElement> PollUntilXmlEventsAreWritten(string fileName, int eventsToRecieve)
        {
            return PollUntilEventsAreWritten(
                fileName,
                eventsToRecieve,
                (fileContents) => XDocument.Parse("<Events>" + fileContents + "</Events>").Root.Elements());
        }

        public static IEnumerable<string> PollUntilTextEventsAreWritten(string fileName, int eventsToRecieve, string header)
        {
            return PollUntilEventsAreWritten(
                fileName,
                eventsToRecieve,
                (fileContents) => Regex.Split(ReadFileWithoutLock(fileName), header).Where(c => !string.IsNullOrWhiteSpace(c)));
        }

        public static void DeleteDirectory(string name)
        {
            if (Directory.Exists(name))
            {
                Directory.Delete(name, true);
            }
        }

        public static string GetAllText(string fileName)
        {
            for (int n = 0; n < 10; n++)
            {
                if (File.Exists(fileName))
                {
                    Task.Delay(100).Wait();
                    return ReadFileWithoutLock(fileName);
                }

                Task.Delay(500).Wait();
            }

            return string.Empty;
        }

        public static IEnumerable<string> GetFileNames(string filePattern)
        {
            return Directory.EnumerateFiles(".", filePattern, SearchOption.TopDirectoryOnly);
        }

        public static string ReadFromFiles(string fileNamePattern)
        {
            string logMessages = null;
            IEnumerable<string> files = GetFileNames(fileNamePattern);
            foreach (var file in files)
            {
                logMessages += ReadFileWithoutLock(file);
            }

            return logMessages;
        }

        public static void PollUntilFilesAreCreated(string path, string fileNamePattern, int filesCount)
        {
            var timeoutToWaitUntilEventIsReceived = DateTime.UtcNow.AddSeconds(10);
            while (DateTime.UtcNow < timeoutToWaitUntilEventIsReceived)
            {
                try
                {
                    if (Directory.Exists(path))
                    {
                        if (GetFileNames(fileNamePattern).Count() == filesCount)
                        {
                            break;
                        }
                    }
                }
                catch
                { }

                Task.Delay(200).Wait();
            }
        }

        public static void DeleteCreatedLogFiles(string fileNameWithoutExtension)
        {
            try
            {
                foreach (string createdFileName in Directory.GetFiles(".", fileNameWithoutExtension + "*"))
                {
                    File.Delete(createdFileName);
                }
            }
            catch
            { }
        }

        private static IEnumerable<T> PollUntilEventsAreWritten<T>(string fileName, int eventsToRecieve, Func<string, IEnumerable<T>> deserializeString)
        {
            IEnumerable<T> entries = new T[0];
            var timeoutToWaitUntilEventIsReceived = DateTime.UtcNow.AddSeconds(10);
            while (DateTime.UtcNow < timeoutToWaitUntilEventIsReceived)
            {
                if (File.Exists(fileName))
                {
                    try
                    {
                        var fileContents = ReadFileWithoutLock(fileName);
                        entries = deserializeString(fileContents);
                        if (entries.Count() == eventsToRecieve)
                        {
                            break;
                        }
                    }
                    catch
                    { }
                }

                Task.Delay(200).Wait();
            }

            return entries;
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
