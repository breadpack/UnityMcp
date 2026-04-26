using Microsoft.Extensions.AI;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace UnityMcpBridge.Tools.Input;

[McpServerToolType]
public static class TypeTextTool
{
    [McpServerTool(Name = "unity_input_type_text"), Description("Play Mode에서 가상 키보드로 텍스트를 입력합니다. uGUI InputField/TMP_InputField가 포커스되어 있어야 합니다. ASCII 우선 — 한글/IME 입력은 미지원.")]
    public static async Task<IEnumerable<AIContent>> Execute(
        UnityConnection connection,
        [Description("입력할 텍스트")] string text,
        [Description("문자 간 입력 간격 (ms). 자연스러운 타이핑 시뮬레이션.")] int intervalMs = 20,
        [Description("입력 후 대기 프레임 수")] int waitFrames = 1,
        [Description("대기 조건 JSON")] string? waitFor = null,
        [Description("스크린샷+로그 캡처")] bool captureResult = false,
        CancellationToken ct = default)
    {
        var paramsObj = new Dictionary<string, object?>
        {
            ["text"] = text,
            ["intervalMs"] = intervalMs,
            ["waitFrames"] = waitFrames,
            ["captureResult"] = captureResult
        };
        if (!string.IsNullOrEmpty(waitFor)) paramsObj["waitFor"] = JsonDocument.Parse(waitFor).RootElement;

        var paramsJson = JsonDocument.Parse(JsonSerializer.Serialize(paramsObj));
        var result = await connection.SendRequestAsync("unity_input_type_text", paramsJson.RootElement, ct);
        return ClickTool.BuildResult(result.RootElement);
    }
}
