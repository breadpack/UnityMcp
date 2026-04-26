using Microsoft.Extensions.AI;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace UnityMcpBridge.Tools.Input;

[McpServerToolType]
public static class DragTool
{
    [McpServerTool(Name = "unity_input_drag"), Description("Play Mode에서 from 좌표/오브젝트에서 to 좌표/오브젝트로 드래그합니다. 슬라이더, 스크롤뷰, 드래그앤드롭 테스트용.")]
    public static async Task<IEnumerable<AIContent>> Execute(
        UnityConnection connection,
        [Description("시작 타겟 JSON (예: {\"target\":\"Canvas/Slider/Handle\"} 또는 {\"position\":{\"x\":100,\"y\":200}})")] string from,
        [Description("종료 타겟 JSON (from과 동일 형식)")] string to,
        [Description("경유점 배열 JSON (예: [{\"position\":{\"x\":150,\"y\":200}}])")] string? points = null,
        [Description("드래그 지속 시간 (ms). points 미지정 시 16ms 단위로 분할")] int durationMs = 200,
        [Description("마우스 버튼")] string button = "left",
        [Description("입력 후 대기 프레임 수")] int waitFrames = 1,
        [Description("대기 조건 JSON")] string? waitFor = null,
        [Description("스크린샷+로그 캡처")] bool captureResult = false,
        CancellationToken ct = default)
    {
        var paramsObj = new Dictionary<string, object?>
        {
            ["from"] = JsonDocument.Parse(from).RootElement,
            ["to"] = JsonDocument.Parse(to).RootElement,
            ["durationMs"] = durationMs,
            ["button"] = button,
            ["waitFrames"] = waitFrames,
            ["captureResult"] = captureResult
        };
        if (!string.IsNullOrEmpty(points)) paramsObj["points"] = JsonDocument.Parse(points).RootElement;
        if (!string.IsNullOrEmpty(waitFor)) paramsObj["waitFor"] = JsonDocument.Parse(waitFor).RootElement;

        var paramsJson = JsonDocument.Parse(JsonSerializer.Serialize(paramsObj));
        var result = await connection.SendRequestAsync("unity_input_drag", paramsJson.RootElement, ct);
        return ClickTool.BuildResult(result.RootElement);
    }
}
