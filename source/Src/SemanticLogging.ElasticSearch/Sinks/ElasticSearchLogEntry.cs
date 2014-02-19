using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Utility;
using Newtonsoft.Json;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Sinks
{
    [JsonConverter(typeof(ElasticSearchConverter))]
    public class ElasticSearchLogEntry
    {
        public JsonEventEntry LogEntry { get; set; }
        public string Type { get; set; }
        public string Index { get; set; }
    }
}
