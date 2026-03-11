using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace UnityMcpBridge.Tools;

[McpServerToolType]
public static class SetPropertyTool
{
    [McpServerTool(Name = "unity_set_property"),
     Description("Component의 필드/프로퍼티를 설정합니다. dot-notation, Array, Asset참조($asset/$guid) 지원")]
    public static async Task<string> Execute(
        UnityConnection connection,
        [Description("Component 타입")] string componentType,
        [Description("설정할 프로퍼티 JSON (예: {\"size\":{\"x\":1,\"y\":2,\"z\":3}})")] string properties,
        [Description("대상 경로")] string? path = null,
        [Description("대상 InstanceID")] int? instanceId = null,
        [Description("같은 타입 컴포넌트 인덱스 (0-based)")] int index = 0,
        CancellationToken ct = default)
    {
        var paramDict = new Dictionary<string, object?>
        {
            ["componentType"] = componentType,
            ["index"] = index,
            ["properties"] = JsonSerializer.Deserialize<object>(properties)
        };
        if (path != null) paramDict["path"] = path;
        if (instanceId != null) paramDict["instanceId"] = instanceId;

        using var paramsJson = JsonDocument.Parse(JsonSerializer.Serialize(paramDict));
        var result = await connection.SendRequestAsync("unity_set_property", paramsJson.RootElement, ct);
        var root = result.RootElement;
        if (root.TryGetProperty("success", out var s) && !s.GetBoolean())
            return $"Error: {root.GetProperty("error").GetString()}";
        return root.GetProperty("data").GetRawText();
    }
}
