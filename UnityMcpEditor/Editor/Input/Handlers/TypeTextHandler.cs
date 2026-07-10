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

            var input = InputBackendRouter.Resolve(InputCapabilities.Text);

            int frameInterval = Mathf.Max(1, intervalMs / 16);

            foreach (var ch in text)
            {
                // 1. 문자가 letter/digit이면 KeyDown/Up도 함께 송신.
                //    legacy InputField는 Event.current.character를 IMGUI 이벤트에서 읽으므로
                //    TextEvent만으로는 입력이 들어가지 않는 경우가 있음.
                var mappedKey = TryMapAsciiToKey(ch);
                bool needShift = char.IsUpper(ch);

                if (input.Delivery == "raw-device" && mappedKey != null)
                {
                    if (needShift) input.KeyDown("LeftShift");
                    input.KeyDown(mappedKey);
                }

                // 2. TextEvent — TMP_InputField 및 InputSystemUIInputModule 경로
                input.SendText(ch);

                if (input.Delivery == "raw-device" && mappedKey != null)
                {
                    input.KeyUp(mappedKey);
                    if (needShift) input.KeyUp("LeftShift");
                }

                await MainThreadDispatcher.DelayFrames(frameInterval);
            }

            return await ResultSnapshot.CaptureAsync(opts, () =>
            {
                return InputBackendRouter.AddMetadata(new JObject
                {
                    ["type"] = "type_text",
                    ["length"] = text.Length,
                    ["intervalMs"] = intervalMs
                }, input);
            });
        }

        // ASCII letter/digit만 매핑. 기호 등은 TextEvent로만 처리.
        private static string TryMapAsciiToKey(char ch)
        {
            char lower = char.ToLowerInvariant(ch);
            if (lower >= 'a' && lower <= 'z')
                return char.ToUpperInvariant(lower).ToString();
            if (ch >= '0' && ch <= '9')
                return "Digit" + ch;
            if (ch == ' ') return "Space";
            if (ch == '\n') return "Enter";
            if (ch == '\t') return "Tab";
            return null;
        }
    }
}
