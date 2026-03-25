using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace UnityMcpBridge.Tools;

[McpServerToolType]
public static class GetUguiTreeTool
{
    [McpServerTool(Name = "unity_get_ugui_tree"), Description("UGUI (Canvas 기반) UI 계층 구조를 조회합니다")]
    public static async Task<string> Execute(
        UnityConnection connection,
        [Description("최대 탐색 깊이")] int maxDepth = 3,
        [Description("RectTransform 상세 정보 포함 여부")] bool includeDetails = false,
        CancellationToken ct = default)
    {
        var paramsJson = JsonDocument.Parse($"{{\"maxDepth\":{maxDepth},\"includeDetails\":{(includeDetails ? "true" : "false")}}}");
        var result = await connection.SendRequestAsync("unity_get_ugui_tree", paramsJson.RootElement, ct);
        return ResponseFormatter.Format(result);
    }
}
