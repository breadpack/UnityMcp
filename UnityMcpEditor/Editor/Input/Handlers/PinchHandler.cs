using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;

namespace BreadPack.Mcp.Unity.Input
{
    public class PinchHandler : IAsyncRequestHandler
    {
        public string ToolName => "unity_input_pinch";

        public async Task<object> HandleAsync(JObject @params)
        {
            var centerObj = @params["center"] as JObject ?? throw new System.ArgumentException("'center' 필요");
            var centerSpec = TargetSpec.Parse(centerObj);

            var startSpread = @params["startSpread"]?.Value<float?>() ?? 100f;
            var endSpread = @params["endSpread"]?.Value<float?>() ?? 300f;
            var durationMs = @params["durationMs"]?.Value<int?>() ?? 300;
            var opts = CommonOptions.Parse(@params);

            InputSystemGuard.EnsurePlayMode();
            var center = TargetResolver.Resolve(centerSpec);
            InputSystemGuard.EnsureReady(center.Kind);
            VirtualInputDevices.EnsureRegistered();

            int steps = Mathf.Max(2, durationMs / 16);
            const int touchId0 = 1;
            const int touchId1 = 2;

            Vector2 finger0Start = center.ScreenPoint + Vector2.left * (startSpread / 2f);
            Vector2 finger1Start = center.ScreenPoint + Vector2.right * (startSpread / 2f);
            Vector2 finger0End = center.ScreenPoint + Vector2.left * (endSpread / 2f);
            Vector2 finger1End = center.ScreenPoint + Vector2.right * (endSpread / 2f);

            // 시작~종료 전 단계에서 예외가 발생해도 두 finger를 반드시 Ended로 닫아
            // 다음 입력 호출에 잔존 터치 상태가 누수되지 않도록 try/finally 보호.
            bool fingersBegan = false;
            try
            {
                InputInjector.TouchSet(0, finger0Start, TouchPhase.Began, touchId0);
                InputInjector.TouchSet(1, finger1Start, TouchPhase.Began, touchId1);
                fingersBegan = true;
                await MainThreadDispatcher.DelayFrames(1);

                for (int i = 1; i < steps; i++)
                {
                    float t = (float)i / steps;
                    float spread = Mathf.Lerp(startSpread, endSpread, t);
                    Vector2 finger0 = center.ScreenPoint + Vector2.left * (spread / 2f);
                    Vector2 finger1 = center.ScreenPoint + Vector2.right * (spread / 2f);

                    InputInjector.TouchSet(0, finger0, TouchPhase.Moved, touchId0);
                    InputInjector.TouchSet(1, finger1, TouchPhase.Moved, touchId1);
                    await MainThreadDispatcher.DelayFrames(1);
                }

                InputInjector.TouchSet(0, finger0End, TouchPhase.Ended, touchId0);
                InputInjector.TouchSet(1, finger1End, TouchPhase.Ended, touchId1);
                fingersBegan = false;
            }
            finally
            {
                if (fingersBegan)
                {
                    // 정상 종료 경로(Ended)가 실행되지 않은 경우만 Canceled로 정리
                    try { InputInjector.TouchSet(0, finger0End, TouchPhase.Canceled, touchId0); } catch { }
                    try { InputInjector.TouchSet(1, finger1End, TouchPhase.Canceled, touchId1); } catch { }
                }
            }

            return await ResultSnapshot.CaptureAsync(opts, () =>
            {
                var json = ClickHandler.BuildResolvedJson(center);
                json["type"] = "pinch";
                json["startSpread"] = startSpread;
                json["endSpread"] = endSpread;
                json["steps"] = steps;
                return json;
            });
        }
    }
}
