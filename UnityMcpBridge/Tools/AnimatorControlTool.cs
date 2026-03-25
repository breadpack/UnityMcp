using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace UnityMcpBridge.Tools;

[McpServerToolType]
public static class AnimatorControlTool
{
    [McpServerTool(Name = "unity_animator_control"),
     Description("Animator 파라미터를 설정하거나 상태를 조회합니다 (Play Mode 필요)")]
    public static async Task<string> Execute(
        UnityConnection connection,
        [Description("액션: set_bool, set_int, set_float, set_trigger, reset_trigger, get_parameters, get_current_state")] string action,
        [Description("대상 GameObject 경로")] string? path = null,
        [Description("대상 GameObject InstanceID")] int? instanceId = null,
        [Description("파라미터 이름 (set_* 액션에서 필수)")] string? parameterName = null,
        [Description("값 (set_bool: \"true\"/\"false\", set_int: \"3\", set_float: \"1.5\")")] string? value = null,
        CancellationToken ct = default)
    {
        var paramDict = new Dictionary<string, object?>
        {
            ["action"] = action
        };
        if (path != null) paramDict["path"] = path;
        if (instanceId != null) paramDict["instanceId"] = instanceId;
        if (parameterName != null) paramDict["parameterName"] = parameterName;
        if (value != null) paramDict["value"] = value;

        using var paramsJson = JsonDocument.Parse(JsonSerializer.Serialize(paramDict));
        var result = await connection.SendRequestAsync("unity_animator_control", paramsJson.RootElement, ct);
        return ResponseFormatter.Format(result);
    }
}
