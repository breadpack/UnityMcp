using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace BreadPack.Mcp.Unity.Input
{
    public class ClickHandler : IAsyncRequestHandler
    {
        public string ToolName => "unity_input_click";

        public async Task<object> HandleAsync(JObject @params)
        {
            var ts = TargetSpec.Parse(@params);
            var opts = CommonOptions.Parse(@params);

            var buttonStr = @params["button"]?.Value<string>() ?? "left";
            var count = @params["count"]?.Value<int?>() ?? 1;

            var button = buttonStr switch
            {
                "right" => McpMouseButton.Right,
                "middle" => McpMouseButton.Middle,
                _ => McpMouseButton.Left
            };

            InputSystemGuard.EnsurePlayMode();
            var resolved = TargetResolver.Resolve(ts);
            var input = InputBackendRouter.Resolve(InputCapabilities.Pointer, resolved);

            // 시퀀스: Move → (Down → Up) × count
            input.PointerMove(resolved.ScreenPoint);
            await MainThreadDispatcher.DelayFrames(1);
            for (int i = 0; i < count; i++)
            {
                input.PointerDown(button);
                await MainThreadDispatcher.DelayFrames(1);
                input.PointerUp(button);
                if (i < count - 1) await MainThreadDispatcher.DelayFrames(1);
            }

            return await ResultSnapshot.CaptureAsync(opts,
                () => InputBackendRouter.AddMetadata(BuildResolvedJson(resolved), input));
        }

        // spec §6.4: ugui | uitk | world | screen
        private static string KindLabel(TargetKind k) => k switch
        {
            TargetKind.UGui => "ugui",
            TargetKind.UiToolkit => "uitk",
            TargetKind.World => "world",
            TargetKind.Screen => "screen",
            _ => k.ToString().ToLowerInvariant()
        };

        internal static JObject BuildResolvedJson(ResolvedTarget r)
        {
            return new JObject
            {
                ["type"] = KindLabel(r.Kind),
                ["path"] = r.ResolvedPath,
                ["screen"] = new JObject
                {
                    ["x"] = r.ScreenPoint.x,
                    ["y"] = r.ScreenPoint.y
                }
            };
        }
    }
}
