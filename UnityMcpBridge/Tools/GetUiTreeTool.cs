using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace UnityMcpBridge.Tools;

[McpServerToolType]
public static class GetUiTreeTool
{
    [McpServerTool(Name = "unity_get_ui_tree"), Description("현재 UI VisualElement 트리를 조회합니다")]
    public static async Task<string> Execute(
        UnityConnection connection,
        [Description("최대 탐색 깊이")] int maxDepth = 10,
        CancellationToken ct = default)
    {
        var paramsJson = JsonDocument.Parse($"{{\"maxDepth\":{maxDepth}}}");
        var result = await connection.SendRequestAsync("unity_get_ui_tree", paramsJson.RootElement, ct);
        return ResponseFormatter.Format(result);
    }
}
