using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace UnityMcpBridge.Tools;

[McpServerToolType]
public static class CreateMaterialTool
{
    [McpServerTool(Name = "unity_create_material"),
     Description("새 Material 에셋을 생성합니다")]
    public static async Task<string> Execute(
        UnityConnection connection,
        [Description("저장 경로 (예: \"Assets/Materials/NewMat.mat\")")] string savePath,
        [Description("셰이더 이름 (기본값: \"Standard\" 또는 \"Universal Render Pipeline/Lit\")")] string? shaderName = null,
        CancellationToken ct = default)
    {
        var paramDict = new Dictionary<string, object?>
        {
            ["savePath"] = savePath
        };
        if (shaderName != null) paramDict["shaderName"] = shaderName;

        using var paramsJson = JsonDocument.Parse(JsonSerializer.Serialize(paramDict));
        var result = await connection.SendRequestAsync("unity_create_material", paramsJson.RootElement, ct);
        return ResponseFormatter.Format(result);
    }
}
