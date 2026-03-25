using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace UnityMcpBridge.Tools;

[McpServerToolType]
public static class SaveSceneTool
{
    [McpServerTool(Name = "unity_save_scene"), Description("현재 열린 Scene을 저장합니다")]
    public static async Task<string> Execute(
        UnityConnection connection,
        [Description("저장할 Scene의 경로. null이면 현재 활성 씬을 저장합니다")] string? scenePath = null,
        [Description("다른 이름으로 저장할지 여부 (기본값: false)")] bool saveAs = false,
        CancellationToken ct = default)
    {
        var paramsObj = new { scenePath, saveAs };
        var paramsJson = JsonDocument.Parse(JsonSerializer.Serialize(paramsObj));
        var result = await connection.SendRequestAsync("unity_save_scene", paramsJson.RootElement, ct);

        var root = result.RootElement;
        if (root.TryGetProperty("success", out var s) && !s.GetBoolean())
            return $"Error: {root.GetProperty("error").GetString()}";
        return root.GetProperty("data").GetRawText();
    }
}
