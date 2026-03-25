using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace UnityMcpBridge.Tools;

[McpServerToolType]
public static class RemoveComponentTool
{
    [McpServerTool(Name = "unity_remove_component"),
     Description("GameObject에서 Component를 제거합니다. Transform은 제거할 수 없습니다")]
    public static async Task<string> Execute(
        UnityConnection connection,
        [Description("Component 타입")] string componentType,
        [Description("대상 경로")] string? path = null,
        [Description("대상 InstanceID")] int? instanceId = null,
        [Description("같은 타입 컴포넌트가 여러 개일 때 인덱스 (0-based)")] int index = 0,
        [Description("실행하지 않고 결과만 미리보기")] bool dryRun = false,
        CancellationToken ct = default)
    {
        var paramDict = new Dictionary<string, object?>
        {
            ["componentType"] = componentType,
            ["index"] = index
        };
        if (path != null) paramDict["path"] = path;
        if (instanceId != null) paramDict["instanceId"] = instanceId;
        paramDict["dryRun"] = dryRun;

        using var paramsJson = JsonDocument.Parse(JsonSerializer.Serialize(paramDict));
        var result = await connection.SendRequestAsync("unity_remove_component", paramsJson.RootElement, ct);
        return ResponseFormatter.Format(result);
    }
}
