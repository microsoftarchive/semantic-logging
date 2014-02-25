// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using Microsoft.Practices.EnterpriseLibrary.SemanticLogging;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Formatters;
using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.IO;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.InProc.Tests.TestObjects
{
    public class MockFormatter : IEventTextFormatter
    {
        private bool throwOnWrite;

        public MockFormatter()
            : this(false)
        {
        }

        public MockFormatter(bool throwOnWrite)
        {
            this.throwOnWrite = throwOnWrite;
        }

        public List<Tuple<EventEntry, TextWriter>> WriteEventCalls = new List<Tuple<EventEntry, TextWriter>>();

        public string Header { get; set; }
        public string Footer { get; set; }

        public void WriteEvent(EventEntry eventEntry, TextWriter writer)
        {
            if (throwOnWrite)
            {
                throw new InvalidOperationException(); 
            }

            this.WriteEventCalls.Add(Tuple.Create(eventEntry, writer));
            if (eventEntry.Payload.Count > 0)
            {
                writer.Write(eventEntry.Payload[0]); 
            }
        }

        public EventLevel Detailed { get; set; }

        public string DateTimeFormat { get; set; }
    }
}
