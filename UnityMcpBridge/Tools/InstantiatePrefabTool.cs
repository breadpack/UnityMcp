using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace UnityMcpBridge.Tools;

[McpServerToolType]
public static class InstantiatePrefabTool
{
    [McpServerTool(Name = "unity_instantiate_prefab"),
     Description("Prefab을 Scene에 인스턴스화합니다")]
    public static async Task<string> Execute(
        UnityConnection connection,
        [Description("Prefab Asset 경로")] string? assetPath = null,
        [Description("Prefab Asset GUID")] string? assetGuid = null,
        [Description("부모 경로")] string? parentPath = null,
        [Description("부모 InstanceID")] int? parentId = null,
        [Description("인스턴스 이름 (미지정 시 Prefab 이름 사용)")] string? name = null,
        CancellationToken ct = default)
    {
        var paramDict = new Dictionary<string, object?>();
        if (assetPath != null) paramDict["assetPath"] = assetPath;
        if (assetGuid != null) paramDict["assetGuid"] = assetGuid;
        if (parentPath != null) paramDict["parentPath"] = parentPath;
        if (parentId != null) paramDict["parentId"] = parentId;
        if (name != null) paramDict["name"] = name;

        using var paramsJson = JsonDocument.Parse(JsonSerializer.Serialize(paramDict));
        var result = await connection.SendRequestAsync("unity_instantiate_prefab", paramsJson.RootElement, ct);
        var root = result.RootElement;
        if (root.TryGetProperty("success", out var s) && !s.GetBoolean())
            return $"Error: {root.GetProperty("error").GetString()}";
        return root.GetProperty("data").GetRawText();
    }
}
