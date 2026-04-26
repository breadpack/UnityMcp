using Microsoft.Extensions.AI;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace UnityMcpBridge.Tools.Input;

[McpServerToolType]
public static class KeyTool
{
    [McpServerTool(Name = "unity_input_key"), Description("Play Mode에서 가상 키보드 키를 누릅니다. 단축키, ESC, Enter 등 단일 키 입력용. 텍스트 입력은 unity_input_type_text를 사용하세요.")]
    public static async Task<IEnumerable<AIContent>> Execute(
        UnityConnection connection,
        [Description("키 이름 (UnityEngine.InputSystem.Key 열거자, 예: \"Enter\", \"Escape\", \"A\", \"Digit1\")")] string key,
        [Description("modifier 키 배열 JSON (예: [\"Ctrl\",\"Shift\"]). \"Ctrl\"|\"Shift\"|\"Alt\"|\"Cmd\".")] string? modifiers = null,
        [Description("\"press\"(누름+뗌) | \"down\"(누름만) | \"up\"(뗌만)")] string action = "press",
        [Description("입력 후 대기 프레임 수")] int waitFrames = 1,
        [Description("대기 조건 JSON")] string? waitFor = null,
        [Description("스크린샷+로그 캡처")] bool captureResult = false,
        CancellationToken ct = default)
    {
        var paramsObj = new Dictionary<string, object?>
        {
            ["key"] = key,
            ["action"] = action,
            ["waitFrames"] = waitFrames,
            ["captureResult"] = captureResult
        };
        if (!string.IsNullOrEmpty(modifiers)) paramsObj["modifiers"] = JsonDocument.Parse(modifiers).RootElement;
        if (!string.IsNullOrEmpty(waitFor)) paramsObj["waitFor"] = JsonDocument.Parse(waitFor).RootElement;

        var paramsJson = JsonDocument.Parse(JsonSerializer.Serialize(paramsObj));
        var result = await connection.SendRequestAsync("unity_input_key", paramsJson.RootElement, ct);
        return ClickTool.BuildResult(result.RootElement);
    }
}
