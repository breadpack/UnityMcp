namespace BreadPack.Mcp.Unity
{
    public class McpResponse
    {
        public string Id { get; set; }
        public bool Success { get; set; }
        public object Data { get; set; }
        public string Error { get; set; }
    }
}
