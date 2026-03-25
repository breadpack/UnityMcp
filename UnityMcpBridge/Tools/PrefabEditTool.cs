using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace UnityMcpBridge.Tools;

[McpServerToolType]
public static class PrefabEditTool
{
    [McpServerTool(Name = "unity_prefab_edit"),
     Description("Prefab 편집 모드 진입/저장/종료를 제어합니다")]
    public static async Task<string> Execute(
        UnityConnection connection,
        [Description("수행할 액션: enter, save, exit")] string action,
        [Description("Prefab 에셋 경로 (enter 시 필수)")] string? assetPath = null,
        CancellationToken ct = default)
    {
        var paramDict = new Dictionary<string, object?>();
        paramDict["action"] = action;
        if (assetPath != null) paramDict["assetPath"] = assetPath;

        using var paramsJson = JsonDocument.Parse(JsonSerializer.Serialize(paramDict));
        var result = await connection.SendRequestAsync("unity_prefab_edit", paramsJson.RootElement, ct);
        var root = result.RootElement;
        if (root.TryGetProperty("success", out var s) && !s.GetBoolean())
            return $"Error: {root.GetProperty("error").GetString()}";
        return root.GetProperty("data").GetRawText();
    }
}
