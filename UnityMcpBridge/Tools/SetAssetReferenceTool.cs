using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace UnityMcpBridge.Tools;

[McpServerToolType]
public static class SetAssetReferenceTool
{
    [McpServerTool(Name = "unity_set_asset_reference"),
     Description("Component의 필드에 Asset 참조(Material, Sprite, PanelSettings 등)를 설정합니다")]
    public static async Task<string> Execute(
        UnityConnection connection,
        [Description("Component 타입")] string componentType,
        [Description("프로퍼티/필드 이름")] string propertyName,
        [Description("대상 경로")] string? path = null,
        [Description("대상 InstanceID")] int? instanceId = null,
        [Description("Asset 경로")] string? assetPath = null,
        [Description("Asset GUID")] string? assetGuid = null,
        [Description("같은 타입 컴포넌트 인덱스")] int index = 0,
        CancellationToken ct = default)
    {
        var paramDict = new Dictionary<string, object?>
        {
            ["componentType"] = componentType,
            ["propertyName"] = propertyName,
            ["index"] = index
        };
        if (path != null) paramDict["path"] = path;
        if (instanceId != null) paramDict["instanceId"] = instanceId;
        if (assetPath != null) paramDict["assetPath"] = assetPath;
        if (assetGuid != null) paramDict["assetGuid"] = assetGuid;

        using var paramsJson = JsonDocument.Parse(JsonSerializer.Serialize(paramDict));
        var result = await connection.SendRequestAsync("unity_set_asset_reference", paramsJson.RootElement, ct);
        return ResponseFormatter.Format(result);
    }
}
