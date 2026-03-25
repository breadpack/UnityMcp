using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace UnityMcpBridge.Tools;

[McpServerToolType]
public static class GetSelectionTool
{
    [McpServerTool(Name = "unity_get_selection"),
     Description("Unity Editor에서 현재 선택된 오브젝트 정보를 조회합니다")]
    public static async Task<string> Execute(
        UnityConnection connection,
        [Description("선택된 GameObject의 컴포넌트 목록 포함 여부")] bool includeComponents = false,
        CancellationToken ct = default)
    {
        if (includeComponents)
        {
            using var paramsJson = JsonDocument.Parse("{\"includeComponents\":true}");
            var result = await connection.SendRequestAsync("unity_get_selection", paramsJson.RootElement, ct);
            return ResponseFormatter.Format(result);
        }
        else
        {
            var result = await connection.SendRequestAsync("unity_get_selection", ct: ct);
            return ResponseFormatter.Format(result);
        }
    }
}
