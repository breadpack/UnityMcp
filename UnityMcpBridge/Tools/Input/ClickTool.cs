using Microsoft.Extensions.AI;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace UnityMcpBridge.Tools.Input;

[McpServerToolType]
public static class ClickTool
{
    [McpServerTool(Name = "unity_input_click"), Description("Play Mode에서 GameView UI/3D 오브젝트를 클릭합니다. target/position/worldPoint 중 하나를 지정하세요.")]
    public static async Task<IEnumerable<AIContent>> Execute(
        UnityConnection connection,
        [Description("타겟 path (예: \"Canvas/Panel/Button\") 또는 JSON 객체 (예: {\"path\":\"Button\",\"index\":1} / {\"instanceId\":123} / {\"ve\":\"root/start-button\"})")] string? target = null,
        [Description("스크린 좌표 JSON (예: {\"x\":480,\"y\":320})")] string? position = null,
        [Description("3D 월드 좌표 JSON (예: {\"x\":0,\"y\":1,\"z\":5})")] string? worldPoint = null,
        [Description("마우스 버튼 (\"left\"|\"right\"|\"middle\")")] string button = "left",
        [Description("클릭 횟수 (2면 더블클릭)")] int count = 1,
        [Description("입력 후 대기 프레임 수")] int waitFrames = 1,
        [Description("대기 조건 JSON (5종 predicate)")] string? waitFor = null,
        [Description("true면 스크린샷 + 콘솔 로그를 응답에 포함")] bool captureResult = false,
        CancellationToken ct = default)
    {
        var paramsObj = new Dictionary<string, object?>
        {
            ["button"] = button,
            ["count"] = count,
            ["waitFrames"] = waitFrames,
            ["captureResult"] = captureResult
        };
        if (!string.IsNullOrEmpty(target)) paramsObj["target"] = TryParseOrString(target);
        if (!string.IsNullOrEmpty(position)) paramsObj["position"] = JsonDocument.Parse(position).RootElement;
        if (!string.IsNullOrEmpty(worldPoint)) paramsObj["worldPoint"] = JsonDocument.Parse(worldPoint).RootElement;
        if (!string.IsNullOrEmpty(waitFor)) paramsObj["waitFor"] = JsonDocument.Parse(waitFor).RootElement;

        var paramsJson = JsonDocument.Parse(JsonSerializer.Serialize(paramsObj));
        var result = await connection.SendRequestAsync("unity_input_click", paramsJson.RootElement, ct);
        return BuildResult(result.RootElement);
    }

    internal static object? TryParseOrString(string s)
    {
        s = s.Trim();
        if (s.StartsWith("{") || s.StartsWith("["))
        {
            try { return JsonDocument.Parse(s).RootElement; }
            catch { /* fall through */ }
        }
        return s;
    }

    internal static IEnumerable<AIContent> BuildResult(JsonElement root)
    {
        if (root.TryGetProperty("success", out var s) && !s.GetBoolean())
            return new AIContent[] { new TextContent($"Error: {root.GetProperty("error").GetString()}") };

        var data = root.GetProperty("data");
        var contents = new List<AIContent>();

        if (data.TryGetProperty("screenshotBase64", out var b64) && b64.GetString() is string base64 && base64.Length > 0)
        {
            var mime = data.GetProperty("mimeType").GetString() ?? "image/jpeg";
            contents.Add(new DataContent(Convert.FromBase64String(base64), mime));
        }

        contents.Add(new TextContent(data.GetRawText()));
        return contents;
    }
}
