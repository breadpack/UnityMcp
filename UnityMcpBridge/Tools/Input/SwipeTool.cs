using Microsoft.Extensions.AI;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace UnityMcpBridge.Tools.Input;

[McpServerToolType]
public static class SwipeTool
{
    [McpServerTool(Name = "unity_input_swipe"), Description("Play Mode에서 from 좌표/오브젝트로부터 방향+거리만큼 스와이프합니다. 모바일 패턴(스크롤뷰 플리킹 등) 검증용.")]
    public static async Task<IEnumerable<AIContent>> Execute(
        UnityConnection connection,
        [Description("시작 타겟 JSON (예: {\"target\":\"Canvas/ScrollView\"} 또는 {\"position\":{\"x\":100,\"y\":200}})")] string from,
        [Description("스와이프 방향 (\"up\"|\"down\"|\"left\"|\"right\")")] string direction = "right",
        [Description("스와이프 거리 (픽셀)")] float distance = 200,
        [Description("스와이프 지속 시간 (ms)")] int durationMs = 150,
        [Description("입력 후 대기 프레임 수")] int waitFrames = 1,
        [Description("대기 조건 JSON")] string? waitFor = null,
        [Description("스크린샷+로그 캡처")] bool captureResult = false,
        CancellationToken ct = default)
    {
        // Phase 1 ClickTool과 일관: 단순 path 문자열도 받아들이도록 fallback 처리
        object? fromValue = ClickTool.TryParseOrString(from);
        if (fromValue is string s)
            fromValue = new Dictionary<string, object?> { ["target"] = s };

        var paramsObj = new Dictionary<string, object?>
        {
            ["from"] = fromValue,
            ["direction"] = direction,
            ["distance"] = distance,
            ["durationMs"] = durationMs,
            ["waitFrames"] = waitFrames,
            ["captureResult"] = captureResult
        };
        if (!string.IsNullOrEmpty(waitFor)) paramsObj["waitFor"] = JsonDocument.Parse(waitFor).RootElement;

        var paramsJson = JsonDocument.Parse(JsonSerializer.Serialize(paramsObj));
        var result = await connection.SendRequestAsync("unity_input_swipe", paramsJson.RootElement, ct);
        return ClickTool.BuildResult(result.RootElement);
    }
}
