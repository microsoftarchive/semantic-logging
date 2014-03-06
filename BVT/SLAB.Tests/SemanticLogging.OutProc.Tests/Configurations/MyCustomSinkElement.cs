using System;
using System.Xml.Linq;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Configuration;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw.Configuration;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Formatters;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.Mocks
{
    public class MyCustomSinkElement : ISinkElement
    {
        private readonly XName sinkName = XName.Get("mySink", "urn:test");

        public bool CanCreateSink(XElement element)
        {
            return element.Name == this.sinkName;
        }

        public IObserver<EventEntry> CreateSink(XElement element)
        {
            var sink = new MyCustomSink(FormatterElementFactory.Get(element));
            MyCustomSink.Instance = sink;
            return sink;
        }
    }

    public class MyCustomSink : IObserver<EventEntry>
    {
        public static MyCustomSink Instance { get; set; }

        public MyCustomSink(IEventTextFormatter formatter)
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
