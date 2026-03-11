using Newtonsoft.Json.Linq;

namespace BreadPack.Mcp.Unity
{
    public class McpRequest
    {
        public string Id { get; set; }
        public string Tool { get; set; }
        public JObject Params { get; set; }
    }
}
