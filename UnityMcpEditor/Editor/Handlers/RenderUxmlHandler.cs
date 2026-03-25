using System;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace BreadPack.Mcp.Unity
{
    public class RenderUxmlHandler : IAsyncRequestHandler
    {
        public string ToolName => "unity_render_uxml";

        public async Task<object> HandleAsync(JObject @params)
        {
            var uxmlPath = @params?["uxmlPath"]?.Value<string>();
            if (string.IsNullOrEmpty(uxmlPath))
                throw new ArgumentException("uxmlPath is required");

            int width = @params?["width"]?.Value<int>() ?? 0;
            int height = @params?["height"]?.Value<int>() ?? 0;
            int quality = @params?["quality"]?.Value<int>() ?? 75;

            var visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);
            if (visualTreeAsset == null)
                throw new ArgumentException($"VisualTreeAsset not found: {uxmlPath}");

            var panelSettings = FindProjectPanelSettings();
            int refWidth = panelSettings?.referenceResolution.x ?? 1080;
            int refHeight = panelSettings?.referenceResolution.y ?? 1920;

            // 해상도 미지정 시 PanelSettings referenceResolution을 그대로 사용 (scale 1.0)
            bool customSize = width > 0 || height > 0;
            if (!customSize)
            {
                width = refWidth;
                height = refHeight;
            }
            else
            {
                if (width <= 0) width = refWidth;
                if (height <= 0) height = refHeight;
            }

            if (width < 50 || height < 50)
                throw new ArgumentException($"해상도가 너무 작습니다 ({width}x{height}). 최소 50x50 이상이어야 합니다.");

            float scale = customSize
                ? Mathf.Min((float)width / refWidth, (float)height / refHeight)
                : 1f;

            // EditorWindow 기반 렌더링 — Editor 모드에서도 동작
            var window = UxmlRenderWindow.Create(visualTreeAsset, width, height, refWidth, refHeight, scale);

            try
            {
                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();

                for (int i = 0; i < 10; i++)
                {
                    window.Repaint();
                    EditorApplication.QueuePlayerLoopUpdate();
                    await MainThreadDispatcher.DelayFrames(2);
                }

                var tex = CaptureWindow(window, width, height);
                if (tex == null)
                    throw new Exception("EditorWindow 캡처에 실패했습니다");

                try
                {
                    byte[] bytes = quality > 0 ? tex.EncodeToJPG(quality) : tex.EncodeToPNG();
                    string mimeType = quality > 0 ? "image/jpeg" : "image/png";

                    return new
                    {
                        imageBase64 = Convert.ToBase64String(bytes),
                        mimeType,
                        width,
                        height
                    };
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(tex);
                }
            }
            finally
            {
                window.Close();
            }
        }

        private static Texture2D CaptureWindow(EditorWindow window, int width, int height)
        {
            // ReadScreenPixel로 에디터 윈도우 영역의 화면 픽셀을 읽음
            var pos = window.position.position;
            var pixels = UnityEditorInternal.InternalEditorUtility.ReadScreenPixel(pos, width, height);

            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        private static PanelSettings FindProjectPanelSettings()
        {
            var guids = AssetDatabase.FindAssets("t:PanelSettings");
            return guids
                .Select(g => AssetDatabase.LoadAssetAtPath<PanelSettings>(
                    AssetDatabase.GUIDToAssetPath(g)))
                .FirstOrDefault(ps => ps != null);
        }
    }

    /// <summary>
    /// UXML 렌더링 전용 임시 EditorWindow.
    /// Editor 패널은 런타임 패널과 달리 Editor 모드에서도 렌더링됨.
    /// </summary>
    public class UxmlRenderWindow : EditorWindow
    {
        private VisualTreeAsset _visualTreeAsset;
        private int _refWidth;
        private int _refHeight;
        private float _scale;

        public static UxmlRenderWindow Create(VisualTreeAsset vta, int width, int height,
            int refWidth, int refHeight, float scale)
        {
            var window = CreateInstance<UxmlRenderWindow>();
            window._visualTreeAsset = vta;
            window._refWidth = refWidth;
            window._refHeight = refHeight;
            window._scale = scale;
            window.minSize = new Vector2(width, height);
            window.maxSize = new Vector2(width, height);

            window.ShowPopup();
            window.position = new Rect(0, 0, width, height);

            return window;
        }

        private void CreateGUI()
        {
            if (_visualTreeAsset == null) return;

            rootVisualElement.Clear();
            rootVisualElement.style.overflow = Overflow.Hidden;
            rootVisualElement.style.backgroundColor = new Color(0, 0, 0, 1);

            // referenceResolution 크기의 컨테이너를 작성하고 scale로 축소
            var container = new VisualElement();
            container.style.width = _refWidth;
            container.style.height = _refHeight;
            container.style.transformOrigin = new TransformOrigin(0, 0);
            container.transform.scale = new Vector3(_scale, _scale, 1);

            _visualTreeAsset.CloneTree(container);
            rootVisualElement.Add(container);
        }
    }
}
