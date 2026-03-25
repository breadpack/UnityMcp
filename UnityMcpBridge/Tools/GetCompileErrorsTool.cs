using ModelContextProtocol.Server;
using System.ComponentModel;

namespace UnityMcpBridge.Tools;

[McpServerToolType]
public static class GetCompileErrorsTool
{
    [McpServerTool(Name = "unity_get_compile_errors"), Description("현재 스크립트 컴파일 상태와 에러/경고를 조회합니다")]
    public static async Task<string> Execute(
        UnityConnection connection,
        CancellationToken ct = default)
    {
        var result = await connection.SendRequestAsync("unity_get_compile_errors", ct: ct);

        return ResponseFormatter.Format(result);
    }
}
