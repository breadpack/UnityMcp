using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace UnityMcpBridge.Tools;

[McpServerToolType]
public static class AddressableAddTool
{
    [McpServerTool(Name = "unity_addressable_add"),
     Description("Asset을 Addressable 그룹에 등록합니다")]
    public static async Task<string> Execute(
        UnityConnection connection,
        [Description("Asset 경로")] string? assetPath = null,
        [Description("Asset GUID")] string? assetGuid = null,
        [Description("Addressable 그룹 이름 (미지정 시 Default Group)")] string? groupName = null,
        [Description("Addressable 주소 (미지정 시 Asset 경로 사용)")] string? address = null,
        CancellationToken ct = default)
    {
        var paramDict = new Dictionary<string, object?>();
        if (assetPath != null) paramDict["assetPath"] = assetPath;
        if (assetGuid != null) paramDict["assetGuid"] = assetGuid;
        if (groupName != null) paramDict["groupName"] = groupName;
        if (address != null) paramDict["address"] = address;

        using var paramsJson = JsonDocument.Parse(JsonSerializer.Serialize(paramDict));
        var result = await connection.SendRequestAsync("unity_addressable_add", paramsJson.RootElement, ct);
        var root = result.RootElement;
        if (root.TryGetProperty("success", out var s) && !s.GetBoolean())
            return $"Error: {root.GetProperty("error").GetString()}";
        return root.GetProperty("data").GetRawText();
    }
}
