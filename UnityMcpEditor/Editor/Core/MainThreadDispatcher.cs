using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;

namespace BreadPack.Mcp.Unity
{
    /// <summary>
    /// 백그라운드(TCP) 스레드의 작업을 Unity 메인 스레드에서 실행한다.
    ///
    /// 메인 스레드 펌프(<see cref="ProcessQueue"/>)를 <see cref="EditorApplication.update"/> 에만
    /// 묶어두면, Editor 가 OS 포커스를 잃었을 때 update tick 이 throttle 되어 큐 드레인이 지연되고
    /// 요청이 타임아웃까지 블록된다. 이를 완화하기 위해 enqueue 시 다음을 함께 수행한다(요청 구동이라
    /// busy-spin 없음 — 큐에 작업이 있을 때만 호출됨):
    ///   (a) <see cref="EditorApplication.QueuePlayerLoopUpdate"/> 로 Unity 가 곧 loop 를 돌리도록 능동 유도(best-effort),
    ///   (b) 메인 스레드 <see cref="SynchronizationContext"/> 로 드레인 콜백을 Post 해 정확히 마샬링,
    ///   (c) 영구 <see cref="EditorApplication.update"/> 훅을 fallback 으로 유지.
    /// 이 조합은 CoplayDev/unity-mcp(MCPForUnity) 의 TransportCommandDispatcher 가 동일 문제에 대해
    /// 프로덕션에서 쓰는 패턴과 같다. 단일 수단(Post 만/QueuePlayerLoopUpdate 만)으로는 불완전하다 —
    /// Post 는 wake 가 없고(throttle 된 tick 에 의존), QueuePlayerLoopUpdate 는 best-effort 라 단독 보장이 약하다.
    ///
    /// 안전 불변식: Unity API 를 실행하는 <see cref="ProcessQueue"/> 는 메인 스레드에서만 호출된다.
    /// </summary>
    public static class MainThreadDispatcher
    {
        private static readonly ConcurrentQueue<Action> _queue = new();
        private static bool _registered;

        // 메인 스레드 컨텍스트/ID — InitializeOnLoadMethod 가 메인 스레드에서 실행되므로 여기서 캡처.
        // 백그라운드 스레드에서 읽히므로 volatile.
        private static volatile SynchronizationContext _mainCtx;
        private static volatile int _mainThreadId = -1;

        // ProcessQueue 재진입/중복 드레인 가드 (둘 다 메인 스레드라 경합은 없으나 재진입 방지).
        private static int _processingFlag;

        [InitializeOnLoadMethod]
        private static void InitializeOnLoad()
        {
            _registered = false;
            _mainCtx = SynchronizationContext.Current;
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
            EnsureInitialized();
        }

        public static void EnsureInitialized()
        {
            if (_registered) return;
            // 멱등 구독 (도메인 리로드/중복 호출에도 단일 구독 보장).
            EditorApplication.update -= ProcessQueue;
            EditorApplication.update += ProcessQueue;
            _registered = true;
        }

        public static Task<T> RunOnMainThread<T>(Func<T> func)
        {
            var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            _queue.Enqueue(() =>
            {
                try
                {
                    tcs.SetResult(func());
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
            RequestPump();
            return tcs.Task;
        }

        public static Task<T> RunOnMainThread<T>(Func<Task<T>> func)
        {
            var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            _queue.Enqueue(() =>
            {
                try
                {
                    func().ContinueWith(t =>
                    {
                        if (t.IsFaulted) tcs.TrySetException(t.Exception.InnerException ?? t.Exception);
                        else if (t.IsCanceled) tcs.TrySetCanceled();
                        else tcs.TrySetResult(t.Result);
                    });
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            });
            RequestPump();
            return tcs.Task;
        }

        public static Task RunOnMainThread(Action action)
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _queue.Enqueue(() =>
            {
                try
                {
                    action();
                    tcs.SetResult(true);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
            RequestPump();
            return tcs.Task;
        }

        /// <summary>
        /// 지정 프레임 수만큼 대기한다. (UniTask.DelayFrame 대체)
        /// 프레임 카운트 대기는 본질적으로 tick 기반이며 도구 처리 hot path 가 아니므로 update 의존을 유지한다.
        /// </summary>
        public static Task DelayFrames(int frameCount, float timeoutSeconds = 10f)
        {
            var tcs = new TaskCompletionSource<bool>();
            int remaining = frameCount;
            double startTime = EditorApplication.timeSinceStartup;
            void OnUpdate()
            {
                if (--remaining <= 0 || EditorApplication.timeSinceStartup - startTime >= timeoutSeconds)
                {
                    EditorApplication.update -= OnUpdate;
                    tcs.TrySetResult(true);
                    return;
                }
                EditorApplication.QueuePlayerLoopUpdate();
            }
            EditorApplication.update += OnUpdate;
            EditorApplication.QueuePlayerLoopUpdate();
            return tcs.Task;
        }

        /// <summary>
        /// 메인 스레드 펌프를 깨운다. 큐에 작업이 있을 때만(enqueue 직후) 호출 → idle busy-spin 없음.
        /// 안전 불변식: <see cref="ProcessQueue"/> 는 메인 스레드에서만 인라인 호출한다.
        /// </summary>
        private static void RequestPump()
        {
            // (a) best-effort 능동 wake: 포커스 없음/throttle 상태에서도 Unity 가 곧 loop iteration 을
            //     돌리도록 유도. QueuePlayerLoopUpdate 는 thread-safe 가 문서화돼 있지 않아 try/catch 로 감싼다.
            try { EditorApplication.QueuePlayerLoopUpdate(); } catch { /* best-effort */ }

            if (Thread.CurrentThread.ManagedThreadId == _mainThreadId)
            {
                // 메인 스레드: 인라인 드레인 (안전).
                ProcessQueue();
                return;
            }

            var ctx = _mainCtx;
            if (ctx != null)
            {
                // 백그라운드(TCP) 스레드: 메인 스레드 message pump 로 마샬링.
                ctx.Post(_ =>
                {
                    // (b) 메인 스레드에서 한 번 더 능동 wake + 드레인.
                    try { EditorApplication.QueuePlayerLoopUpdate(); } catch { /* best-effort */ }
                    ProcessQueue();
                }, null);
            }
            // ctx 미확보(가드): (a) 의 능동 wake + EditorApplication.update fallback 펌프에 맡긴다.
            // (백그라운드에서 ProcessQueue 를 직접 호출하지 않는다 — 불변식 보존.)
        }

        private static void ProcessQueue()
        {
            // 재진입/중복 드레인 방지. 둘 다 메인 스레드라 경합은 없으나, action 이 동기적으로
            // RunOnMainThread 를 다시 호출하는 재진입을 차단한다(큐 아이템은 ConcurrentQueue 에 남아
            // 바깥 while 루프 또는 다음 pump 가 반드시 드레인하므로 유실 없음).
            if (Interlocked.Exchange(ref _processingFlag, 1) == 1)
                return;
            try
            {
                while (_queue.TryDequeue(out var action))
                {
                    action();
                }
            }
            finally
            {
                Interlocked.Exchange(ref _processingFlag, 0);
            }
        }
    }
}
