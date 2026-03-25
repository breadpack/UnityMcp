using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace UnityMcpBridge.Tools;

[McpServerToolType]
public static class GetAvailableActionsTool
{
    [McpServerTool(Name = "unity_get_available_actions"), Description("현재 화면에서 클릭 가능한 UI 요소 목록을 반환합니다")]
    public static async Task<string> Execute(UnityConnection connection, CancellationToken ct)
    {
        var result = await connection.SendRequestAsync("unity_get_available_actions", ct: ct);
        return ResponseFormatter.Format(result);
    }
}
