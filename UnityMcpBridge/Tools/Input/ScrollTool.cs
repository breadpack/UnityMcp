using Microsoft.Extensions.AI;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace UnityMcpBridge.Tools.Input;

[McpServerToolType]
public static class ScrollTool
{
    [McpServerTool(Name = "unity_input_scroll"), Description("Play Mode에서 GameView 좌표 위치에서 마우스 휠 스크롤을 발생시킵니다. ScrollRect 검증용.")]
    public static async Task<IEnumerable<AIContent>> Execute(
        UnityConnection connection,
        [Description("타겟 path 또는 JSON 객체")] string? target = null,
        [Description("스크린 좌표 JSON")] string? position = null,
        [Description("3D 월드 좌표 JSON")] string? worldPoint = null,
        [Description("수평 스크롤 (양수=오른쪽)")] float dx = 0,
        [Description("수직 스크롤 (양수=위로)")] float dy = 100,
        [Description("입력 후 대기 프레임 수")] int waitFrames = 1,
        [Description("대기 조건 JSON")] string? waitFor = null,
        [Description("스크린샷+로그 캡처")] bool captureResult = false,
        CancellationToken ct = default)
    {
        var paramsObj = new Dictionary<string, object?>
        {
            ["dx"] = dx,
            ["dy"] = dy,
            ["waitFrames"] = waitFrames,
            ["captureResult"] = captureResult
        };
        if (!string.IsNullOrEmpty(target)) paramsObj["target"] = ClickTool.TryParseOrString(target);
        if (!string.IsNullOrEmpty(position)) paramsObj["position"] = JsonDocument.Parse(position).RootElement;
        if (!string.IsNullOrEmpty(worldPoint)) paramsObj["worldPoint"] = JsonDocument.Parse(worldPoint).RootElement;
        if (!string.IsNullOrEmpty(waitFor)) paramsObj["waitFor"] = JsonDocument.Parse(waitFor).RootElement;

        var paramsJson = JsonDocument.Parse(JsonSerializer.Serialize(paramsObj));
        var result = await connection.SendRequestAsync("unity_input_scroll", paramsJson.RootElement, ct);
        return ClickTool.BuildResult(result.RootElement);
    }
}
