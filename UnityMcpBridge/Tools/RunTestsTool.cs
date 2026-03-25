using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace UnityMcpBridge.Tools;

[McpServerToolType]
public static class RunTestsTool
{
    [McpServerTool(Name = "unity_run_tests"), Description("Unity Test Runner로 테스트를 실행합니다 (EditMode/PlayMode)")]
    public static async Task<string> Execute(
        UnityConnection connection,
        [Description("테스트 모드: 'EditMode', 'PlayMode', 'All' (기본값: EditMode)")] string? testMode = null,
        [Description("테스트 이름 필터 (부분 일치)")] string? testFilter = null,
        [Description("카테고리 필터")] string? categoryFilter = null,
        CancellationToken ct = default)
    {
        var paramsObj = new { testMode, testFilter, categoryFilter };
        var paramsJson = JsonDocument.Parse(JsonSerializer.Serialize(paramsObj));
        var result = await connection.SendRequestAsync("unity_run_tests", paramsJson.RootElement, ct);

        return ResponseFormatter.Format(result);
    }
}
