using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace BreadPack.Mcp.Unity.Input
{
    public class KeyHandler : IAsyncRequestHandler
    {
        public string ToolName => "unity_input_key";

        public async Task<object> HandleAsync(JObject @params)
        {
            var keyStr = @params["key"]?.Value<string>() ?? throw new System.ArgumentException("'key' 필요");
            var action = @params["action"]?.Value<string>() ?? "press";
            var opts = CommonOptions.Parse(@params);

            if (action != "press" && action != "down" && action != "up")
                throw new System.ArgumentException($"알 수 없는 action: {action}. press/down/up 중 하나여야 합니다.");

            var modifiers = ParseModifiers(@params["modifiers"] as JArray);

            var input = InputBackendRouter.Resolve(InputCapabilities.Keyboard);

            if (action == "press" || action == "down")
            {
                foreach (var modifier in modifiers) input.KeyDown(modifier);
                input.KeyDown(keyStr);
            }
            if (action == "press")
            {
                await MainThreadDispatcher.DelayFrames(1);
            }
            if (action == "press" || action == "up")
            {
                input.KeyUp(keyStr);
                if (action == "press")
                {
                    foreach (var modifier in modifiers) input.KeyUp(modifier);
                }
            }

            return await ResultSnapshot.CaptureAsync(opts, () =>
            {
                return InputBackendRouter.AddMetadata(new JObject
                {
                    ["type"] = "key",
                    ["key"] = keyStr,
                    ["action"] = action,
                    ["modifiers"] = new JArray(modifiers)
                }, input);
            });
        }

        private static List<string> ParseModifiers(JArray arr)
        {
            var list = new List<string>();
            if (arr == null) return list;
            foreach (var item in arr)
            {
                var s = item.Value<string>();
                if (string.IsNullOrEmpty(s)) continue;
                var modifier = s.ToLowerInvariant() switch
                {
                    "ctrl" or "control" => "LeftCtrl",
                    "shift" => "LeftShift",
                    "alt" => "LeftAlt",
                    "cmd" or "meta" or "win" => "LeftMeta",
                    _ => throw new System.ArgumentException($"알 수 없는 modifier: {s}. Ctrl/Shift/Alt/Cmd 중 하나여야 합니다.")
                };
                list.Add(modifier);
            }
            return list;
        }
    }
}
