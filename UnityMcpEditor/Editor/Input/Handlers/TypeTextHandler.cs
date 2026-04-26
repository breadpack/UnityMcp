using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace BreadPack.Mcp.Unity.Input
{
    public class TypeTextHandler : IAsyncRequestHandler
    {
        public string ToolName => "unity_input_type_text";

        public async Task<object> HandleAsync(JObject @params)
        {
            var text = @params["text"]?.Value<string>() ?? throw new System.ArgumentException("'text' 필요");
            var intervalMs = @params["intervalMs"]?.Value<int?>() ?? 20;
            var opts = CommonOptions.Parse(@params);

            InputSystemGuard.EnsurePlayMode();
            VirtualInputDevices.EnsureRegistered();

            int frameInterval = Mathf.Max(1, intervalMs / 16);

            foreach (var ch in text)
            {
                InputInjector.SendText(ch);
                await MainThreadDispatcher.DelayFrames(frameInterval);
            }

            return await ResultSnapshot.CaptureAsync(opts, () =>
            {
                return new JObject
                {
                    ["type"] = "type_text",
                    ["length"] = text.Length,
                    ["intervalMs"] = intervalMs
                };
            });
        }
    }
}
