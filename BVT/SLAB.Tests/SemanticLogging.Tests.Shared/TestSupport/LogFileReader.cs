// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.Shared.TestSupport
{
    public static class LogFileReader
    {
        public static List<Dictionary<string, string>> GetEntries(string fileName)
        {
            List<Dictionary<string, string>> fileContents = new List<Dictionary<string, string>>();
            using (var reader = new StreamReader(new FileStream(fileName, FileMode.Open, FileAccess.Read)))
            {
                int index = 0;
                while (!reader.EndOfStream)
                {
                    string line = reader.ReadLine();
                    if (String.IsNullOrEmpty(line.Trim()))
                    {
                        continue; 
                    }
                    string[] keyValue = null;
                    if (!line.Contains("Payload :"))
                    {
                        keyValue = line.Split(':');
                    }
                    else
                    {
                        keyValue = new string[2];
                        keyValue[0] = "Payload";
                        keyValue[1] = line.Substring(9, line.Length - 9).Trim();
                    }

                    keyValue[0] = keyValue[0].Trim();
                    keyValue[1] = keyValue[1].Trim();
                    if (keyValue[0] == "ProviderId")
                    {
                        fileContents.Add(new Dictionary<string, string>());
                        index++;
                    }

                    fileContents[index - 1][keyValue[0]] = keyValue[1];
                }
            }

            return fileContents;
        }

        public static string ReadFileWithoutLock(string fileName)
        {
            using (var reader = new StreamReader(new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
            {
                return reader.ReadToEnd();
            }
        }
    }
}
