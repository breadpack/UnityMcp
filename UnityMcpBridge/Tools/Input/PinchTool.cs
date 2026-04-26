using Microsoft.Extensions.AI;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace UnityMcpBridge.Tools.Input;

[McpServerToolType]
public static class PinchTool
{
    [McpServerTool(Name = "unity_input_pinch"), Description("Play Mode에서 두 손가락 핀치 제스처를 시뮬레이션합니다. center를 기준으로 두 손가락이 startSpread → endSpread로 변화. 줌/회전 검증용 (수평 축).")]
    public static async Task<IEnumerable<AIContent>> Execute(
        UnityConnection connection,
        [Description("중심 타겟 JSON (예: {\"target\":\"Canvas/Photo\"} 또는 {\"position\":{\"x\":480,\"y\":320}})")] string center,
        [Description("초기 두 손가락 거리 (픽셀)")] float startSpread = 100,
        [Description("최종 두 손가락 거리 (픽셀). startSpread보다 크면 zoom-in, 작으면 zoom-out.")] float endSpread = 300,
        [Description("핀치 지속 시간 (ms)")] int durationMs = 300,
        [Description("입력 후 대기 프레임 수")] int waitFrames = 1,
        [Description("대기 조건 JSON")] string? waitFor = null,
        [Description("스크린샷+로그 캡처")] bool captureResult = false,
        CancellationToken ct = default)
    {
        var paramsObj = new Dictionary<string, object?>
        {
            ["center"] = JsonDocument.Parse(center).RootElement,
            ["startSpread"] = startSpread,
            ["endSpread"] = endSpread,
            ["durationMs"] = durationMs,
            ["waitFrames"] = waitFrames,
            ["captureResult"] = captureResult
        };
        if (!string.IsNullOrEmpty(waitFor)) paramsObj["waitFor"] = JsonDocument.Parse(waitFor).RootElement;

        var paramsJson = JsonDocument.Parse(JsonSerializer.Serialize(paramsObj));
        var result = await connection.SendRequestAsync("unity_input_pinch", paramsJson.RootElement, ct);
        return ClickTool.BuildResult(result.RootElement);
    }
}
