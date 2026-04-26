using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;

namespace BreadPack.Mcp.Unity.Input
{
    public sealed class ResolvedTarget
    {
        public TargetKind Kind;
        public Vector2 ScreenPoint;
        public string ResolvedPath; // 디버깅/응답용
        public GameObject GameObject; // null 가능 (직접 좌표인 경우)
    }

    public static class TargetResolver
    {
        public static ResolvedTarget Resolve(TargetSpec spec)
        {
            // 1. 직접 좌표
            if (spec.Position.HasValue)
                return new ResolvedTarget { Kind = TargetKind.Screen, ScreenPoint = spec.Position.Value, ResolvedPath = "(screen)" };

            if (spec.WorldPoint.HasValue)
            {
                var cam = Camera.main ?? Object.FindFirstObjectByType<Camera>();
                if (cam == null)
                    throw new System.Exception("월드 좌표 변환에 사용할 Camera가 없습니다");
                var sp = cam.WorldToScreenPoint(spec.WorldPoint.Value);
                if (sp.z < 0)
                    throw new System.Exception("월드 좌표가 카메라 뒤에 있습니다");
                return new ResolvedTarget { Kind = TargetKind.World, ScreenPoint = new Vector2(sp.x, sp.y), ResolvedPath = $"(world {spec.WorldPoint.Value})" };
            }

            // 2. UI Toolkit
            if (!string.IsNullOrEmpty(spec.VisualElement))
            {
                var ve = ResolveVisualElement(spec.VisualElement);
                var rect = ve.worldBound; // panel coords (already screen-aligned for Screen panels)
                var center = rect.center;
                // UI Toolkit panel coords: y는 위에서 0, Unity screen coords는 아래에서 0 → 변환
                center.y = Screen.height - center.y;
                return new ResolvedTarget { Kind = TargetKind.UiToolkit, ScreenPoint = center, ResolvedPath = $"VE:{spec.VisualElement}" };
            }

            // 3. uGUI / 3D Object (path or instanceId)
            var go = ResolveGameObject(spec);
            if (go == null)
                throw new System.Exception($"GameObject를 찾을 수 없습니다: {spec.Path ?? spec.InstanceId.ToString()}");

            // active 검증
            if (!go.activeInHierarchy)
                throw new System.Exception($"비활성 상태의 오브젝트는 입력을 받을 수 없습니다: {go.name}");

            // RectTransform이면 uGUI 경로
            if (go.GetComponent<RectTransform>() is RectTransform rt && rt.GetComponentInParent<Canvas>() != null)
            {
                var canvas = rt.GetComponentInParent<Canvas>();
                var worldPos = rt.position;
                Vector2 screen;

                switch (canvas.renderMode)
                {
                    case RenderMode.ScreenSpaceOverlay:
                        screen = new Vector2(worldPos.x, worldPos.y);
                        break;
                    case RenderMode.ScreenSpaceCamera:
                    case RenderMode.WorldSpace:
                        var cam = canvas.worldCamera ?? Camera.main;
                        if (cam == null)
                            throw new System.Exception($"Canvas '{canvas.name}'의 worldCamera가 없고 Camera.main도 없습니다");
                        var sp = RectTransformUtility.WorldToScreenPoint(cam, worldPos);
                        screen = sp;
                        break;
                    default:
                        throw new System.Exception($"알 수 없는 Canvas RenderMode: {canvas.renderMode}");
                }

                return new ResolvedTarget { Kind = TargetKind.UGui, ScreenPoint = screen, ResolvedPath = GetHierarchyPath(go), GameObject = go };
            }

            // 4. 3D 오브젝트 (Renderer/Collider bounds 중심)
            {
                var cam = Camera.main ?? Object.FindFirstObjectByType<Camera>();
                if (cam == null)
                    throw new System.Exception("3D 오브젝트 변환에 사용할 Camera가 없습니다");

                Vector3 worldCenter;
                if (go.GetComponent<Renderer>() is Renderer r) worldCenter = r.bounds.center;
                else if (go.GetComponent<Collider>() is Collider c) worldCenter = c.bounds.center;
                else worldCenter = go.transform.position;

                var sp = cam.WorldToScreenPoint(worldCenter);
                if (sp.z < 0)
                    throw new System.Exception($"오브젝트 '{go.name}'가 카메라 뒤에 있습니다");

                return new ResolvedTarget { Kind = TargetKind.World, ScreenPoint = new Vector2(sp.x, sp.y), ResolvedPath = GetHierarchyPath(go), GameObject = go };
            }
        }

        private static GameObject ResolveGameObject(TargetSpec spec)
        {
            if (spec.InstanceId.HasValue)
            {
                var obj = UnityEditor.EditorUtility.InstanceIDToObject(spec.InstanceId.Value);
                return obj as GameObject;
            }

            if (string.IsNullOrEmpty(spec.Path))
                return null;

            // Path 기반: "/"로 분리. 첫 segment는 루트 GameObject 이름
            var segments = spec.Path.Split('/');
            var roots = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None)
                .Where(g => g.transform.parent == null && g.name == segments[0])
                .ToList();

            var matches = new List<GameObject>();
            foreach (var root in roots)
            {
                var found = TraverseChildren(root.transform, segments, 1);
                if (found != null) matches.Add(found.gameObject);
            }

            if (matches.Count == 0) return null;
            if (matches.Count == 1) return matches[0];

            // 동명 다수
            if (spec.Index.HasValue && spec.Index.Value < matches.Count)
                return matches[spec.Index.Value];

            var paths = string.Join(", ", matches.Select((m, i) => $"#{i}:{GetHierarchyPath(m)}"));
            throw new System.Exception($"동명 후보 다수. 'index' 지정 필요: [{paths}]");
        }

        private static Transform TraverseChildren(Transform current, string[] segments, int idx)
        {
            if (idx >= segments.Length) return current;
            for (int i = 0; i < current.childCount; i++)
            {
                var child = current.GetChild(i);
                if (child.name == segments[idx])
                {
                    var found = TraverseChildren(child, segments, idx + 1);
                    if (found != null) return found;
                }
            }
            return null;
        }

        private static string GetHierarchyPath(GameObject go)
        {
            var path = go.name;
            var t = go.transform.parent;
            while (t != null)
            {
                path = t.name + "/" + path;
                t = t.parent;
            }
            return path;
        }

        private static VisualElement ResolveVisualElement(string spec)
        {
            var doc = Object.FindFirstObjectByType<UIDocument>();
            if (doc == null || doc.rootVisualElement == null)
                throw new System.Exception("UIDocument를 찾을 수 없습니다");

            // "name" 또는 "parent/name" 형식
            var segments = spec.Split('/');
            VisualElement current = doc.rootVisualElement;
            foreach (var seg in segments)
            {
                if (string.IsNullOrEmpty(seg)) continue;
                var found = current.Q<VisualElement>(seg);
                if (found == null)
                    throw new System.Exception($"VisualElement '{seg}'를 찾을 수 없습니다 (in '{spec}')");
                current = found;
            }
            return current;
        }
    }
}
