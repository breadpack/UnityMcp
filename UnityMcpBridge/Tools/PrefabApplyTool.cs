using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace UnityMcpBridge.Tools;

[McpServerToolType]
public static class PrefabApplyTool
{
    [McpServerTool(Name = "unity_prefab_apply"),
     Description(
         "Prefab을 편집 모드(enter/edit/save_and_exit) 없이 단일 원자 호출로 편집합니다. " +
         "edits 배열의 각 op를 prefab 루트 기준 상대 경로 target에 순차 적용 후 자동 저장합니다. " +
         "스테이지 진입 불필요·instanceId 불필요·부분 적용/유실 없음. " +
         "여러 GameObject를 가리키는 prefab 일괄 편집의 권장 경로입니다. Undo는 미지원입니다. " +
         "op 종류: set_property, add_component(properties 인라인 가능), remove_component, set_transform, " +
         "set_active, create_child, reparent, set_asset_reference, delete.")]
    public static async Task<string> Execute(
        UnityConnection connection,
        [Description(
            "편집 작업 배열(JSON). 각 항목 {op, target, ...}. " +
            "target은 prefab 루트 기준 상대 경로('' 또는 생략 시 루트, '루트이름/자식' 또는 '자식/손자' 모두 허용). " +
            "예: [{\"op\":\"set_property\",\"target\":\"Body\",\"componentType\":\"SpriteRenderer\",\"properties\":{\"color\":{\"r\":1,\"g\":0,\"b\":0,\"a\":1}}}," +
            "{\"op\":\"add_component\",\"target\":\"\",\"componentType\":\"BoxCollider2D\"}]")]
        string edits,
        [Description("Prefab 경로 (예: 'Assets/Prefabs/Enemy.prefab')")] string? assetPath = null,
        [Description("Prefab GUID (assetPath 대체)")] string? assetGuid = null,
        CancellationToken ct = default)
    {
        var paramDict = new Dictionary<string, object?>
        {
            ["edits"] = JsonSerializer.Deserialize<object>(edits)
        };
        if (assetPath != null) paramDict["assetPath"] = assetPath;
        if (assetGuid != null) paramDict["assetGuid"] = assetGuid;

        using var paramsJson = JsonDocument.Parse(JsonSerializer.Serialize(paramDict));
        var result = await connection.SendRequestAsync("unity_prefab_apply", paramsJson.RootElement, ct);
        return ResponseFormatter.Format(result);
    }
}
