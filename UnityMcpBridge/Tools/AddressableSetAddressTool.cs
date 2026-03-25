using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace UnityMcpBridge.Tools;

[McpServerToolType]
public static class AddressableSetAddressTool
{
    [McpServerTool(Name = "unity_addressable_set_address"),
     Description("Addressable Asset의 주소와 레이블을 설정합니다 (Addressable 패키지 필요)")]
    public static async Task<string> Execute(
        UnityConnection connection,
        [Description("설정할 주소")] string address,
        [Description("Asset 경로")] string? assetPath = null,
        [Description("Asset GUID")] string? assetGuid = null,
        [Description("레이블 목록 JSON 배열 (예: [\"label1\",\"label2\"])")] string? labels = null,
        CancellationToken ct = default)
    {
        var paramDict = new Dictionary<string, object?> { ["address"] = address };
        if (assetPath != null) paramDict["assetPath"] = assetPath;
        if (assetGuid != null) paramDict["assetGuid"] = assetGuid;
        if (labels != null) paramDict["labels"] = JsonSerializer.Deserialize<object>(labels);

        using var paramsJson = JsonDocument.Parse(JsonSerializer.Serialize(paramDict));
        var result = await connection.SendRequestAsync("unity_addressable_set_address", paramsJson.RootElement, ct);
        return ResponseFormatter.Format(result);
    }
}
