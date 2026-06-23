using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace UnityMcpBridge.Tools;

[McpServerToolType]
public static class PrefabEditTool
{
    [McpServerTool(Name = "unity_prefab_edit"),
     Description(
         "Prefab 편집 스테이지의 진입/저장/종료/상태를 제어합니다. 이 도구 자체는 편집하지 않습니다 — " +
         "enter 후 generic 씬 도구(unity_set_property/unity_add_component/unity_create_gameobject 등)로 " +
         "편집하고 save→exit 합니다. enter는 루트 instanceId/path를 반환하고, 편집 모드에서 " +
         "unity_get_hierarchy는 prefab 내부(각 노드 instanceId 포함)를 반환합니다. " +
         "단일 호출 일괄 편집을 원하면 unity_prefab_apply를 쓰세요.")]
    public static async Task<string> Execute(
        UnityConnection connection,
        [Description("수행할 액션: enter, save, exit, status")] string action,
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
