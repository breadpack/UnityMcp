using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace BreadPack.Mcp.Unity
{
    /// <summary>
    /// 프리팹을 씬에 인스턴스화하지 않고 PreviewRenderUtility 의 격리된 프리뷰 씬에서
    /// 임의 해상도·각도로 렌더링하여 이미지(base64)로 반환한다. Play Mode 불필요.
    /// </summary>
    public class RenderPrefabPreviewHandler : IRequestHandler
    {
        public string ToolName => "unity_render_prefab_preview";

        public object Handle(JObject @params)
        {
            // 모든 검증·클램프는 BeginStaticPreview 호출 이전에 수행한다.
            // (BeginPreview 이후 예외가 나면 EndPreview 없이 종료돼 에디터가 크래시하는 알려진 이슈가 있다.)
            int width = Mathf.Clamp(@params?["width"]?.Value<int>() ?? 512, 32, 4096);
            int height = Mathf.Clamp(@params?["height"]?.Value<int>() ?? 512, 32, 4096);
            int quality = Mathf.Clamp(@params?["quality"]?.Value<int>() ?? 75, 0, 100);
            int maxWidth = @params?["maxWidth"]?.Value<int>() ?? 0;
            float yaw = @params?["yaw"]?.Value<float>() ?? 30f;
            float pitch = @params?["pitch"]?.Value<float>() ?? 20f;
            float fov = Mathf.Clamp(@params?["fov"]?.Value<float>() ?? 30f, 1f, 179f);

            var asset = AssetResolver.Resolve(@params, "assetPath", "assetGuid");
            if (asset is not GameObject prefab)
                throw new ArgumentException(
                    $"Asset is not a GameObject prefab: {asset?.GetType().Name ?? "null"}");

            PreviewRenderUtility pru = null;
            GameObject instance = null;
            Texture2D tex = null;
            try
            {
                // renderFullScene: true → SRP(URP/HDRP) 라이팅 설정이 프리뷰 씬에 적용된다.
                pru = new PreviewRenderUtility(true);
                pru.cameraFieldOfView = fov;

                instance = pru.InstantiatePrefabInScene(prefab);
                if (instance == null)
                {
                    // 일부 prefab variant 등에서 null 이 반환될 수 있어 fallback.
                    instance = UnityEngine.Object.Instantiate(prefab);
                    pru.AddSingleGO(instance);
                }
                instance.transform.position = Vector3.zero;

                // MeshRenderer / SkinnedMeshRenderer 만 프레이밍 대상. Particle/UI 등은 정적 프리뷰가 무의미.
                var renderers = instance.GetComponentsInChildren<Renderer>(false)
                    .Where(r => r is MeshRenderer || r is SkinnedMeshRenderer)
                    .ToArray();
                if (renderers.Length == 0)
                    throw new InvalidOperationException(
                        "Prefab has no MeshRenderer/SkinnedMeshRenderer to preview");

                Bounds bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                    bounds.Encapsulate(renderers[i].bounds);

                FrameCamera(pru.camera, bounds, yaw, pitch, fov, width, height);

                // PreviewRenderUtility 기본 clearFlags 는 Depth 라 배경이 비어 보인다 → SolidColor 로 명시.
                // 알파 0 → PNG(quality 0) 일 때 투명 배경.
                pru.camera.clearFlags = CameraClearFlags.SolidColor;
                pru.camera.backgroundColor = new Color(0.18f, 0.18f, 0.18f, 0f);

                pru.BeginStaticPreview(new Rect(0, 0, width, height));
                // 위치 인자: (allowScriptableRenderPipeline: true, updatefov: false)
                // SRP 에서 셰이더가 그려지려면 첫 인자가 true 여야 한다. updatefov:false 로 우리가 잡은 프레이밍 유지.
                pru.Render(true, false);
                tex = pru.EndStaticPreview(); // 읽기 가능한 Texture2D(RGB24) 반환

                return ImageEncoder.Encode(tex, quality, maxWidth);
            }
            finally
            {
                if (tex != null) UnityEngine.Object.DestroyImmediate(tex);
                if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
                pru?.Cleanup(); // RenderTexture·프리뷰 씬·카메라 전부 정리 (필수)
            }
        }

        /// <summary>
        /// 외접 구 반경 기준으로 카메라를 자동 프레이밍한다. yaw/pitch 로 시점 각도를 제어한다.
        /// </summary>
        private static void FrameCamera(Camera cam, Bounds bounds, float yaw, float pitch, float fov, int width, int height)
        {
            Vector3 center = bounds.center;
            float radius = bounds.extents.magnitude; // 외접 구 → 회전 각도와 무관하게 항상 화면에 들어옴
            if (radius < 1e-4f) radius = 0.5f;

            float vFov = fov * Mathf.Deg2Rad;
            float aspect = (float)width / height;
            float hFov = 2f * Mathf.Atan(Mathf.Tan(vFov * 0.5f) * aspect);
            float fovToUse = Mathf.Min(vFov, hFov); // 가로/세로 중 좁은 쪽 기준으로 거리를 키운다
            float distance = radius / Mathf.Sin(fovToUse * 0.5f) * 1.25f; // 1.25 = 여백 마진

            Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 dir = rot * Vector3.forward;

            cam.transform.position = center - dir * distance;
            cam.transform.rotation = rot;
            cam.fieldOfView = fov;
            cam.orthographic = false;

            // 기본 near=2 / far=10 은 좁아서 클리핑된다 → 거리에 맞춰 재설정.
            cam.nearClipPlane = Mathf.Max(0.01f, distance - radius * 2f);
            cam.farClipPlane = distance + radius * 2f;
        }
    }
}
