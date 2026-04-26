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
            var buttonStr = @params["button"]?.Value<string>() ?? "left";
            var button = buttonStr switch
            {
                "right" => MouseButton.Right,
                "middle" => MouseButton.Middle,
                _ => MouseButton.Left
            };

            InputSystemGuard.EnsurePlayMode();
            var resolved = TargetResolver.Resolve(ts);
            InputSystemGuard.EnsureReady(resolved.Kind);
            VirtualInputDevices.EnsureRegistered();

            InputInjector.MouseMove(resolved.ScreenPoint);
            await MainThreadDispatcher.DelayFrames(1);
            InputInjector.MouseDown(button);

            int frames = Mathf.Max(1, holdMs / 16);
            await MainThreadDispatcher.DelayFrames(frames);

            InputInjector.MouseUp(button);

            return await ResultSnapshot.CaptureAsync(opts, () =>
            {
                var json = ClickHandler.BuildResolvedJson(resolved);
                json["holdMs"] = holdMs;
                return json;
            });
        }
    }
}
