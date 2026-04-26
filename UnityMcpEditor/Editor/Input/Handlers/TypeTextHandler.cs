using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

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
                // 1. 문자가 letter/digit이면 KeyDown/Up도 함께 송신.
                //    legacy InputField는 Event.current.character를 IMGUI 이벤트에서 읽으므로
                //    TextEvent만으로는 입력이 들어가지 않는 경우가 있음.
                Key? mappedKey = TryMapAsciiToKey(ch);
                bool needShift = char.IsUpper(ch);

                if (mappedKey.HasValue)
                {
                    if (needShift) InputInjector.KeyDown(Key.LeftShift);
                    InputInjector.KeyDown(mappedKey.Value);
                }

                // 2. TextEvent — TMP_InputField 및 InputSystemUIInputModule 경로
                InputInjector.SendText(ch);

                if (mappedKey.HasValue)
                {
                    InputInjector.KeyUp(mappedKey.Value);
                    if (needShift) InputInjector.KeyUp(Key.LeftShift);
                }

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

        // ASCII letter/digit만 매핑. 기호 등은 TextEvent로만 처리.
        private static Key? TryMapAsciiToKey(char ch)
        {
            char lower = char.ToLowerInvariant(ch);
            if (lower >= 'a' && lower <= 'z')
                return (Key)((int)Key.A + (lower - 'a'));
            if (ch >= '0' && ch <= '9')
                return (Key)((int)Key.Digit0 + (ch - '0'));
            if (ch == ' ') return Key.Space;
            if (ch == '\n') return Key.Enter;
            if (ch == '\t') return Key.Tab;
            return null;
        }
    }
}
