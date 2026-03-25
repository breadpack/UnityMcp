using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace UnityMcpBridge.Tools;

[McpServerToolType]
public static class ManagePackageTool
{
    [McpServerTool(Name = "unity_manage_package"), Description("Unity Package Manager 패키지를 관리합니다 (목록/추가/제거)")]
    public static async Task<string> Execute(
        UnityConnection connection,
        [Description("'list': 패키지 목록 조회, 'add': 패키지 추가, 'remove': 패키지 제거")] string action,
        [Description("패키지 식별자 (add/remove 시 필요, 예: \"com.unity.textmeshpro\", \"com.unity.textmeshpro@3.0.6\")")] string? packageId = null,
        CancellationToken ct = default)
    {
        var paramsObj = new { action, packageId };
        var paramsJson = JsonDocument.Parse(JsonSerializer.Serialize(paramsObj));
        var result = await connection.SendRequestAsync("unity_manage_package", paramsJson.RootElement, ct);

        return ResponseFormatter.Format(result);
    }
}
