using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace UnityMcpBridge.Tools;

[McpServerToolType]
public static class BuildTool
{
    [McpServerTool(Name = "unity_build"), Description("프로젝트를 빌드합니다")]
    public static async Task<string> Execute(
        UnityConnection connection,
        [Description("빌드 출력 경로")] string outputPath,
        [Description("빌드 타겟: 'Windows', 'macOS', 'Linux', 'Android', 'iOS', 'WebGL' (기본: 현재 타겟)")] string? target = null,
        [Description("빌드할 씬 경로 JSON 배열 (예: '[\"Assets/Scenes/Main.unity\"]'). 미지정 시 Build Settings 사용")] string? scenes = null,
        CancellationToken ct = default)
    {
        var paramsObj = new { outputPath, target, scenes };
        var paramsJson = JsonDocument.Parse(JsonSerializer.Serialize(paramsObj));
        var result = await connection.SendRequestAsync("unity_build", paramsJson.RootElement, ct);

        return ResponseFormatter.Format(result);
    }
}
