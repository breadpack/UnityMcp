using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace UnityMcpBridge.Tools;

[McpServerToolType]
public static class FindAssetsTool
{
    [McpServerTool(Name = "unity_find_assets"),
     Description("프로젝트에서 에셋을 검색합니다 (이름, 타입, 레이블 필터)")]
    public static async Task<string> Execute(
        UnityConnection connection,
        [Description("검색 필터 (Unity 검색 문법: \"t:Material\", \"l:UI\", \"PlayerPrefab t:Prefab\" 등)")] string filter,
        [Description("검색할 폴더 경로 (쉼표 구분, 예: \"Assets/Prefabs,Assets/Materials\")")] string? searchInFolders = null,
        [Description("최대 결과 수")] int maxResults = 50,
        CancellationToken ct = default)
    {
        var paramDict = new Dictionary<string, object?>
        {
            ["filter"] = filter,
            ["maxResults"] = maxResults
        };
        if (searchInFolders != null) paramDict["searchInFolders"] = searchInFolders;

        using var paramsJson = JsonDocument.Parse(JsonSerializer.Serialize(paramDict));
        var result = await connection.SendRequestAsync("unity_find_assets", paramsJson.RootElement, ct);
        return ResponseFormatter.Format(result);
    }
}
