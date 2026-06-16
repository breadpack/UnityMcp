using System.ComponentModel;
using ModelContextProtocol.Server;

namespace UnityMcpBridge;

/// <summary>
/// Bridge(이 프로세스) 자신을 최신 릴리스로 갱신한다.
///
/// Claude Code 는 세션 중 MCP 서버 hot-reload 를 지원하지 않으므로(#46426, not planned),
/// 프로세스를 교체하지 않고는 새 바이너리를 적용할 수 없다. 이 도구는 그 제약을 우회한다:
///   1) 약속된 종료 코드(<see cref="UpdateExitCode"/>)로 프로세스를 종료하고,
///   2) 런처(run-bridge.js)가 그 코드를 보고 GitHub 최신 바이너리를 받아 재spawn 하며,
///   3) Claude Code 는 stdio 재연결로 새 프로세스에 자동으로 다시 붙는다.
///
/// 실제 다운로드/교체는 런처가 담당한다(파일 잠금·플랫폼별 압축 해제 로직이 이미 거기 있음).
/// 여기서는 "업데이트 후 재시작" 신호만 낸다.
/// </summary>
[McpServerToolType]
public static class SelfUpdateTool
{
    // run-bridge.js 와 약속된 "업데이트 후 재시작" 종료 코드. 양쪽을 함께 바꿔야 한다.
    private const int UpdateExitCode = 42;

    // 응답이 stdout 으로 flush 될 시간. 즉시 Exit 하면 이 응답이 유실되어
    // 클라이언트가 도구 실패로 인식한다.
    private const int FlushDelayMs = 500;

    [McpServerTool(Name = "unity_bridge_self_update"),
     Description("UnityMcpBridge 를 GitHub 최신 릴리스로 갱신하고 재시작합니다 (Claude 세션 재시작 불필요). " +
                 "다운로드/교체는 런처가 수행합니다. 이 도구 호출 직후 Bridge 가 잠시 끊겼다가 " +
                 "최신 버전으로 자동 재연결되므로, 다음 도구 호출은 정상 동작합니다. " +
                 "이미 최신이면 동일 버전으로 재시작만 됩니다.")]
    public static string SelfUpdate()
    {
        // 응답 반환 → 프레임워크가 stdout 으로 전송 → 그 직후 종료(런처가 갱신·재spawn).
        _ = Task.Run(async () =>
        {
            await Task.Delay(FlushDelayMs);
            Environment.Exit(UpdateExitCode);
        });

        return "UnityMcpBridge 업데이트를 시작합니다. 잠시 후 최신 버전으로 재시작되며, "
             + "다음 도구 호출 시 자동으로 재연결됩니다.";
    }
}
