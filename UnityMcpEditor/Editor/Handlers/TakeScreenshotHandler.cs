using System;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace BreadPack.Mcp.Unity
{
    public class TakeScreenshotHandler : IAsyncRequestHandler
    {
        public string ToolName => "unity_take_screenshot";

        public async Task<object> HandleAsync(JObject @params)
        {
            if (!EditorApplication.isPlaying)
                throw new Exception("Play Mode에서만 스크린샷을 캡처할 수 있습니다");

            int quality = @params?["quality"]?.Value<int>() ?? 75;
            int maxWidth = @params?["maxWidth"]?.Value<int>() ?? 0;

            // Game View 가 비포커스/숨김 상태면 frame 이 안 그려져 ScreenCapture.CaptureScreenshotAsTexture 가
            // "Was method called before end of frame" throw. 강제 포커스 + 충분한 frame wait + 점진적
            // retry 로 우회. 실제 캡처는 메인 스레드에서 직접 호출 (CaptureScreenshotAsTexture 가
            // 다음 EndOfFrame 까지 기다리는 sync API 라 Repaint+QueuePlayerLoopUpdate 후 wait 필수).
            FocusGameView();
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            EditorApplication.QueuePlayerLoopUpdate();

            await MainThreadDispatcher.DelayFrames(3);

            const int maxRetries = 4;
            Texture2D tex = null;
            Exception lastEx = null;
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try { tex = ScreenCapture.CaptureScreenshotAsTexture(); }
                catch (Exception e) { lastEx = e; tex = null; }
                if (tex != null && tex.width > 0 && tex.height > 0) break;
                if (tex != null) { UnityEngine.Object.DestroyImmediate(tex); tex = null; }

                FocusGameView();
                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                EditorApplication.QueuePlayerLoopUpdate();
                await MainThreadDispatcher.DelayFrames(2 + attempt); // 진행적 wait: 2, 3, 4, 5
            }

            if (tex == null)
                throw new Exception($"스크린샷 캡처에 실패했습니다 (Game View 가 백그라운드일 가능성). last={lastEx?.Message ?? "(no exception)"}");

            try
            {
                if (maxWidth > 0 && tex.width > maxWidth)
                {
                    float ratio = (float)maxWidth / tex.width;
                    int newHeight = Mathf.RoundToInt(tex.height * ratio);
                    var resized = new Texture2D(maxWidth, newHeight);
                    var rt = RenderTexture.GetTemporary(maxWidth, newHeight);
                    Graphics.Blit(tex, rt);
                    RenderTexture.active = rt;
                    resized.ReadPixels(new Rect(0, 0, maxWidth, newHeight), 0, 0);
                    resized.Apply();
                    RenderTexture.active = null;
                    RenderTexture.ReleaseTemporary(rt);
                    UnityEngine.Object.DestroyImmediate(tex);
                    tex = resized;
                }

                byte[] bytes = quality > 0 ? tex.EncodeToJPG(quality) : tex.EncodeToPNG();
                string mimeType = quality > 0 ? "image/jpeg" : "image/png";
                return new
                {
                    imageBase64 = Convert.ToBase64String(bytes),
                    mimeType,
                    width = tex.width,
                    height = tex.height
                };
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tex);
            }
        }

        private static void FocusGameView()
        {
            // GameView 를 강제 포커스해 player loop 가 frame 을 그리도록 유도. 비포커스 상태에선
            // ScreenCapture.CaptureScreenshotAsTexture 가 frame-end 동기화에 실패해 throw 한다.
            var gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            if (gameViewType == null) return;

            var gameView = EditorWindow.GetWindow(gameViewType, false, null, false);
            if (gameView == null) return;

            try { gameView.Show(); } catch { /* ignore — already shown */ }
            try { gameView.Focus(); } catch { /* ignore */ }
            gameView.Repaint();
        }
    }
}
