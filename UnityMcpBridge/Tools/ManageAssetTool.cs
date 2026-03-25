using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace UnityMcpBridge.Tools;

[McpServerToolType]
public static class ManageAssetTool
{
    [McpServerTool(Name = "unity_manage_asset"), Description("에셋을 이동, 복사, 삭제하거나 폴더를 생성합니다")]
    public static async Task<string> Execute(
        UnityConnection connection,
        [Description("수행할 작업: 'move', 'copy', 'delete', 'create_folder'")] string action,
        [Description("대상 에셋 경로 (예: 'Assets/Materials/Red.mat')")] string assetPath,
        [Description("move/copy 시 목적지 경로 (예: 'Assets/NewFolder/Red.mat')")] string? destinationPath = null,
        CancellationToken ct = default)
    {
        var paramsObj = new { action, assetPath, destinationPath };
        var paramsJson = JsonDocument.Parse(JsonSerializer.Serialize(paramsObj));
        var result = await connection.SendRequestAsync("unity_manage_asset", paramsJson.RootElement, ct);

        return ResponseFormatter.Format(result);
    }
}
