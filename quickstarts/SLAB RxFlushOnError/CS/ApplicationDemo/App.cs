// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Diagnostics.Tracing;
using System.Windows;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging;

namespace ApplicationDemo
{
    public class App : Application
    {
        private ObservableEventListener listener;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            this.listener = new ObservableEventListener();

            // Note: Enable Informational messages (and not just errors), even though not all of them will be flushed to the log file.
            listener.EnableEvents(RxFlushQuickStartEventSource.Log, EventLevel.Informational);

            // FlushOnTrigger is a custom extension method that shows how you can leverage the power of Reactive Extensions (Rx) 
            // to perform filtering (or transformation) of the event stream before it is sent to the underlying sink.
            // In this case, FlushOnTrigger buffers the last 3 event entries that are > EventLevel.Error (such as informational 
            // entries), and only if an error occurs afterwards, then these informational entries are flushed to the sink,
            // so the admin has some additional diagnostics information, without the need of logging absolutely every message
            // even when the application is behaving correctly.

            // Note: For basic scenarios, you DO NOT need to use Rx, and SLAB does not depend on it.

            listener
                .FlushOnEventLevel(EventLevel.Error, bufferSize: 3)
                .LogToFlatFile("RxQuickStart-log.txt");
        }

        protected override void OnExit(ExitEventArgs e)
        {
            base.OnExit(e);
            
            listener.Dispose();
        }


        [STAThread]
        public static void Main()
        {
            App app = new App();
            app.StartupUri = new Uri("MainWindow.xaml", UriKind.Relative);
            app.Run();
        }
    }
}
