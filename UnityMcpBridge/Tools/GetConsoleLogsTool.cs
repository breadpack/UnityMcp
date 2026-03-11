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
        CancellationToken ct = default)
    {
        var paramsObj = logType != null
            ? $"{{\"count\":{count},\"logType\":\"{logType}\"}}"
            : $"{{\"count\":{count}}}";
        var paramsJson = JsonDocument.Parse(paramsObj);
        var result = await connection.SendRequestAsync("unity_get_console_logs", paramsJson.RootElement, ct);

        var root = result.RootElement;
        if (root.TryGetProperty("success", out var s) && !s.GetBoolean())
            return $"Error: {root.GetProperty("error").GetString()}";
        return root.GetProperty("data").GetRawText();
    }
}
