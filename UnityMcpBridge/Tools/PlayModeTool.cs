using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace UnityMcpBridge.Tools;

[McpServerToolType]
public static class PlayModeTool
{
    [McpServerTool(Name = "unity_play_mode"), Description("Unity Editor의 Play Mode를 제어합니다. 스크린샷 등 Play Mode가 필요한 기능 사용 전에 호출합니다")]
    public static async Task<string> Execute(
        UnityConnection connection,
        [Description("'enter': Play Mode 진입, 'exit': Play Mode 종료, 'toggle': 전환 (기본값: toggle)")] string action = "toggle",
        CancellationToken ct = default)
    {
        var paramsObj = new { action };
        var paramsJson = JsonDocument.Parse(JsonSerializer.Serialize(paramsObj));
        var result = await connection.SendRequestAsync("unity_play_mode", paramsJson.RootElement, ct);

        var root = result.RootElement;
        if (root.TryGetProperty("success", out var s) && !s.GetBoolean())
            return $"Error: {root.GetProperty("error").GetString()}";
        return root.GetProperty("data").GetRawText();
    }
}
