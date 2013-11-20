// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace ApplicationDemo
{
    public partial class MainWindow : Window
    {
        private int retryCount;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void InformationalClick(object sender, RoutedEventArgs e)
        {
            // simulate some work with a trace event being published.
            var customerId = new Random().Next();
            RxFlushQuickStartEventSource.Log.NewCustomerSelected(customerId);
        }

        private void WarningClick(object sender, RoutedEventArgs e)
        {
            // simulate some work with a warning event being published.
            var customerId = new Random().Next();
            RxFlushQuickStartEventSource.Log.TransientErrorWhileRefreshingCustomerData(customerId, ++this.retryCount);
        }

        private void ErrorClick(object sender, RoutedEventArgs e)
        {
            // simulate an unexpected failure.
            try
            {
                throw new UnauthorizedAccessException("User is not authorized to access customer information.");
            }
            catch (Exception ex)
            {
                RxFlushQuickStartEventSource.Log.UnknownError(ex.ToString());
            }
        }

        private void OpenLogFile(object sender, RoutedEventArgs e)
        {
            var logFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RxQuickStart-log.txt");
            var process = new Process();
            process.StartInfo = new ProcessStartInfo("notepad.exe", logFile);
            process.Start();
        }
    }
}
