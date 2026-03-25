using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace UnityMcpBridge.Tools;

[McpServerToolType]
public static class UndoRedoTool
{
    [McpServerTool(Name = "unity_undo"), Description("Unity의 Undo/Redo를 실행하거나 히스토리를 조회합니다")]
    public static async Task<string> Execute(
        UnityConnection connection,
        [Description("'undo': Undo 실행, 'redo': Redo 실행, 'history': 히스토리 조회")] string action,
        [Description("undo/redo 실행 횟수 또는 history 조회 시 항목 수 (기본값: 1)")] int count = 1,
        CancellationToken ct = default)
    {
        var paramsObj = new { action, count };
        var paramsJson = JsonDocument.Parse(JsonSerializer.Serialize(paramsObj));
        var result = await connection.SendRequestAsync("unity_undo", paramsJson.RootElement, ct);

        return ResponseFormatter.Format(result);
    }
}
