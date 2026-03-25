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

            RepaintGameView();
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            EditorApplication.QueuePlayerLoopUpdate();

            await MainThreadDispatcher.DelayFrames(1);

            const int maxRetries = 3;
            Texture2D tex = null;
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                tex = ScreenCapture.CaptureScreenshotAsTexture();
                if (tex != null) break;

                RepaintGameView();
                EditorApplication.QueuePlayerLoopUpdate();
                await MainThreadDispatcher.DelayFrames(1);
            }

            if (tex == null)
                throw new Exception("스크린샷 캡처에 실패했습니다. Game View가 활성화되어 있는지 확인하세요.");

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

        private static void RepaintGameView()
        {
            var gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            if (gameViewType == null) return;

            var gameView = EditorWindow.GetWindow(gameViewType, false, null, false);
            if (gameView != null)
                gameView.Repaint();
        }
    }
}
