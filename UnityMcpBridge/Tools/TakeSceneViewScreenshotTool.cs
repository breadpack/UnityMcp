using Microsoft.Extensions.AI;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace UnityMcpBridge.Tools;

[McpServerToolType]
public static class TakeSceneViewScreenshotTool
{
    [McpServerTool(Name = "unity_take_scene_view_screenshot"), Description("Unity Scene View의 스크린샷을 캡처합니다 (Play Mode 불필요)")]
    public static async Task<IEnumerable<AIContent>> Execute(
        UnityConnection connection,
        [Description("JPEG 품질 (1-100, 0이면 PNG)")] int quality = 75,
        [Description("최대 너비 (0이면 원본 유지)")] int maxWidth = 0,
        CancellationToken ct = default)
    {
        var paramsJson = JsonDocument.Parse($"{{\"quality\":{quality},\"maxWidth\":{maxWidth}}}");
        var result = await connection.SendRequestAsync("unity_take_scene_view_screenshot", paramsJson.RootElement, ct);

        var root = result.RootElement;
        if (root.TryGetProperty("success", out var s) && !s.GetBoolean())
            return [new TextContent($"Error: {root.GetProperty("error").GetString()}")];

        var data = root.GetProperty("data");
        var base64 = data.GetProperty("imageBase64").GetString()!;
        var mimeType = data.GetProperty("mimeType").GetString()!;
        var width = data.GetProperty("width").GetInt32();
        var height = data.GetProperty("height").GetInt32();

        var imageBytes = Convert.FromBase64String(base64);
        return [
            new DataContent(imageBytes, mimeType),
            new TextContent($"{width}x{height}")
        ];
    }
}
