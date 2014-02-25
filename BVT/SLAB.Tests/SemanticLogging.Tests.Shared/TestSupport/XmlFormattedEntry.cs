// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System.Linq;
using System.Xml.Linq;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.Shared.TestSupport
{
    public class XmlFormattedEntry
    {
        private const string EventNS = "{http://schemas.microsoft.com/win/2004/08/events/event}";

        public static XElement Provider { get; set; }
        public static XElement EventId { get; set; }
        public static XElement Version { get; set; }
        public static XElement Level { get; set; }
        public static XElement Task { get; set; }
        public static XElement Opcode { get; set; }
        public static XElement Keywords { get; set; }
        public static XElement TimeCreated { get; set; }
        public static XElement Payload { get; set; }
        public static XElement Message { get; set; }
        public static void Fill(XElement entry)
        {
            Provider = entry.Descendants(EventNS + "Provider").Single();
            EventId = entry.Descendants(EventNS + "EventID").Single();
            Version = entry.Descendants(EventNS + "Version").Single();
            Level = entry.Descendants(EventNS + "Level").Single();
            Task = entry.Descendants(EventNS + "Task").Single();
            Opcode = entry.Descendants(EventNS + "Opcode").Single();
            Keywords = entry.Descendants(EventNS + "Keywords").Single();
            TimeCreated = entry.Descendants(EventNS + "TimeCreated").Single();
            Payload = entry.Descendants(EventNS + "EventData").Single();
            Message = entry.Descendants(EventNS + "RenderingInfo").Single();
        }
    }
}
