using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace UnityMcpBridge.Tools;

[McpServerToolType]
public static class CustomToolProxyTool
{
    [McpServerTool(Name = "unity_custom_tool"), Description("사용자가 [McpTool] 어트리뷰트로 등록한 커스텀 도구를 실행합니다. unity_list_custom_tools로 사용 가능한 도구를 먼저 확인하세요")]
    public static async Task<string> Execute(
        UnityConnection connection,
        [Description("실행할 커스텀 도구 이름")] string toolName,
        [Description("JSON 형태의 파라미터 (예: {\"key\":\"value\"})")] string parameters = "{}",
        CancellationToken ct = default)
    {
        JsonElement paramsElement;
        try
        {
            paramsElement = JsonDocument.Parse(parameters).RootElement;
        }
        catch
        {
            return "Error: parameters must be valid JSON";
        }

        // Forward directly to the custom tool handler (registered by its actual name)
        var result = await connection.SendRequestAsync(toolName, paramsElement, ct);
        return ResponseFormatter.Format(result);
    }
}
