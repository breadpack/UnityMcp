using System;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace BreadPack.Mcp.Unity
{
    public class TakeScreenshotHandler : IAsyncRequestHandler
    {
        public string ToolName => "unity_take_screenshot";

        public async Task<object> HandleAsync(JObject @params)
        {
            int quality = @params?["quality"]?.Value<int>() ?? 75;
            int maxWidth = @params?["maxWidth"]?.Value<int>() ?? 0;
            return await GameViewCaptureService.CaptureEncodedAsync(quality, maxWidth);
        }
    }
}
