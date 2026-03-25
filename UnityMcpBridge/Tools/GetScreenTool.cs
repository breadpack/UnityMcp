using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace UnityMcpBridge.Tools;

[McpServerToolType]
public static class GetScreenTool
{
    [McpServerTool(Name = "unity_get_screen"), Description("현재 활성 화면과 ViewModel 속성을 조회합니다")]
    public static async Task<string> Execute(UnityConnection connection, CancellationToken ct)
    {
        var result = await connection.SendRequestAsync("unity_get_screen", ct: ct);
        return ResponseFormatter.Format(result);
    }
}
