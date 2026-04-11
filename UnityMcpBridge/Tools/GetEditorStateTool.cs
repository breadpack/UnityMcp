using ModelContextProtocol.Server;
using System.ComponentModel;

namespace UnityMcpBridge.Tools;

[McpServerToolType]
public static class GetEditorStateTool
{
    [McpServerTool(Name = "unity_get_editor_state"), Description("Unity Editor의 현재 상태를 조회합니다 (isCompiling, isUpdating, isPlaying, unityVersion, projectName) — Hook 폴링용 경량 엔드포인트")]
    public static async Task<string> Execute(
        UnityConnection connection,
        CancellationToken ct = default)
    {
        var result = await connection.SendRequestAsync("unity_get_editor_state", ct: ct);
        return ResponseFormatter.Format(result);
    }
}
