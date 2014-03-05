using System.Collections.Generic;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.TestObjects
{
    public class MockHttpListenerResponse
    {
        public MockHttpListenerResponse()
        {
            this.Headers = new List<string>();
        }

        public int ResponseCode { get; set; }

        public string Content { get; set; }

        public string ContentType { get; set; }

        public List<string> Headers { get; set; }
    }
}
