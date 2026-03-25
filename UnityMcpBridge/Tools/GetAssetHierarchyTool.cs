using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace UnityMcpBridge.Tools;

[McpServerToolType]
public static class GetAssetHierarchyTool
{
    [McpServerTool(Name = "unity_get_asset_hierarchy"),
     Description("Prefab 또는 Scene 파일의 GameObject 계층구조를 조회합니다 (열려 있지 않은 파일도 가능)")]
    public static async Task<string> Execute(
        UnityConnection connection,
        [Description("Asset 경로 (예: Assets/Prefabs/Player.prefab)")] string? assetPath = null,
        [Description("Asset GUID")] string? assetGuid = null,
        [Description("최대 탐색 깊이")] int maxDepth = 5,
        [Description("컴포넌트 목록 포함 여부")] bool includeComponents = false,
        CancellationToken ct = default)
    {
        var paramDict = new Dictionary<string, object?>
        {
            ["maxDepth"] = maxDepth,
            ["includeComponents"] = includeComponents
        };
        if (assetPath != null) paramDict["assetPath"] = assetPath;
        if (assetGuid != null) paramDict["assetGuid"] = assetGuid;

        using var paramsJson = JsonDocument.Parse(JsonSerializer.Serialize(paramDict));
        var result = await connection.SendRequestAsync("unity_get_asset_hierarchy", paramsJson.RootElement, ct);
        return ResponseFormatter.Format(result);
    }
}
