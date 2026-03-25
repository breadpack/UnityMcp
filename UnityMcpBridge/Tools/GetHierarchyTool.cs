using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace UnityMcpBridge.Tools;

[McpServerToolType]
public static class GetHierarchyTool
{
    [McpServerTool(Name = "unity_get_hierarchy"), Description("Scene의 GameObject 계층구조를 조회합니다")]
    public static async Task<string> Execute(
        UnityConnection connection,
        [Description("최대 탐색 깊이 (1-10)")] int maxDepth = 3,
        [Description("컴포넌트 목록 포함 여부")] bool includeComponents = false,
        CancellationToken ct = default)
    {
        var paramsJson = JsonDocument.Parse(
            $"{{\"maxDepth\":{maxDepth},\"includeComponents\":{includeComponents.ToString().ToLower()}}}");
        var result = await connection.SendRequestAsync("unity_get_hierarchy", paramsJson.RootElement, ct);

        return ResponseFormatter.Format(result);
    }
}
