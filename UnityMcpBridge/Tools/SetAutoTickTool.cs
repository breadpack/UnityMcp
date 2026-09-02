using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace UnityMcpBridge.Tools;

[McpServerToolType]
public static class SetAutoTickTool
{
    [McpServerTool(Name = "unity_set_autotick"), Description("Unity Editor 창이 비포커스 상태여도 계속 tick 하도록 강제합니다 (EditorApplication.SignalTick). 비포커스에서 컴파일·테스트·도메인 리로드 후 재연결이 멈춰 보일 때 켭니다. 세션 시작 훅이 기본으로 켜므로 보통 직접 호출할 필요는 없습니다. com.unity.pipeline 이 있으면 `unity command set_autotick` 과 동일합니다")]
    public static async Task<string> Execute(
        UnityConnection connection,
        [Description("켜기(true) / 끄기(false). 기본 true")] bool enable = true,
        [Description("강제 tick 최소 간격(ms). 0 = 매 update (CPU 코어 하나를 점유). 기본 16 (~60Hz)")] int intervalMs = 16,
        [Description("SessionState 에 저장해 도메인 리로드 후에도 유지. 기본 true. 일회성 설정이면 false")] bool persist = true,
        CancellationToken ct = default)
    {
        var paramsJson = JsonSerializer.SerializeToElement(new { enable, intervalMs, persist });
        var result = await connection.SendRequestAsync("unity_set_autotick", paramsJson, ct);
        return ResponseFormatter.Format(result);
    }
}
