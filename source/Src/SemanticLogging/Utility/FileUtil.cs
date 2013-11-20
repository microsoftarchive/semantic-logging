// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Security;
using System.Text.RegularExpressions;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Properties;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Utility
{
    internal class FileUtil
    {
        /// <summary>
        /// This method will replace environment variables and root the filename to the base directory while ensuring the path exists.
        /// </summary>
        /// <param name="fileName">The name of the file.</param>
        /// <returns>A FileInfo instance.</returns>
        public static FileInfo ProcessFileNameForLogging(string fileName)
        {
            ValidFile(fileName, "fileName");

            var s = ReplaceEnvironmentVariables(fileName);

            if (!string.Equals(fileName, s, StringComparison.OrdinalIgnoreCase))
            {
                ValidFile(s, "fileName", true);
            }

            return new FileInfo(RootFileNameAndEnsureTargetFolderExists(s));
        }

        /// <summary>
        /// Validate the file name or throws an exception.
        /// </summary>
        /// <param name="fileName">Name of the file.</param>
        /// <param name="argumentName">Name of argument being checked.</param>
        /// <param name="replaced"><see langword="true"/> if the file name is the result of replacing environment variables; otherwise, <see langword="false"/>.</param>
        /// <exception cref="System.ArgumentException">The file name is invalid.</exception>
        public static void ValidFile(string fileName, string argumentName = "fileName", bool replaced = false)
        {
            Guard.ArgumentNotNullOrEmpty(fileName, argumentName);

            // FileInfo will perform extra validations (chars, path length, etc)
            var file = new FileInfo(fileName);

            // Check for relative file path expansions and the actual file name.
            // Samples of invalid entries (without quotes):
            // ".", "..\..", "C:\Test\.."  
            if (string.IsNullOrWhiteSpace(file.Name) ||
                Path.GetFileName(file.FullName) != file.Name)
            {
                if (!replaced)
                {
                    throw new ArgumentException(Properties.Resources.InvalidNavigationPathInFileNameError);
                }
                else
                {
                    throw new ArgumentException(Properties.Resources.InvalidNavigationPathInReplacedFileNameError);
                }
            }
        }

        public static string CreateRandomFileName()
        {
            return Path.ChangeExtension(Path.GetRandomFileName(), ".log");
        }

        private static string RootFileNameAndEnsureTargetFolderExists(string fileName)
        {
            string rootedFileName = fileName;
            if (!Path.IsPathRooted(rootedFileName))
            {
                // GetFullPath will resolve any relative path in rootedFileName
                // AppDomain.CurrentDomain.BaseDirectory will be used as root to decouple from Environment.CurrentDirectory value.                
                rootedFileName = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, rootedFileName));
            }

            string directory = Path.GetDirectoryName(rootedFileName);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            return rootedFileName;
        }

        private static string ReplaceEnvironmentVariables(string fileName)
        {
            // Check EnvironmentPermission for the ability to access the environment variables.
            try
            {
                string variables = Environment.ExpandEnvironmentVariables(fileName);

                // If an Environment Variable is not found then remove any invalid tokens
                Regex filter = new Regex("%(.*?)%", RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace);

                string filePath = filter.Replace(variables, string.Empty);

                if (Path.GetDirectoryName(filePath) == null)
                {
                    filePath = Path.GetFileName(filePath);
                }

                return filePath;
            }
            catch (SecurityException)
            {
                throw new InvalidOperationException(Resources.ExceptionReadEnvironmentVariablesDenied);
            }
        }
    }
}
