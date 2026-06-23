using Microsoft.Extensions.AI;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace UnityMcpBridge.Tools;

[McpServerToolType]
public static class RenderPrefabPreviewTool
{
    [McpServerTool(Name = "unity_render_prefab_preview"),
     Description("프리팹을 씬에 배치하지 않고 격리된 환경에서 고품질 미리보기 이미지로 렌더링합니다 (Play Mode 불필요)")]
    public static async Task<IEnumerable<AIContent>> Execute(
        UnityConnection connection,
        [Description("프리팹 에셋 경로 (예: 'Assets/Prefabs/Player.prefab')")] string? assetPath = null,
        [Description("프리팹 에셋 GUID (assetPath 대신 사용 가능)")] string? assetGuid = null,
        [Description("렌더 너비 (기본 512, 32~4096)")] int width = 512,
        [Description("렌더 높이 (기본 512, 32~4096)")] int height = 512,
        [Description("JPEG 품질 (1-100, 0이면 PNG)")] int quality = 75,
        [Description("최대 너비 (0이면 렌더 크기 유지)")] int maxWidth = 0,
        [Description("카메라 Y축 회전 각도 (기본 30, 3/4 시점)")] float yaw = 30,
        [Description("카메라 X축 상하 각도 (기본 20)")] float pitch = 20,
        [Description("카메라 시야각 FOV (기본 30)")] float fov = 30,
        CancellationToken ct = default)
    {
        var paramDict = new Dictionary<string, object?>
        {
            ["width"] = width,
            ["height"] = height,
            ["quality"] = quality,
            ["maxWidth"] = maxWidth,
            ["yaw"] = yaw,
            ["pitch"] = pitch,
            ["fov"] = fov,
        };
        if (assetPath != null) paramDict["assetPath"] = assetPath;
        if (assetGuid != null) paramDict["assetGuid"] = assetGuid;

        using var paramsJson = JsonDocument.Parse(JsonSerializer.Serialize(paramDict));
        var result = await connection.SendRequestAsync("unity_render_prefab_preview", paramsJson.RootElement, ct);

        var root = result.RootElement;
        if (root.TryGetProperty("success", out var s) && !s.GetBoolean())
            return [new TextContent($"Error: {root.GetProperty("error").GetString()}")];

        var data = root.GetProperty("data");
        var base64 = data.GetProperty("imageBase64").GetString()!;
        var mimeType = data.GetProperty("mimeType").GetString()!;
        var imgWidth = data.GetProperty("width").GetInt32();
        var imgHeight = data.GetProperty("height").GetInt32();

        var imageBytes = Convert.FromBase64String(base64);
        return [
            new DataContent(imageBytes, mimeType),
            new TextContent($"{imgWidth}x{imgHeight}")
        ];
    }
}
