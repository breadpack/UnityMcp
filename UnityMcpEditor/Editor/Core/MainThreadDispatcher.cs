using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using UnityEditor;

namespace BreadPack.Mcp.Unity
{
    /// <summary>
    /// EditorApplication.update를 사용하여 백그라운드 스레드의 작업을 메인 스레드에서 실행합니다.
    /// UniTask.SwitchToMainThread()를 대체합니다.
    /// </summary>
    public static class MainThreadDispatcher
    {
        private static readonly ConcurrentQueue<Action> _queue = new();
        private static bool _registered;

        [InitializeOnLoadMethod]
        private static void InitializeOnLoad()
        {
            _registered = false;
            EnsureInitialized();
        }

        public static void EnsureInitialized()
        {
            if (_registered) return;
            EditorApplication.update += ProcessQueue;
            _registered = true;
        }

        public static Task<T> RunOnMainThread<T>(Func<T> func)
        {
            var tcs = new TaskCompletionSource<T>();
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
            EditorApplication.QueuePlayerLoopUpdate();
            return tcs.Task;
        }

        public static Task<T> RunOnMainThread<T>(Func<Task<T>> func)
        {
            var tcs = new TaskCompletionSource<T>();
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
            EditorApplication.QueuePlayerLoopUpdate();
            return tcs.Task;
        }

        public static Task RunOnMainThread(Action action)
        {
            var tcs = new TaskCompletionSource<bool>();
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
            EditorApplication.QueuePlayerLoopUpdate();
            return tcs.Task;
        }

        /// <summary>
        /// 지정 프레임 수만큼 대기합니다. (UniTask.DelayFrame 대체)
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

        private static void ProcessQueue()
        {
            while (_queue.TryDequeue(out var action))
            {
                action();
            }

            if (!_queue.IsEmpty)
            {
                EditorApplication.QueuePlayerLoopUpdate();
            }
        }
    }
}
