using Newtonsoft.Json.Linq;

namespace UnityMcp.Editor
{
    public class McpRequest
    {
        public string Id { get; set; }
        public string Tool { get; set; }
        public JObject Params { get; set; }
    }
}
