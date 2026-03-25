using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace UnityMcpBridge.Tools;

[McpServerToolType]
public static class SetTransformTool
{
    [McpServerTool(Name = "unity_set_transform"),
     Description("GameObject의 Transform을 설정합니다")]
    public static async Task<string> Execute(
        UnityConnection connection,
        [Description("대상 경로")] string? path = null,
        [Description("대상 InstanceID")] int? instanceId = null,
        [Description("위치 JSON {x,y,z}")] string? position = null,
        [Description("회전 JSON {x,y,z} (Euler)")] string? rotation = null,
        [Description("스케일 JSON {x,y,z}")] string? scale = null,
        [Description("좌표계: 'local' 또는 'world'")] string space = "local",
        CancellationToken ct = default)
    {
        var paramDict = new Dictionary<string, object?>();
        if (path != null) paramDict["path"] = path;
        if (instanceId != null) paramDict["instanceId"] = instanceId;
        if (position != null) paramDict["position"] = JsonSerializer.Deserialize<object>(position);
        if (rotation != null) paramDict["rotation"] = JsonSerializer.Deserialize<object>(rotation);
        if (scale != null) paramDict["scale"] = JsonSerializer.Deserialize<object>(scale);
        paramDict["space"] = space;

        using var paramsJson = JsonDocument.Parse(JsonSerializer.Serialize(paramDict));
        var result = await connection.SendRequestAsync("unity_set_transform", paramsJson.RootElement, ct);
        return ResponseFormatter.Format(result);
    }
}
