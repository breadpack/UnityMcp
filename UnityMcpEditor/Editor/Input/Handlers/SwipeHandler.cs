using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace BreadPack.Mcp.Unity.Input
{
    public class SwipeHandler : IAsyncRequestHandler
    {
        public string ToolName => "unity_input_swipe";

        public async Task<object> HandleAsync(JObject @params)
        {
            var fromObj = @params["from"] as JObject ?? throw new System.ArgumentException("'from' 필요");
            var fromSpec = TargetSpec.Parse(fromObj);

            var direction = @params["direction"]?.Value<string>()?.ToLowerInvariant() ?? "right";
            var distance = @params["distance"]?.Value<float?>() ?? 200f;
            var durationMs = @params["durationMs"]?.Value<int?>() ?? 150;
            var opts = CommonOptions.Parse(@params);

            Vector2 dirVec = direction switch
            {
                "up" => Vector2.up,
                "down" => Vector2.down,
                "left" => Vector2.left,
                "right" => Vector2.right,
                _ => throw new System.ArgumentException($"알 수 없는 direction: {direction}. up/down/left/right 중 하나여야 합니다.")
            };

            InputSystemGuard.EnsurePlayMode();
            var fromR = TargetResolver.Resolve(fromSpec);
            InputSystemGuard.EnsureReady(fromR.Kind);
            VirtualInputDevices.EnsureRegistered();

            var toPoint = fromR.ScreenPoint + dirVec * distance;

            int steps = Mathf.Max(2, durationMs / 16);
            var path = new List<Vector2> { fromR.ScreenPoint };
            for (int i = 1; i < steps; i++)
            {
                var t = (float)i / steps;
                path.Add(Vector2.Lerp(fromR.ScreenPoint, toPoint, t));
            }
            path.Add(toPoint);

            InputInjector.MouseMove(path[0]);
            await MainThreadDispatcher.DelayFrames(1);
            InputInjector.MouseDown(MouseButton.Left);
            await MainThreadDispatcher.DelayFrames(1);

            for (int i = 1; i < path.Count; i++)
            {
                InputInjector.MouseMove(path[i]);
                await MainThreadDispatcher.DelayFrames(1);
            }

            InputInjector.MouseUp(MouseButton.Left);

            return await ResultSnapshot.CaptureAsync(opts, () =>
            {
                return new JObject
                {
                    ["type"] = "swipe",
                    ["from"] = ClickHandler.BuildResolvedJson(fromR),
                    ["direction"] = direction,
                    ["distance"] = distance,
                    ["to"] = new JObject { ["x"] = toPoint.x, ["y"] = toPoint.y },
                    ["pathLength"] = path.Count
                };
            });
        }
    }
}
