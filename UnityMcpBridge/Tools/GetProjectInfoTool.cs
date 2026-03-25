using ModelContextProtocol.Server;
using System.ComponentModel;

namespace UnityMcpBridge.Tools;

[McpServerToolType]
public static class GetProjectInfoTool
{
    [McpServerTool(Name = "unity_get_project_info"), Description("Unity 프로젝트의 기본 정보를 조회합니다 (버전, 렌더 파이프라인, 패키지, 빌드 타겟 등)")]
    public static async Task<string> Execute(
        UnityConnection connection,
        CancellationToken ct = default)
    {
        var result = await connection.SendRequestAsync("unity_get_project_info", ct: ct);
        return ResponseFormatter.Format(result);
    }
}
