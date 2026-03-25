using System;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace BreadPack.Mcp.Unity
{
    public class TakeSceneViewScreenshotHandler : IRequestHandler
    {
        public string ToolName => "unity_take_scene_view_screenshot";

        public object Handle(JObject @params)
        {
            int quality = @params?["quality"]?.Value<int>() ?? 75;
            int maxWidth = @params?["maxWidth"]?.Value<int>() ?? 0;

            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null)
                throw new Exception("Scene View가 열려있지 않습니다");

            sceneView.Repaint();

            var camera = sceneView.camera;
            int width = (int)sceneView.position.width;
            int height = (int)sceneView.position.height;

            var rt = new RenderTexture(width, height, 24);
            camera.targetTexture = rt;
            camera.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();

            camera.targetTexture = null;
            RenderTexture.active = null;
            UnityEngine.Object.DestroyImmediate(rt);

            try
            {
                if (maxWidth > 0 && tex.width > maxWidth)
                {
                    float ratio = (float)maxWidth / tex.width;
                    int newHeight = Mathf.RoundToInt(tex.height * ratio);
                    var resized = new Texture2D(maxWidth, newHeight);
                    var resizeRT = RenderTexture.GetTemporary(maxWidth, newHeight);
                    Graphics.Blit(tex, resizeRT);
                    RenderTexture.active = resizeRT;
                    resized.ReadPixels(new Rect(0, 0, maxWidth, newHeight), 0, 0);
                    resized.Apply();
                    RenderTexture.active = null;
                    RenderTexture.ReleaseTemporary(resizeRT);
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
    }
}
