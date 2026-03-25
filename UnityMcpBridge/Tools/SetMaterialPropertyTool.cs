using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace UnityMcpBridge.Tools;

[McpServerToolType]
public static class SetMaterialPropertyTool
{
    [McpServerTool(Name = "unity_set_material_property"),
     Description("Material의 프로퍼티를 설정합니다 (Color, Float, Texture 등)")]
    public static async Task<string> Execute(
        UnityConnection connection,
        [Description("Material 에셋 경로 (예: \"Assets/Materials/MyMat.mat\")")] string materialPath,
        [Description("프로퍼티 이름 (예: \"_Color\", \"_MainTex\")")] string propertyName,
        [Description("JSON 값. Color: \"{\\\"r\\\":1,\\\"g\\\":0,\\\"b\\\":0,\\\"a\\\":1}\", Float: \"1.5\", Texture: \"Assets/Textures/tex.png\"")] string value,
        [Description("프로퍼티 타입: \"color\", \"float\", \"int\", \"texture\", \"vector\"")] string propertyType,
        CancellationToken ct = default)
    {
        var paramDict = new Dictionary<string, object?>
        {
            ["materialPath"] = materialPath,
            ["propertyName"] = propertyName,
            ["value"] = value,
            ["propertyType"] = propertyType
        };

        using var paramsJson = JsonDocument.Parse(JsonSerializer.Serialize(paramDict));
        var result = await connection.SendRequestAsync("unity_set_material_property", paramsJson.RootElement, ct);
        return ResponseFormatter.Format(result);
    }
}
