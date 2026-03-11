using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace UnityMcpBridge.Tools;

[McpServerToolType]
public static class DeleteGameObjectTool
{
    [McpServerTool(Name = "unity_delete_gameobject"),
     Description("GameObject를 삭제합니다. Undo 지원")]
    public static async Task<string> Execute(
        UnityConnection connection,
        [Description("삭제할 GameObject 경로")] string? path = null,
        [Description("삭제할 GameObject InstanceID")] int? instanceId = null,
        [Description("자식 포함 삭제 여부 (false면 자식을 부모로 이동)")] bool includeChildren = true,
        CancellationToken ct = default)
    {
        var paramDict = new Dictionary<string, object?>();
        if (path != null) paramDict["path"] = path;
        if (instanceId != null) paramDict["instanceId"] = instanceId;
        paramDict["includeChildren"] = includeChildren;

        using var paramsJson = JsonDocument.Parse(JsonSerializer.Serialize(paramDict));
        var result = await connection.SendRequestAsync("unity_delete_gameobject", paramsJson.RootElement, ct);
        var root = result.RootElement;
        if (root.TryGetProperty("success", out var s) && !s.GetBoolean())
            return $"Error: {root.GetProperty("error").GetString()}";
        return root.GetProperty("data").GetRawText();
    }
}
