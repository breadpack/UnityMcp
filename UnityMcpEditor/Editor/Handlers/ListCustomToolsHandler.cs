using System.Linq;
using Newtonsoft.Json.Linq;

namespace BreadPack.Mcp.Unity
{
    public class ListCustomToolsHandler : IRequestHandler
    {
        public string ToolName => "unity_list_custom_tools";

        public object Handle(JObject @params)
        {
            var tools = CustomToolRegistry.Tools.Values.Select(t => new
            {
                name = t.Name,
                description = t.Description,
                parameters = t.GetParameterList()
            }).ToArray();

            return new
            {
                count = tools.Length,
                tools
            };
        }
    }
}
