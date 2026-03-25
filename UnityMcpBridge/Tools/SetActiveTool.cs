using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace UnityMcpBridge.Tools;

[McpServerToolType]
public static class SetActiveTool
{
    [McpServerTool(Name = "unity_set_active"),
     Description("GameObject의 활성 상태를 설정합니다")]
    public static async Task<string> Execute(
        UnityConnection connection,
        [Description("활성화 여부")] bool active,
        [Description("대상 GameObject 경로")] string? path = null,
        [Description("대상 GameObject InstanceID")] int? instanceId = null,
        CancellationToken ct = default)
    {
        var paramDict = new Dictionary<string, object?>();
        paramDict["active"] = active;
        if (path != null) paramDict["path"] = path;
        if (instanceId != null) paramDict["instanceId"] = instanceId;

        using var paramsJson = JsonDocument.Parse(JsonSerializer.Serialize(paramDict));
        var result = await connection.SendRequestAsync("unity_set_active", paramsJson.RootElement, ct);
        var root = result.RootElement;
        if (root.TryGetProperty("success", out var s) && !s.GetBoolean())
            return $"Error: {root.GetProperty("error").GetString()}";
        return root.GetProperty("data").GetRawText();
    }
}
