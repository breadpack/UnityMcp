using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace UnityMcpBridge.Tools;

[McpServerToolType]
public static class ReparentGameObjectTool
{
    [McpServerTool(Name = "unity_reparent_gameobject"),
     Description("GameObject의 부모를 변경합니다")]
    public static async Task<string> Execute(
        UnityConnection connection,
        [Description("대상 경로")] string? path = null,
        [Description("대상 InstanceID")] int? instanceId = null,
        [Description("새 부모 경로 (null이면 루트로 이동)")] string? newParentPath = null,
        [Description("새 부모 InstanceID")] int? newParentId = null,
        [Description("월드 좌표 유지 여부")] bool worldPositionStays = true,
        CancellationToken ct = default)
    {
        var paramDict = new Dictionary<string, object?>();
        if (path != null) paramDict["path"] = path;
        if (instanceId != null) paramDict["instanceId"] = instanceId;
        if (newParentPath != null) paramDict["newParentPath"] = newParentPath;
        if (newParentId != null) paramDict["newParentId"] = newParentId;
        paramDict["worldPositionStays"] = worldPositionStays;

        using var paramsJson = JsonDocument.Parse(JsonSerializer.Serialize(paramDict));
        var result = await connection.SendRequestAsync("unity_reparent_gameobject", paramsJson.RootElement, ct);
        return ResponseFormatter.Format(result);
    }
}
