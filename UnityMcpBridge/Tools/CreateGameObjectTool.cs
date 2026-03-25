using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace UnityMcpBridge.Tools;

[McpServerToolType]
public static class CreateGameObjectTool
{
    [McpServerTool(Name = "unity_create_gameobject"),
     Description("새 GameObject를 생성합니다")]
    public static async Task<string> Execute(
        UnityConnection connection,
        [Description("생성할 GameObject 이름")] string name = "GameObject",
        [Description("부모 GameObject 경로 (예: 'Canvas/Panel')")] string? parentPath = null,
        [Description("부모 GameObject InstanceID")] int? parentId = null,
        CancellationToken ct = default)
    {
        var paramDict = new Dictionary<string, object?> { ["name"] = name };
        if (parentPath != null) paramDict["parentPath"] = parentPath;
        if (parentId != null) paramDict["parentId"] = parentId;

        using var paramsJson = JsonDocument.Parse(JsonSerializer.Serialize(paramDict));
        var result = await connection.SendRequestAsync("unity_create_gameobject", paramsJson.RootElement, ct);
        return ResponseFormatter.Format(result);
    }
}
