// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.IO;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.OutProc.Tests.TestObjects
{
    public class MockConsoleOutput : IDisposable
    {
        private TextWriter writer;
        private TextWriter originalOutput;
        private bool disposed;

        public MockConsoleOutput()
        {
            writer = new StringWriter();
            originalOutput = Console.Out;
            Console.SetOut(writer);
        }

        public string Ouput
        {
            get { return writer.ToString(); }
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
                }

                this.writer = null;
                this.disposed = true;
            }
        }
    }
}
