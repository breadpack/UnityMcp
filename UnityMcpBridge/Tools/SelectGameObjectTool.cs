using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace UnityMcpBridge.Tools;

[McpServerToolType]
public static class SelectGameObjectTool
{
    [McpServerTool(Name = "unity_select_gameobject"),
     Description("Editor에서 GameObject를 선택합니다")]
    public static async Task<string> Execute(
        UnityConnection connection,
        [Description("대상 GameObject 경로")] string? path = null,
        [Description("대상 GameObject InstanceID")] int? instanceId = null,
        CancellationToken ct = default)
    {
        var paramDict = new Dictionary<string, object?>();
        if (path != null) paramDict["path"] = path;
        if (instanceId != null) paramDict["instanceId"] = instanceId;

        using var paramsJson = JsonDocument.Parse(JsonSerializer.Serialize(paramDict));
        var result = await connection.SendRequestAsync("unity_select_gameobject", paramsJson.RootElement, ct);
        return ResponseFormatter.Format(result);
    }
}
