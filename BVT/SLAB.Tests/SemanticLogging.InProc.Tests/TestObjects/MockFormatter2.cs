// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using Microsoft.Practices.EnterpriseLibrary.SemanticLogging;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Formatters;
using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.IO;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.InProc.Tests.TestObjects
{
    public class MockFormatter2 : IEventTextFormatter
    {
        private bool throwOnWrite;

        public MockFormatter2()
            : this(false)
        {
        }

        public MockFormatter2(bool throwOnWrite)
        {
            this.throwOnWrite = throwOnWrite;
        }

        public List<Tuple<EventEntry, TextWriter>> WriteEventCalls = new List<Tuple<EventEntry, TextWriter>>();

        public string Header { get; set; }
        public string Footer { get; set; }

        public void WriteEvent(EventEntry eventEntry, TextWriter writer)
        {
            writer.Close();
            writer.Write(eventEntry.FormattedMessage ?? (object)eventEntry.Payload);
        }

        public EventLevel Detailed { get; set; }

        public string DateTimeFormat { get; set; }
    }
}
