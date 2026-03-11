using Microsoft.Extensions.AI;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace UnityMcpBridge.Tools;

[McpServerToolType]
public static class RenderUxmlTool
{
    [McpServerTool(Name = "unity_render_uxml"), Description("UXML 파일을 오프스크린 렌더링하여 이미지로 반환합니다. UI Toolkit 레이아웃 확인에 사용합니다")]
    public static async Task<IEnumerable<AIContent>> Execute(
        UnityConnection connection,
        [Description("UXML 에셋 경로 (예: Assets/_GAME_ASSETS/UI/UI_TOOLKIT/Menu/Screens/TouchStartScreen.uxml)")] string uxmlPath,
        [Description("렌더링 너비 (픽셀)")] int width = 1080,
        [Description("렌더링 높이 (픽셀)")] int height = 1920,
        [Description("JPEG 품질 (1-100). 0이면 PNG")] int quality = 75,
        CancellationToken ct = default)
    {
        var paramsObj = new { uxmlPath, width, height, quality };
        var paramsJson = JsonDocument.Parse(JsonSerializer.Serialize(paramsObj));
        var result = await connection.SendRequestAsync("unity_render_uxml", paramsJson.RootElement, ct);

        var root = result.RootElement;
        if (root.TryGetProperty("success", out var s) && !s.GetBoolean())
            return [new TextContent($"Error: {root.GetProperty("error").GetString()}")];

        var data = root.GetProperty("data");
        var base64 = data.GetProperty("imageBase64").GetString()!;
        var mimeType = data.GetProperty("mimeType").GetString()!;
        var w = data.GetProperty("width").GetInt32();
        var h = data.GetProperty("height").GetInt32();

        var imageBytes = Convert.FromBase64String(base64);
        return [
            new DataContent(imageBytes, mimeType),
            new TextContent($"{w}x{h} rendered from {uxmlPath}")
        ];
    }
}
