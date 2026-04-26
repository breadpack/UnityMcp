using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace BreadPack.Mcp.Unity.Input
{
    public class ScrollHandler : IAsyncRequestHandler
    {
        public string ToolName => "unity_input_scroll";

        public async Task<object> HandleAsync(JObject @params)
        {
            var ts = TargetSpec.Parse(@params);
            var opts = CommonOptions.Parse(@params);

            var dx = @params["dx"]?.Value<float?>() ?? 0f;
            var dy = @params["dy"]?.Value<float?>() ?? 100f;

            InputSystemGuard.EnsurePlayMode();
            var resolved = TargetResolver.Resolve(ts);
            InputSystemGuard.EnsureReady(resolved.Kind);
            VirtualInputDevices.EnsureRegistered();

            InputInjector.MouseMove(resolved.ScreenPoint);
            await MainThreadDispatcher.DelayFrames(1);
            InputInjector.MouseScroll(new Vector2(dx, dy));
            await MainThreadDispatcher.DelayFrames(1);
            InputInjector.MouseScroll(Vector2.zero);

            return await ResultSnapshot.CaptureAsync(opts, () =>
            {
                var json = ClickHandler.BuildResolvedJson(resolved);
                json["dx"] = dx;
                json["dy"] = dy;
                return json;
            });
        }
    }
}
