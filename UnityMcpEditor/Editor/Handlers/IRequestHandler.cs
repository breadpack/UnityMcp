using Newtonsoft.Json.Linq;

namespace UnityMcp.Editor
{
    public interface IRequestHandler
    {
        string ToolName { get; }
        object Handle(JObject @params);
    }
}
