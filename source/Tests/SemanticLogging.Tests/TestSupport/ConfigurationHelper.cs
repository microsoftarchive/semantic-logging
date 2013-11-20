// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System.Configuration;
using Microsoft.Win32;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.TestSupport
{
    internal class ConfigurationHelper
    {
        public static string GetSetting(string settingName)
        {
            string value = null;
            using (var subKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\EntLib") ?? Registry.CurrentUser)
            {
                var keyValue = subKey.GetValue(settingName);
                if (keyValue != null)
                {
                    value = keyValue.ToString();
                }
            }

            if (string.IsNullOrEmpty(value))
            {
                value = ConfigurationManager.AppSettings[settingName];
            }

            return value;
        }
    }
}
