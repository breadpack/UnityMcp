using ModelContextProtocol.Server;
using System.ComponentModel;

namespace UnityMcpBridge.Tools;

[McpServerToolType]
public static class ListCustomToolsTool
{
    [McpServerTool(Name = "unity_list_custom_tools"), Description("사용자가 [McpTool] 어트리뷰트로 등록한 커스텀 도구 목록을 조회합니다")]
    public static async Task<string> Execute(
        UnityConnection connection,
        CancellationToken ct = default)
    {
        var result = await connection.SendRequestAsync("unity_list_custom_tools", ct: ct);
        return ResponseFormatter.Format(result);
    }
}
