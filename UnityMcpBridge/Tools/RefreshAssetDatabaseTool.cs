using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace UnityMcpBridge.Tools;

[McpServerToolType]
public static class RefreshAssetDatabaseTool
{
    [McpServerTool(Name = "unity_refresh_assets"), Description("Unity AssetDatabase를 강제로 Refresh합니다. Auto Refresh가 비활성화된 경우 수동으로 에셋 변경을 반영할 때 사용합니다")]
    public static async Task<string> Execute(
        UnityConnection connection,
        CancellationToken ct = default)
    {
        var result = await connection.SendRequestAsync("unity_refresh_assets", ct: ct);

        return ResponseFormatter.Format(result);
    }
}
