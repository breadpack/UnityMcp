using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace UnityMcpBridge.Tools;

[McpServerToolType]
public static class AnimationClipTool
{
    [McpServerTool(Name = "unity_animation_clip"),
     Description(
         "AnimationClip 자산을 생성·조회·편집합니다 (Edit Mode 전용). " +
         "action: create(신규 클립), get_info(길이·frameRate·loop·커브 바인딩 목록), " +
         "get_curve(단일 커브의 키프레임 조회), set_curve(커브 생성/교체), remove_curve(커브 삭제), " +
         "set_settings(loopTime/loopBlend/frameRate 변경), " +
         "sample(Play Mode 없이 특정 시각의 포즈를 GameObject에 적용 — 스크린샷 검증용), " +
         "stop_sample(sample으로 적용된 상태를 되돌림). " +
         "커브는 EditorCurveBinding 기준(targetPath+componentType+propertyPath), 프로퍼티명은 " +
         "Unity 직렬화 이름(m_Alpha, m_AnchoredPosition.x, m_LocalScale.x, m_IsActive 등)을 그대로 쓴다. " +
         "ObjectReference 커브(스프라이트 교체 등) 쓰기는 미지원 — get_info 조회만 가능.")]
    public static async Task<string> Execute(
        UnityConnection connection,
        [Description("액션: create, get_info, get_curve, set_curve, remove_curve, set_settings, sample, stop_sample")] string action,
        [Description("클립 에셋 경로 (예: \"Assets/.../NKM_UI_X_INTRO.anim\")")] string? assetPath = null,
        [Description("클립 에셋 GUID (assetPath 대신 사용 가능)")] string? assetGuid = null,
        [Description("커브 대상 GameObject의 클립 루트 기준 상대 경로 (루트 자신은 \"\" 또는 생략)")] string? targetPath = null,
        [Description("커브 대상 타입명. Component 파생 타입(CanvasGroup, RectTransform 등) 또는 \"GameObject\"(활성 토글 m_IsActive용)")] string? componentType = null,
        [Description("커브 프로퍼티 경로 (예: \"m_Alpha\", \"m_AnchoredPosition.x\", \"m_LocalScale.y\", \"m_IsActive\")")] string? propertyPath = null,
        [Description("set_curve 키프레임 배열 JSON. 예: [{\"time\":0,\"value\":0},{\"time\":0.5,\"value\":1}]. 항목별 inTangent/outTangent 지정 가능(지정 시 Free 탄젠트로 적용)")] string? keys = null,
        [Description("set_curve 탄젠트 모드(키에 명시적 탄젠트가 없을 때 적용): auto(기본, ClampedAuto), linear, constant")] string? tangentMode = null,
        [Description("create 시 프레임레이트 (기본 60)")] float? frameRate = null,
        [Description("create/set_settings 시 루프 여부")] bool? loopTime = null,
        [Description("set_settings 시 loopBlend")] bool? loopBlend = null,
        [Description("sample 대상 GameObject 경로")] string? path = null,
        [Description("sample 대상 GameObject InstanceID")] int? instanceId = null,
        [Description("sample 시각(초)")] float? time = null,
        CancellationToken ct = default)
    {
        var paramDict = new Dictionary<string, object?> { ["action"] = action };
        if (assetPath != null) paramDict["assetPath"] = assetPath;
        if (assetGuid != null) paramDict["assetGuid"] = assetGuid;
        if (targetPath != null) paramDict["targetPath"] = targetPath;
        if (componentType != null) paramDict["componentType"] = componentType;
        if (propertyPath != null) paramDict["propertyPath"] = propertyPath;
        if (keys != null) paramDict["keys"] = JsonSerializer.Deserialize<object>(keys);
        if (tangentMode != null) paramDict["tangentMode"] = tangentMode;
        if (frameRate != null) paramDict["frameRate"] = frameRate;
        if (loopTime != null) paramDict["loopTime"] = loopTime;
        if (loopBlend != null) paramDict["loopBlend"] = loopBlend;
        if (path != null) paramDict["path"] = path;
        if (instanceId != null) paramDict["instanceId"] = instanceId;
        if (time != null) paramDict["time"] = time;

        using var paramsJson = JsonDocument.Parse(JsonSerializer.Serialize(paramDict));
        var result = await connection.SendRequestAsync("unity_animation_clip", paramsJson.RootElement, ct);
        return ResponseFormatter.Format(result);
    }
}
