using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace UnityMcpBridge.Tools;

[McpServerToolType]
public static class ExecuteCodeTool
{
    [McpServerTool(Name = "unity_execute_code"), Description("Unity Editor 컨텍스트에서 C# 코드를 실행합니다. Unity API에 접근할 수 있습니다")]
    public static async Task<string> Execute(
        UnityConnection connection,
        [Description("실행할 C# 코드. 마지막 표현식의 값이 반환됩니다")] string code,
        [Description("추가 using 문 (쉼표 구분, 기본: \"UnityEngine,UnityEditor\")")] string usings = "UnityEngine,UnityEditor",
        CancellationToken ct = default)
    {
        var paramsObj = new { code, usings };
        var paramsJson = JsonDocument.Parse(JsonSerializer.Serialize(paramsObj));
        var result = await connection.SendRequestAsync("unity_execute_code", paramsJson.RootElement, ct);

        return ResponseFormatter.Format(result);
    }
}
