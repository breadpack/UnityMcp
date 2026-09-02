using System;
using System.Diagnostics;
using System.Reflection;
using UnityEditor;

namespace BreadPack.Mcp.Unity
{
    /// <summary>
    /// 비포커스 Editor 가 계속 tick 하도록 강제하는 옵트인 펌프. Unity 는 창이 백그라운드면
    /// EditorApplication.update 를 throttle 하므로, 에이전트가 요청한 컴파일·테스트·메인 스레드 큐가
    /// 창을 클릭할 때까지 멈춘 것처럼 보인다. 켜면 매 update 마다 내부 API
    /// <c>EditorApplication.SignalTick()</c> 을 호출해 다음 tick 을 즉시 예약한다
    /// (com.unity.pipeline 의 set_autotick 과 같은 방식). 리플렉션 실패 시 비활성 상태로 남는다.
    ///
    /// 정적 구독은 도메인 리로드로 사라지므로 켜짐 여부·간격을 SessionState 에 저장하고
    /// <see cref="RestoreFromSession"/> 으로 서버 재기동 때마다 복원한다. 전체 속도 tick 은 포커스된
    /// Editor 만큼 CPU 를 쓰므로 기본은 꺼짐이다.
    /// </summary>
    public static class EditorAutoTick
    {
        public const int DefaultIntervalMs = 16;

        private const string EnabledKey = "UnityMcp_AutoTick_Enabled";
        private const string IntervalKey = "UnityMcp_AutoTick_IntervalMs";

        private static Action _signalTick;
        private static EditorApplication.CallbackFunction _pump;
        private static readonly Stopwatch _stopwatch = new Stopwatch();
        private static volatile bool _enabled;
        private static int _intervalMs;

        public static bool IsEnabled => _enabled;
        public static int IntervalMs => _intervalMs;

        /// <summary>메인 스레드에서 호출. 성공 시 (enabled, message), 실패 시 message 에 사유.</summary>
        public static (bool ok, string message) Set(bool enable, int intervalMs = DefaultIntervalMs, bool persist = true)
        {
            var (ok, message) = Apply(enable, intervalMs);
            if (ok && persist)
            {
                SessionState.SetBool(EnabledKey, _enabled);
                SessionState.SetInt(IntervalKey, _intervalMs);
            }
            return (ok, message);
        }

        /// <summary>세션에서 마지막으로 명시한 설정을 복원한다. 설정한 적이 없으면 아무것도 하지 않는다.</summary>
        public static void RestoreFromSession()
        {
            var interval = SessionState.GetInt(IntervalKey, -1);
            if (interval < 0) return;
            Apply(SessionState.GetBool(EnabledKey, false), interval);
        }

        private static (bool ok, string message) Apply(bool enable, int intervalMs)
        {
            intervalMs = Math.Max(0, intervalMs);

            if (!enable)
            {
                if (_pump != null)
                {
                    EditorApplication.update -= _pump;
                    _pump = null;
                }
                _stopwatch.Stop();
                var was = _enabled;
                _enabled = false;
                return (true, was ? "Auto-tick disabled" : "Auto-tick already disabled");
            }

            if (_enabled)
            {
                _intervalMs = intervalMs;
                return (true, $"Auto-tick already enabled (interval updated to {_intervalMs}ms)");
            }

            if (_signalTick == null)
            {
                var method = typeof(EditorApplication).GetMethod(
                    "SignalTick", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                if (method == null)
                    return (false, "EditorApplication.SignalTick not found (internal API may have changed)");
                try
                {
                    _signalTick = (Action)Delegate.CreateDelegate(typeof(Action), method);
                }
                catch (Exception ex)
                {
                    return (false, $"could not bind EditorApplication.SignalTick: {ex.Message}");
                }
            }

            _intervalMs = intervalMs;
            _stopwatch.Restart();
            _pump = () =>
            {
                if (_intervalMs <= 0 || _stopwatch.ElapsedMilliseconds >= _intervalMs)
                {
                    _stopwatch.Restart();
                    _signalTick();
                }
            };
            EditorApplication.update += _pump;
            _enabled = true;
            return (true, $"Auto-tick enabled (interval {_intervalMs}ms)");
        }
    }
}
