using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace UnityMcpBridge.Tools;

[McpServerToolType]
public static class ProjectSettingsTool
{
    [McpServerTool(Name = "unity_project_settings"), Description("ProjectSettings를 조회하거나 설정합니다")]
    public static async Task<string> Execute(
        UnityConnection connection,
        [Description("'get' 또는 'set'")] string action,
        [Description("설정 카테고리: 'player', 'quality', 'physics', 'time'")] string category,
        [Description("설정 이름 (예: player의 'productName', 'bundleVersion' / time의 'fixedDeltaTime')")] string? propertyName = null,
        [Description("설정 값 (set 시 필수)")] string? value = null,
        CancellationToken ct = default)
    {
        var paramsObj = new { action, category, propertyName, value };
        var paramsJson = JsonDocument.Parse(JsonSerializer.Serialize(paramsObj));
        var result = await connection.SendRequestAsync("unity_project_settings", paramsJson.RootElement, ct);

        return ResponseFormatter.Format(result);
    }
}
