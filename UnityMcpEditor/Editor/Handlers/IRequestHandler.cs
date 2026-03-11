using Newtonsoft.Json.Linq;

namespace BreadPack.Mcp.Unity
{
    public interface IRequestHandler
    {
        string ToolName { get; }
        object Handle(JObject @params);
    }
}
