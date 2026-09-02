using Newtonsoft.Json.Linq;

namespace BreadPack.Mcp.Unity
{
    public class SetAutoTickHandler : IRequestHandler
    {
        public string ToolName => "unity_set_autotick";

        public object Handle(JObject @params)
        {
            bool enable = @params["enable"]?.Value<bool>() ?? true;
            int intervalMs = @params["intervalMs"]?.Value<int>() ?? EditorAutoTick.DefaultIntervalMs;
            bool persist = @params["persist"]?.Value<bool>() ?? true;

            var (ok, message) = EditorAutoTick.Set(enable, intervalMs, persist);
            if (!ok) throw new System.InvalidOperationException(message);

            return new
            {
                enabled = EditorAutoTick.IsEnabled,
                intervalMs = EditorAutoTick.IntervalMs,
                persisted = persist,
                message
            };
        }
    }
}
