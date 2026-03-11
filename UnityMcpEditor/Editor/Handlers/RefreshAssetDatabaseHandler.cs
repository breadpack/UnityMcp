using Newtonsoft.Json.Linq;
using UnityEditor;

namespace UnityMcp.Editor
{
    public class RefreshAssetDatabaseHandler : IRequestHandler
    {
        public string ToolName => "unity_refresh_assets";

        public object Handle(JObject @params)
        {
            AssetDatabase.Refresh();
            return new
            {
                refreshed = true,
                autoRefreshMode = GetAutoRefreshModeName()
            };
        }

        internal static string GetAutoRefreshModeName()
        {
            int mode = EditorPrefs.GetInt("kAutoRefreshMode", 1);
            return mode switch
            {
                0 => "Disabled",
                1 => "Enabled",
                2 => "EnabledOutsidePlaymode",
                _ => $"Unknown({mode})"
            };
        }
    }
}
