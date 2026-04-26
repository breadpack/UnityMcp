using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace BreadPack.Mcp.Unity.Input
{
    public class DragHandler : IAsyncRequestHandler
    {
        public string ToolName => "unity_input_drag";

        public async Task<object> HandleAsync(JObject @params)
        {
            var fromObj = @params["from"] as JObject ?? throw new System.ArgumentException("'from' 필요");
            var toObj = @params["to"] as JObject ?? throw new System.ArgumentException("'to' 필요");
            var fromSpec = TargetSpec.Parse(fromObj);
            var toSpec = TargetSpec.Parse(toObj);

            var pointsArr = @params["points"] as JArray;
            var durationMs = @params["durationMs"]?.Value<int?>() ?? 200;
            var buttonStr = @params["button"]?.Value<string>() ?? "left";
            var opts = CommonOptions.Parse(@params);

            var button = buttonStr switch
            {
                "right" => MouseButton.Right,
                "middle" => MouseButton.Middle,
                _ => MouseButton.Left
            };

            InputSystemGuard.EnsurePlayMode();
            var fromR = TargetResolver.Resolve(fromSpec);
            var toR = TargetResolver.Resolve(toSpec);
            InputSystemGuard.EnsureReady(fromR.Kind);
            // from/to가 다른 Kind면 to에 대해서도 검증 (uGUI ↔ World 혼합 드래그)
            if (toR.Kind != fromR.Kind) InputSystemGuard.EnsureReady(toR.Kind);
            VirtualInputDevices.EnsureRegistered();

            // 경유점 빌드
            var path = new List<Vector2> { fromR.ScreenPoint };
            if (pointsArr != null)
            {
                foreach (var p in pointsArr)
                {
                    if (p is JObject po)
                    {
                        var ps = TargetSpec.Parse(po);
                        path.Add(TargetResolver.Resolve(ps).ScreenPoint);
                    }
                }
            }
            else
            {
                // duration / 16ms = 분할 수
                int steps = Mathf.Max(2, durationMs / 16);
                for (int i = 1; i < steps; i++)
                {
                    var t = (float)i / steps;
                    path.Add(Vector2.Lerp(fromR.ScreenPoint, toR.ScreenPoint, t));
                }
            }
            path.Add(toR.ScreenPoint);

            // 시퀀스
            InputInjector.MouseMove(path[0]);
            await MainThreadDispatcher.DelayFrames(1);
            InputInjector.MouseDown(button);
            await MainThreadDispatcher.DelayFrames(1);

            for (int i = 1; i < path.Count; i++)
            {
                InputInjector.MouseMove(path[i]);
                await MainThreadDispatcher.DelayFrames(1);
            }

            InputInjector.MouseUp(button);

            return await ResultSnapshot.CaptureAsync(opts, () =>
            {
                return new JObject
                {
                    ["type"] = "drag",
                    ["from"] = ClickHandler.BuildResolvedJson(fromR),
                    ["to"] = ClickHandler.BuildResolvedJson(toR),
                    ["pathLength"] = path.Count
                };
            });
        }
    }
}
