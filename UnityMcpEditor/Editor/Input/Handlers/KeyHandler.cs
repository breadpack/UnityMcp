using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine.InputSystem;

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

            if (!System.Enum.TryParse<Key>(keyStr, ignoreCase: true, out var key))
                throw new System.ArgumentException($"알 수 없는 key: {keyStr}. UnityEngine.InputSystem.Key 열거자 이름을 사용하세요 (예: Enter, Escape, A, Digit1).");

            var modifiers = ParseModifiers(@params["modifiers"] as JArray);

            InputSystemGuard.EnsurePlayMode();
            VirtualInputDevices.EnsureRegistered();

            if (action == "press" || action == "down")
            {
                foreach (var m in modifiers) InputInjector.KeyDown(m);
                InputInjector.KeyDown(key);
            }
            if (action == "press")
            {
                await MainThreadDispatcher.DelayFrames(1);
            }
            if (action == "press" || action == "up")
            {
                InputInjector.KeyUp(key);
                if (action == "press")
                {
                    foreach (var m in modifiers) InputInjector.KeyUp(m);
                }
            }

            return await ResultSnapshot.CaptureAsync(opts, () =>
            {
                return new JObject
                {
                    ["type"] = "key",
                    ["key"] = key.ToString(),
                    ["action"] = action,
                    ["modifiers"] = new JArray(System.Linq.Enumerable.Select(modifiers, m => (object)m.ToString()))
                };
            });
        }

        private static List<Key> ParseModifiers(JArray arr)
        {
            var list = new List<Key>();
            if (arr == null) return list;
            foreach (var item in arr)
            {
                var s = item.Value<string>();
                if (string.IsNullOrEmpty(s)) continue;
                Key m = s.ToLowerInvariant() switch
                {
                    "ctrl" or "control" => Key.LeftCtrl,
                    "shift" => Key.LeftShift,
                    "alt" => Key.LeftAlt,
                    "cmd" or "meta" or "win" => Key.LeftMeta,
                    _ => throw new System.ArgumentException($"알 수 없는 modifier: {s}. Ctrl/Shift/Alt/Cmd 중 하나여야 합니다.")
                };
                list.Add(m);
            }
            return list;
        }
    }
}
