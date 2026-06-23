using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BreadPack.Mcp.Unity
{
    /// <summary>
    /// Prefab 을 편집 스테이지 없이 단일 원자 호출로 편집한다.
    ///
    /// PrefabUtility.LoadPrefabContents 로 prefab 콘텐츠를 임시 씬에 로드하고, edits 배열의
    /// 각 op 를 prefab 루트 기준 상대 경로(target)에 순차 적용한 뒤 SaveAsPrefabAsset 으로
    /// 저장하고 UnloadPrefabContents 로 정리한다.
    ///
    /// 다단계 상태 프로토콜(enter→여러 도구→save→exit)의 모든 실패 모드(스테이지 비인식 해석,
    /// instanceId 왕복, 중간 도메인 리로드, 부분 적용)를 구조적으로 제거한다.
    /// 임시 콘텐츠는 Undo 시스템 밖이라 Undo 미지원(Addressable 도구와 동일 제약).
    /// </summary>
    public class PrefabApplyHandler : IRequestHandler
    {
        public string ToolName => "unity_prefab_apply";

        public object Handle(JObject @params)
        {
            string assetPath = ResolvePrefabPath(@params);

            var edits = @params?["edits"] as JArray;
            if (edits == null || edits.Count == 0)
                throw new ArgumentException("'edits' must be a non-empty array");

            // 같은 prefab 이 편집 스테이지로 열려 있으면 LoadPrefabContents 와 충돌한다 — 명확히 차단.
            var openStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (openStage != null &&
                string.Equals(openStage.assetPath, assetPath, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    $"Prefab '{assetPath}' 은 현재 편집 스테이지로 열려 있습니다. " +
                    "unity_prefab_edit(action=exit) 로 스테이지를 닫은 뒤 prefab_apply 를 쓰거나, " +
                    "스테이지가 열린 동안에는 generic 편집 도구를 사용하세요.");

            var root = PrefabUtility.LoadPrefabContents(assetPath);
            var editResults = new List<object>();
            try
            {
                for (int i = 0; i < edits.Count; i++)
                {
                    if (edits[i] is not JObject edit)
                        throw new ArgumentException($"edits[{i}] is not an object");
                    editResults.Add(ApplyEdit(root, edit, i));
                }

                PrefabUtility.SaveAsPrefabAsset(root, assetPath);

                return new
                {
                    assetPath,
                    applied = editResults.Count,
                    edits = editResults,
                    hierarchy = Serialize(root, 6, 0)
                };
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static string ResolvePrefabPath(JObject @params)
        {
            string assetPath = @params?["assetPath"]?.Value<string>();
            string assetGuid = @params?["assetGuid"]?.Value<string>();

            if (!string.IsNullOrEmpty(assetGuid))
            {
                assetPath = AssetDatabase.GUIDToAssetPath(assetGuid);
                if (string.IsNullOrEmpty(assetPath))
                    throw new ArgumentException($"No asset found for GUID: '{assetGuid}'");
            }

            if (string.IsNullOrEmpty(assetPath))
                throw new ArgumentException("Either 'assetPath' or 'assetGuid' must be specified.");
            // System.IO.Path 로 정규화 — 이 클래스의 private Path(GameObject) 헬퍼와 이름 충돌 방지.
            if (System.IO.Path.GetExtension(assetPath).ToLower() != ".prefab")
                throw new ArgumentException($"Asset is not a prefab: '{assetPath}'");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(assetPath) == null)
                throw new ArgumentException($"Prefab not found at path: '{assetPath}'");

            return assetPath;
        }

        private object ApplyEdit(GameObject root, JObject edit, int index)
        {
            string op = edit["op"]?.Value<string>();
            if (string.IsNullOrEmpty(op))
                throw new ArgumentException($"edits[{index}].op is required");

            string target = edit["target"]?.Value<string>();
            var go = ResolveInRoot(root, target);

            switch (op)
            {
                case "set_property":
                    return OpSetProperty(go, edit);
                case "add_component":
                    return OpAddComponent(go, edit);
                case "remove_component":
                    return OpRemoveComponent(go, edit);
                case "set_transform":
                    return OpSetTransform(go, edit);
                case "set_active":
                    return OpSetActive(go, edit);
                case "create_child":
                    return OpCreateChild(go, edit);
                case "reparent":
                    return OpReparent(root, go, edit);
                case "set_asset_reference":
                    return OpSetAssetReference(go, edit);
                case "delete":
                    return OpDelete(root, go);
                default:
                    throw new ArgumentException(
                        $"edits[{index}]: 알 수 없는 op '{op}'. " +
                        "set_property|add_component|remove_component|set_transform|set_active|" +
                        "create_child|reparent|set_asset_reference|delete 중 하나를 사용하세요.");
            }
        }

        // ───────── ops ─────────

        private object OpSetProperty(GameObject go, JObject edit)
        {
            var componentType = RequireString(edit, "componentType");
            var index = edit["index"]?.Value<int>() ?? 0;
            if (edit["properties"] is not JObject properties || !properties.HasValues)
                throw new ArgumentException("set_property: 'properties' must be a non-empty object");

            var component = ComponentResolver.GetComponent(go, componentType, index);
            var results = PropertySetter.SetProperties(component, properties);
            // PropertySetter 는 프로퍼티별 예외를 results 에 "error:..." 로 삼킨다. 원자성 보장을 위해
            // 하나라도 실패하면 저장 전에 throw 한다(loop 중단 → SaveAsPrefabAsset 미실행 → Unload 가 폐기).
            EnsureAllOk(results, $"set_property '{Path(go)}' ({component.GetType().Name})");
            return new { op = "set_property", target = Path(go), componentType = component.GetType().Name, results };
        }

        private object OpAddComponent(GameObject go, JObject edit)
        {
            var componentType = RequireString(edit, "componentType");
            var type = ComponentResolver.Resolve(componentType);
            var component = go.AddComponent(type);

            // 선택적 인라인 properties — 추가와 동시에 값 설정
            if (edit["properties"] is JObject props && props.HasValues)
            {
                var results = PropertySetter.SetProperties(component, props);
                EnsureAllOk(results, $"add_component properties '{Path(go)}' ({type.Name})");
            }

            return new { op = "add_component", target = Path(go), added = type.Name };
        }

        private object OpRemoveComponent(GameObject go, JObject edit)
        {
            var componentType = RequireString(edit, "componentType");
            var index = edit["index"]?.Value<int>() ?? 0;
            var type = ComponentResolver.Resolve(componentType);
            if (type == typeof(Transform) || type == typeof(RectTransform))
                throw new ArgumentException("Cannot remove Transform component");

            var component = ComponentResolver.GetComponent(go, componentType, index);
            var typeName = component.GetType().Name;
            UnityEngine.Object.DestroyImmediate(component);
            return new { op = "remove_component", target = Path(go), removed = typeName };
        }

        private object OpSetTransform(GameObject go, JObject edit)
        {
            var space = edit["space"]?.Value<string>() ?? "local";
            bool isLocal = space.ToLower() != "world";
            var t = go.transform;

            if (edit["position"] != null)
            {
                var pos = (Vector3)PropertySetter.ConvertValue(edit["position"], typeof(Vector3));
                if (isLocal) t.localPosition = pos; else t.position = pos;
            }
            if (edit["rotation"] != null)
            {
                var rot = (Vector3)PropertySetter.ConvertValue(edit["rotation"], typeof(Vector3));
                if (isLocal) t.localEulerAngles = rot; else t.eulerAngles = rot;
            }
            if (edit["scale"] != null)
            {
                var scale = (Vector3)PropertySetter.ConvertValue(edit["scale"], typeof(Vector3));
                t.localScale = scale;
            }

            return new
            {
                op = "set_transform",
                target = Path(go),
                localPosition = V(t.localPosition),
                localRotation = V(t.localEulerAngles),
                localScale = V(t.localScale)
            };
        }

        private object OpSetActive(GameObject go, JObject edit)
        {
            bool active = edit["active"]?.Value<bool>() ?? true;
            go.SetActive(active);
            return new { op = "set_active", target = Path(go), active = go.activeSelf };
        }

        private object OpCreateChild(GameObject parent, JObject edit)
        {
            var name = edit["name"]?.Value<string>() ?? "GameObject";
            var child = new GameObject(name);
            child.transform.SetParent(parent.transform, false);

            if (edit["properties"] is JObject props && props.HasValues)
            {
                // create_child 에서 인라인 컴포넌트 추가는 지원하지 않음 — add_component op 를 따로 쓰세요.
                // properties 는 무시하지 않고 명확히 알린다.
                throw new ArgumentException(
                    "create_child 는 properties 를 직접 받지 않습니다. 자식 생성 후 별도 add_component/set_property op 를 사용하세요.");
            }

            return new { op = "create_child", target = Path(child), created = name, parent = Path(parent) };
        }

        private object OpReparent(GameObject root, GameObject go, JObject edit)
        {
            if (go == root)
                throw new ArgumentException("Cannot reparent the prefab root");

            var newParentPath = edit["newParent"]?.Value<string>();
            var newParent = ResolveInRoot(root, newParentPath);

            if (newParent == go)
                throw new ArgumentException("Cannot parent a GameObject to itself");
            if (newParent.transform.IsChildOf(go.transform))
                throw new ArgumentException("Cannot parent a GameObject to one of its descendants");

            bool worldPositionStays = edit["worldPositionStays"]?.Value<bool>() ?? true;
            go.transform.SetParent(newParent.transform, worldPositionStays);
            return new { op = "reparent", newPath = Path(go) };
        }

        private object OpSetAssetReference(GameObject go, JObject edit)
        {
            var componentType = RequireString(edit, "componentType");
            var propertyName = RequireString(edit, "propertyName");
            var index = edit["index"]?.Value<int>() ?? 0;

            var component = ComponentResolver.GetComponent(go, componentType, index);
            var asset = AssetResolver.Resolve(edit, "assetPath", "assetGuid");

            var type = component.GetType();
            var field = type.GetField(propertyName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                if (!field.FieldType.IsAssignableFrom(asset.GetType()))
                    throw new ArgumentException(
                        $"Asset type {asset.GetType().Name} is not compatible with field type {field.FieldType.Name}");
                field.SetValue(component, asset);
            }
            else
            {
                var prop = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
                if (prop == null || !prop.CanWrite)
                    throw new ArgumentException(
                        $"Field or writable property '{propertyName}' not found on {type.Name}");
                if (!prop.PropertyType.IsAssignableFrom(asset.GetType()))
                    throw new ArgumentException(
                        $"Asset type {asset.GetType().Name} is not compatible with property type {prop.PropertyType.Name}");
                prop.SetValue(component, asset);
            }

            return new
            {
                op = "set_asset_reference",
                target = Path(go),
                componentType = type.Name,
                propertyName,
                assetPath = AssetDatabase.GetAssetPath(asset)
            };
        }

        private object OpDelete(GameObject root, GameObject go)
        {
            if (go == root)
                throw new ArgumentException("Cannot delete the prefab root");
            var name = go.name;
            var path = Path(go);
            UnityEngine.Object.DestroyImmediate(go);
            return new { op = "delete", deleted = name, path };
        }

        // ───────── helpers ─────────

        /// <summary>
        /// prefab 루트 기준 상대 경로로 GameObject 를 찾는다.
        /// "" / null / "." / 루트이름 → 루트. "루트이름/Child" 또는 "Child/GrandChild" 모두 허용.
        /// </summary>
        private static GameObject ResolveInRoot(GameObject root, string target)
        {
            if (string.IsNullOrEmpty(target) || target == "." || target == "/")
                return root;

            var t = target.Trim('/');
            if (t == root.name)
                return root;
            if (t.StartsWith(root.name + "/"))
                t = t.Substring(root.name.Length + 1);

            var found = root.transform.Find(t);
            if (found == null)
                throw new ArgumentException(
                    $"Target '{target}' not found under prefab root '{root.name}'. " +
                    "응답의 hierarchy 에서 유효한 경로를 확인하세요.");
            return found.gameObject;
        }

        /// <summary>루트를 첫 세그먼트로 하는 경로(예: "Enemy/Body"). UnloadPrefabContents 후에도 안정적.</summary>
        private static string Path(GameObject go) => GameObjectResolver.GetPath(go);

        private static object V(Vector3 v) => new { x = v.x, y = v.y, z = v.z };

        private static string RequireString(JObject edit, string key)
        {
            var value = edit[key]?.Value<string>();
            if (string.IsNullOrEmpty(value))
                throw new ArgumentException($"'{key}' is required for this op");
            return value;
        }

        /// <summary>
        /// PropertySetter 결과에 "error:..." 가 있으면 throw 한다. 원자성 보장:
        /// 저장 전에 던져야 SaveAsPrefabAsset 가 실행되지 않고 UnloadPrefabContents 가 변경을 폐기한다.
        /// </summary>
        private static void EnsureAllOk(Dictionary<string, object> results, string ctx)
        {
            var failed = results
                .Where(kv => kv.Value is string s && s.StartsWith("error:"))
                .Select(kv => $"{kv.Key} → {kv.Value}")
                .ToList();
            if (failed.Count > 0)
                throw new ArgumentException(
                    $"{ctx}: 프로퍼티 설정 실패 — 원자성 보장을 위해 저장하지 않고 중단합니다. " +
                    string.Join("; ", failed));
        }

        private Dictionary<string, object> Serialize(GameObject go, int maxDepth, int depth)
        {
            var result = new Dictionary<string, object>
            {
                ["name"] = go.name,
                ["path"] = Path(go),
                ["active"] = go.activeSelf,
                ["components"] = go.GetComponents<Component>()
                    .Where(c => c != null)
                    .Select(c => c.GetType().Name)
                    .ToList()
            };

            if (depth < maxDepth)
            {
                var children = new List<Dictionary<string, object>>();
                for (int i = 0; i < go.transform.childCount; i++)
                    children.Add(Serialize(go.transform.GetChild(i).gameObject, maxDepth, depth + 1));
                if (children.Count > 0)
                    result["children"] = children;
            }

            return result;
        }
    }
}
