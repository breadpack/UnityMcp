using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace UnityMcpBridge.Tools;

[McpServerToolType]
public static class LoadSceneTool
{
    [McpServerTool(Name = "unity_load_scene"), Description("Scene을 엽니다")]
    public static async Task<string> Execute(
        UnityConnection connection,
        [Description("열 Scene의 경로 (필수)")] string scenePath,
        [Description("Additive 모드로 열지 여부 (기본값: false)")] bool additive = false,
        CancellationToken ct = default)
    {
        var paramsObj = new { scenePath, additive };
        var paramsJson = JsonDocument.Parse(JsonSerializer.Serialize(paramsObj));
        var result = await connection.SendRequestAsync("unity_load_scene", paramsJson.RootElement, ct);

        return ResponseFormatter.Format(result);
    }
}
