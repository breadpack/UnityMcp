using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace BreadPack.Mcp.Unity
{
    /// <summary>
    /// Game View 캡처의 유일한 진입점.
    /// EditorApplication.update tick을 프레임 종료로 간주하지 않고, PlayerLoop의
    /// WaitForEndOfFrame 이후 ScreenCapture를 호출한다.
    /// </summary>
    public static class GameViewCaptureService
    {
        private const int MaxAttempts = 2;
        private const float DefaultTimeoutSeconds = 10f;

        private static readonly SemaphoreSlim CaptureGate = new(1, 1);
        private static CaptureAttempt _activeAttempt;

        public static async Task<EncodedImage> CaptureEncodedAsync(
            int quality,
            int maxWidth,
            float timeoutSeconds = DefaultTimeoutSeconds)
        {
            EnsureCaptureState();

            if (timeoutSeconds <= 0f)
                throw new ArgumentOutOfRangeException(nameof(timeoutSeconds), "캡처 timeout은 0보다 커야 합니다");

            await CaptureGate.WaitAsync();
            try
            {
                Exception lastException = null;
                for (int attempt = 0; attempt < MaxAttempts; attempt++)
                {
                    if (!EditorApplication.isPlaying)
                        throw new InvalidOperationException("Play Mode가 종료되어 스크린샷 캡처를 취소했습니다");

                    try
                    {
                        PrepareGameView();
                        return await RunAttemptAsync(quality, maxWidth, timeoutSeconds);
                    }
                    catch (Exception ex)
                    {
                        if (ex is OperationCanceledException)
                            throw;

                        lastException = ex;
                        if (attempt + 1 < MaxAttempts && EditorApplication.isPlaying)
                        {
                            PrepareGameView();
                        }
                    }
                }

                throw new Exception(
                    $"Game View 스크린샷 캡처에 실패했습니다. " +
                    $"last={lastException?.Message ?? "(no exception)"}; {GetDiagnostics()}",
                    lastException);
            }
            finally
            {
                CaptureGate.Release();
            }
        }

        /// <summary>
        /// 서버 종료/도메인 리로드 시 대기 중인 캡처를 즉시 정리한다.
        /// </summary>
        public static void Shutdown()
        {
            _activeAttempt?.Fail(new OperationCanceledException("Unity MCP 캡처 서비스가 종료되었습니다"));
        }

        private static void EnsureCaptureState()
        {
            if (!EditorApplication.isPlaying)
                throw new InvalidOperationException("Play Mode에서만 스크린샷을 캡처할 수 있습니다");
            if (EditorApplication.isPaused)
                throw new InvalidOperationException("Unity가 일시정지 상태라 EndOfFrame 캡처를 진행할 수 없습니다");
        }

        private static async Task<EncodedImage> RunAttemptAsync(
            int quality,
            int maxWidth,
            float timeoutSeconds)
        {
            var attempt = new CaptureAttempt(quality, maxWidth, timeoutSeconds);
            _activeAttempt = attempt;
            try
            {
                attempt.Start();
                return await attempt.Completion.Task;
            }
            finally
            {
                if (ReferenceEquals(_activeAttempt, attempt))
                    _activeAttempt = null;
                attempt.Dispose();
            }
        }

        private static void PrepareGameView()
        {
            FocusGameView();
            try { UnityEditorInternal.InternalEditorUtility.RepaintAllViews(); }
            catch { /* best effort */ }
            try { EditorApplication.QueuePlayerLoopUpdate(); }
            catch { /* best effort */ }
        }

        private static EditorWindow FocusGameView()
        {
            var gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            if (gameViewType == null) return null;

            var gameView = EditorWindow.GetWindow(gameViewType, false, null, false);
            if (gameView == null) return null;

            try { gameView.Show(); } catch { /* already shown */ }
            try { gameView.Focus(); } catch { /* best effort */ }
            try { gameView.Repaint(); } catch { /* best effort */ }
            return gameView;
        }

        private static IEnumerator CaptureAfterEndOfFrame(CaptureAttempt attempt)
        {
            yield return new WaitForEndOfFrame();

            if (!EditorApplication.isPlaying)
            {
                attempt.Fail(new InvalidOperationException("Play Mode가 종료되어 EndOfFrame 캡처를 취소했습니다"));
                yield break;
            }

            Texture2D texture = null;
            try
            {
                texture = ScreenCapture.CaptureScreenshotAsTexture();
                if (texture == null || texture.width <= 0 || texture.height <= 0)
                    throw new InvalidOperationException("ScreenCapture가 유효한 텍스처를 반환하지 않았습니다");

                var encoded = ImageEncoder.Encode(texture, attempt.Quality, attempt.MaxWidth);
                attempt.Complete(encoded);
            }
            catch (Exception ex)
            {
                attempt.Fail(new Exception(
                    $"EndOfFrame 이후 ScreenCapture가 실패했습니다: {ex.Message}; {GetDiagnostics()}", ex));
            }
            finally
            {
                if (texture != null)
                    UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static string GetDiagnostics()
        {
            var active = RenderTexture.active;
            var activeTarget = active == null ? "null" : $"{active.width}x{active.height}";
            return $"screen={Screen.width}x{Screen.height}, activeRT={activeTarget}, " +
                   $"playing={EditorApplication.isPlaying}, paused={EditorApplication.isPaused}, " +
                   $"compiling={EditorApplication.isCompiling}";
        }

        private sealed class CaptureAttempt : IDisposable
        {
            private readonly float _timeoutSeconds;
            private readonly double _deadline;
            private readonly TaskCompletionSource<EncodedImage> _completion =
                new(TaskCreationOptions.RunContinuationsAsynchronously);

            private Action<PlayModeStateChange> _playModeChanged;
            private GameObject _hostObject;
            private GameViewCaptureRunner _runner;
            private Coroutine _coroutine;
            private bool _disposed;

            public CaptureAttempt(int quality, int maxWidth, float timeoutSeconds)
            {
                Quality = quality;
                MaxWidth = maxWidth;
                _timeoutSeconds = timeoutSeconds;
                _deadline = EditorApplication.timeSinceStartup + timeoutSeconds;
            }

            public int Quality { get; }
            public int MaxWidth { get; }
            public TaskCompletionSource<EncodedImage> Completion => _completion;

            public void Start()
            {
                _hostObject = new GameObject("[UnityMCP] EndOfFrame Capture Runner")
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                UnityEngine.Object.DontDestroyOnLoad(_hostObject);
                _runner = _hostObject.AddComponent<GameViewCaptureRunner>();

                _playModeChanged = OnPlayModeChanged;
                EditorApplication.update += CheckTimeout;
                EditorApplication.playModeStateChanged += _playModeChanged;

                _coroutine = _runner.Begin(CaptureAfterEndOfFrame(this));
            }

            public void Complete(EncodedImage result)
            {
                _completion.TrySetResult(result);
            }

            public void Fail(Exception exception)
            {
                if (_disposed) return;
                if (_coroutine != null && _runner != null)
                    _runner.Cancel(_coroutine);
                _completion.TrySetException(exception);
            }

            private void CheckTimeout()
            {
                if (EditorApplication.timeSinceStartup - _deadline >= 0)
                {
                    Fail(new TimeoutException(
                        $"EndOfFrame에 도달하지 못해 캡처가 시간 초과되었습니다 ({_timeoutSeconds:0.##}초); " +
                        GetDiagnostics()));
                }
            }

            private void OnPlayModeChanged(PlayModeStateChange change)
            {
                if (change == PlayModeStateChange.ExitingPlayMode ||
                    change == PlayModeStateChange.EnteredEditMode)
                {
                    Fail(new OperationCanceledException(
                        $"Play Mode 전환으로 캡처를 취소했습니다 ({change})"));
                }
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;

                EditorApplication.update -= CheckTimeout;
                if (_playModeChanged != null)
                    EditorApplication.playModeStateChanged -= _playModeChanged;

                if (_coroutine != null && _runner != null)
                    _runner.Cancel(_coroutine);
                if (_hostObject != null)
                    UnityEngine.Object.DestroyImmediate(_hostObject);

                _coroutine = null;
                _runner = null;
                _hostObject = null;
            }
        }
    }
}
