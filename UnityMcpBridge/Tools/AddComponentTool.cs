using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace UnityMcpBridge.Tools;

[McpServerToolType]
public static class AddComponentTool
{
    [McpServerTool(Name = "unity_add_component"),
     Description("GameObject에 Component를 추가합니다. 추가 후 설정 가능한 프로퍼티 목록을 반환합니다")]
    public static async Task<string> Execute(
        UnityConnection connection,
        [Description("Component 타입 (예: 'BoxCollider', 'UIDocument', 전체 이름도 가능)")] string componentType,
        [Description("대상 경로")] string? path = null,
        [Description("대상 InstanceID")] int? instanceId = null,
        CancellationToken ct = default)
    {
        var paramDict = new Dictionary<string, object?> { ["componentType"] = componentType };
        if (path != null) paramDict["path"] = path;
        if (instanceId != null) paramDict["instanceId"] = instanceId;

        using var paramsJson = JsonDocument.Parse(JsonSerializer.Serialize(paramDict));
        var result = await connection.SendRequestAsync("unity_add_component", paramsJson.RootElement, ct);
        var root = result.RootElement;
        if (root.TryGetProperty("success", out var s) && !s.GetBoolean())
            return $"Error: {root.GetProperty("error").GetString()}";
        return root.GetProperty("data").GetRawText();
    }
}
