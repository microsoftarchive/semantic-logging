// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.IO;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.InProc.Tests.TestObjects
{
    public class MockConsoleOutputInterceptor : IDisposable
    {
        private ConsoleWriter writer;
        private TextWriter originalOutput;
        private bool disposed;

        public MockConsoleOutputInterceptor()
        {
            writer = new ConsoleWriter();
            originalOutput = Console.Out;
            Console.SetOut(writer);
        }

        public string Ouput
        {
            get { return writer.ToString(); }
        }

        public ConsoleColor OutputForegroundColor
        {
            get { return writer.ForegroundColor; }
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                Console.SetOut(originalOutput);
                if (disposing)
                {
                    if (this.writer != null)
                    {
                        this.writer.Dispose();
                    }

                    if (this.originalOutput != null)
                    {
                        this.originalOutput.Dispose();
                    }
                }

                this.writer = null;
                this.originalOutput = null;

                this.disposed = true;
            }
        }

        private class ConsoleWriter : StringWriter
        {
            public ConsoleColor ForegroundColor { get; private set; }

            public override void Flush()
            {
                base.Flush();
                this.ForegroundColor = Console.ForegroundColor;
            }
        }
    }
}
