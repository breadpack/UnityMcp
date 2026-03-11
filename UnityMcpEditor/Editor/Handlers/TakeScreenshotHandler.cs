using System;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace UnityMcp.Editor
{
    public class TakeScreenshotHandler : IAsyncRequestHandler
    {
        public string ToolName => "unity_take_screenshot";

        public async UniTask<object> HandleAsync(JObject @params)
        {
            if (!EditorApplication.isPlaying)
                throw new Exception("Play Mode에서만 스크린샷을 캡처할 수 있습니다");

            int quality = @params?["quality"]?.Value<int>() ?? 75;

            // Game View를 강제로 Repaint하여 최신 프레임이 렌더링되도록 함
            RepaintGameView();

            // 렌더링이 완료될 때까지 프레임 끝 대기
            await UniTask.WaitForEndOfFrame();

            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            if (tex == null)
                throw new Exception("스크린샷 캡처에 실패했습니다. Game View가 활성화되어 있는지 확인하세요.");

            try
            {
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
