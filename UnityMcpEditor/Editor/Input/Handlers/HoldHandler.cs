using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace BreadPack.Mcp.Unity.Input
{
    public class HoldHandler : IAsyncRequestHandler
    {
        public string ToolName => "unity_input_hold";

        public async Task<object> HandleAsync(JObject @params)
        {
            var ts = TargetSpec.Parse(@params);
            var opts = CommonOptions.Parse(@params);

            var holdMs = @params["holdMs"]?.Value<int?>() ?? 500;
            if (holdMs <= 0)
                throw new System.ArgumentException($"holdMs는 1 이상이어야 합니다. (받은 값: {holdMs}). 단순 클릭은 unity_input_click을 사용하세요.");
            var buttonStr = @params["button"]?.Value<string>() ?? "left";
            var button = buttonStr switch
            {
                "right" => McpMouseButton.Right,
                "middle" => McpMouseButton.Middle,
                _ => McpMouseButton.Left
            };

            InputSystemGuard.EnsurePlayMode();
            var resolved = TargetResolver.Resolve(ts);
            var input = InputBackendRouter.Resolve(InputCapabilities.Pointer, resolved);

            input.PointerMove(resolved.ScreenPoint);
            await MainThreadDispatcher.DelayFrames(1);
            input.PointerDown(button);

            int frames = Mathf.Max(1, holdMs / 16);
            await MainThreadDispatcher.DelayFrames(frames);

            input.PointerUp(button);

            return await ResultSnapshot.CaptureAsync(opts, () =>
            {
                var json = ClickHandler.BuildResolvedJson(resolved);
                json["holdMs"] = holdMs;
                return InputBackendRouter.AddMetadata(json, input);
            });
        }
    }
}
