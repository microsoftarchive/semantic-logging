// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Xml.Linq;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw.Configuration;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Formatters;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.Etw.Configuration
{
    public class MySinkElement : ISinkElement
    {
        private readonly XName sinkName = XName.Get("mySink", "urn:test");

        public bool CanCreateSink(XElement element)
        {
            return element.Name == this.sinkName;
        }

        public IObserver<EventEntry> CreateSink(XElement element)
        {
            var sink = new MySink(FormatterElementFactory.Get(element));
            MySink.Instance = sink;
            return sink;
        }
    }

    public class MyNoSchemaSinkElement : ISinkElement
    {
        private readonly XName sinkName = XName.Get("mySink", "urn:no_schema");

        public bool CanCreateSink(XElement element)
        {
            return element.Name == this.sinkName;
        }

        public IObserver<EventEntry> CreateSink(XElement element)
        {
            var sink = new MySink(FormatterElementFactory.Get(element));
            MySink.Instance = sink;
            return sink;
        }
    }

    public class MySink : IObserver<EventEntry>
    {
        public static MySink Instance { get; set; }

        public MySink(IEventTextFormatter formatter)
        {
            this.Formatter = formatter;
        }

        public IEventTextFormatter Formatter { get; set; }

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void OnNext(EventEntry value)
        {
        }
    }
}
