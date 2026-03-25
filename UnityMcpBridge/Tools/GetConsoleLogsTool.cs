using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace UnityMcpBridge.Tools;

[McpServerToolType]
public static class GetConsoleLogsTool
{
    [McpServerTool(Name = "unity_get_console_logs"), Description("Unity Console의 최근 로그를 조회합니다")]
    public static async Task<string> Execute(
        UnityConnection connection,
        [Description("반환할 로그 수")] int count = 50,
        [Description("로그 타입 필터 (Log, Warning, Error, Exception)")] string? logType = null,
        [Description("스택 트레이스 포함 여부")] bool includeStackTrace = false,
        CancellationToken ct = default)
    {
        var parts = new List<string> { $"\"count\":{count}" };
        if (logType != null) parts.Add($"\"logType\":\"{logType}\"");
        if (includeStackTrace) parts.Add("\"includeStackTrace\":true");
        var paramsObj = "{" + string.Join(",", parts) + "}";
        var paramsJson = JsonDocument.Parse(paramsObj);
        var result = await connection.SendRequestAsync("unity_get_console_logs", paramsJson.RootElement, ct);

        var root = result.RootElement;
        if (root.TryGetProperty("success", out var s) && !s.GetBoolean())
            return $"Error: {root.GetProperty("error").GetString()}";
        return root.GetProperty("data").GetRawText();
    }
}
