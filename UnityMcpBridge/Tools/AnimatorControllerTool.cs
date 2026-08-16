using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace UnityMcpBridge.Tools;

[McpServerToolType]
public static class AnimatorControllerTool
{
    [McpServerTool(Name = "unity_animator_controller"),
     Description(
         "AnimatorController 자산을 생성·조회·편집합니다 (Edit Mode 전용). " +
         "action: create, get_info(레이어·상태·전이·파라미터 전체 구조 조회), " +
         "add_state/remove_state/set_state_motion, add_transition/remove_transition(from=\"AnyState\" 가능), " +
         "add_parameter/remove_parameter, assign(GameObject의 Animator에 컨트롤러 연결). " +
         "unity_animation_clip으로 만든 클립을 motionPath로 상태에 연결해 쓴다. layerIndex 생략 시 0(기본 레이어).")]
    public static async Task<string> Execute(
        UnityConnection connection,
        [Description("액션: create, get_info, add_state, remove_state, set_state_motion, add_transition, remove_transition, add_parameter, remove_parameter, assign")] string action,
        [Description("컨트롤러 에셋 경로 (예: \"Assets/.../NKM_UI_X_AC.controller\")")] string? assetPath = null,
        [Description("컨트롤러 에셋 GUID (assetPath 대신 사용 가능)")] string? assetGuid = null,
        [Description("레이어 인덱스 (기본 0)")] int? layerIndex = null,
        [Description("상태 이름 (add_state/remove_state/set_state_motion)")] string? name = null,
        [Description("상태에 연결할 AnimationClip 경로 (add_state/set_state_motion)")] string? motionPath = null,
        [Description("add_state 시 기본 상태로 지정할지 여부")] bool? isDefault = null,
        [Description("전이 시작 상태 이름, 또는 \"AnyState\" (add_transition/remove_transition)")] string? from = null,
        [Description("전이 도착 상태 이름 (add_transition/remove_transition)")] string? to = null,
        [Description("add_transition: ExitTime 기반 자동 전이 여부")] bool? hasExitTime = null,
        [Description("add_transition: hasExitTime=true일 때 진행률(0~1)")] float? exitTime = null,
        [Description("add_transition: 크로스페이드 duration(초)")] float? duration = null,
        [Description("파라미터 이름 (add_parameter/remove_parameter)")] string? parameterName = null,
        [Description("파라미터 타입: trigger, bool, int, float (add_parameter)")] string? parameterType = null,
        [Description("assign 대상 GameObject 경로")] string? path = null,
        [Description("assign 대상 GameObject InstanceID")] int? instanceId = null,
        CancellationToken ct = default)
    {
        var paramDict = new Dictionary<string, object?> { ["action"] = action };
        if (assetPath != null) paramDict["assetPath"] = assetPath;
        if (assetGuid != null) paramDict["assetGuid"] = assetGuid;
        if (layerIndex != null) paramDict["layerIndex"] = layerIndex;
        if (name != null) paramDict["name"] = name;
        if (motionPath != null) paramDict["motionPath"] = motionPath;
        if (isDefault != null) paramDict["isDefault"] = isDefault;
        if (from != null) paramDict["from"] = from;
        if (to != null) paramDict["to"] = to;
        if (hasExitTime != null) paramDict["hasExitTime"] = hasExitTime;
        if (exitTime != null) paramDict["exitTime"] = exitTime;
        if (duration != null) paramDict["duration"] = duration;
        if (parameterName != null) paramDict["parameterName"] = parameterName;
        if (parameterType != null) paramDict["parameterType"] = parameterType;
        if (path != null) paramDict["path"] = path;
        if (instanceId != null) paramDict["instanceId"] = instanceId;

        using var paramsJson = JsonDocument.Parse(JsonSerializer.Serialize(paramDict));
        var result = await connection.SendRequestAsync("unity_animator_controller", paramsJson.RootElement, ct);
        return ResponseFormatter.Format(result);
    }
}
