using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace UnityMcpBridge.Tools;

[McpServerToolType]
public static class ExecuteMenuItemTool
{
    [McpServerTool(Name = "unity_execute_menu_item"), Description("Unity Editor의 메뉴 아이템을 실행합니다")]
    public static async Task<string> Execute(
        UnityConnection connection,
        [Description("메뉴 경로 (예: \"GameObject/Create Empty\", \"Assets/Refresh\")")] string menuPath,
        [Description("true이면 실행하지 않고 사용 가능한 메뉴 목록만 반환")] bool listOnly = false,
        CancellationToken ct = default)
    {
        var paramsObj = new { menuPath, listOnly };
        var paramsJson = JsonDocument.Parse(JsonSerializer.Serialize(paramsObj));
        var result = await connection.SendRequestAsync("unity_execute_menu_item", paramsJson.RootElement, ct);

        return ResponseFormatter.Format(result);
    }
}
