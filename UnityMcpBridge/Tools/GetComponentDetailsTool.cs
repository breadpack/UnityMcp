using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace UnityMcpBridge.Tools;

[McpServerToolType]
public static class GetComponentDetailsTool
{
    [McpServerTool(Name = "unity_get_component_details"),
     Description("GameObject의 Component 프로퍼티 세부 정보(현재 값)를 조회합니다")]
    public static async Task<string> Execute(
        UnityConnection connection,
        [Description("대상 GameObject 경로")] string? path = null,
        [Description("대상 GameObject InstanceID")] int? instanceId = null,
        [Description("특정 컴포넌트 타입만 조회 (미지정 시 전체)")] string? componentType = null,
        [Description("같은 타입 컴포넌트가 여러 개일 때 인덱스 (0-based)")] int index = 0,
        CancellationToken ct = default)
    {
        var paramDict = new Dictionary<string, object?> { ["index"] = index };
        if (path != null) paramDict["path"] = path;
        if (instanceId != null) paramDict["instanceId"] = instanceId;
        if (componentType != null) paramDict["componentType"] = componentType;

        using var paramsJson = JsonDocument.Parse(JsonSerializer.Serialize(paramDict));
        var result = await connection.SendRequestAsync("unity_get_component_details", paramsJson.RootElement, ct);
        return ResponseFormatter.Format(result);
    }
}
