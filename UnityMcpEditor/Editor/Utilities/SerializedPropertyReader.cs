using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BreadPack.Mcp.Unity
{
    public static class SerializedPropertyReader
    {
        private const int MaxRecursionDepth = 10;
        private const int MaxArrayElements = 50;

        /// <summary>
        /// SerializedObject의 모든 visible 프로퍼티를 Dictionary로 변환한다.
        /// </summary>
        public static Dictionary<string, object> ReadAllProperties(SerializedObject so)
        {
            var result = new Dictionary<string, object>();
            var iterator = so.GetIterator();

            // EnterChildren=true로 첫 프로퍼티 진입
            if (!iterator.NextVisible(true)) return result;

            do
            {
                // m_Script는 MonoBehaviour의 스크립트 참조 — 스킵
                if (iterator.name == "m_Script") continue;

                result[iterator.name] = ReadProperty(iterator, 0);
            }
            while (iterator.NextVisible(false)); // 형제 프로퍼티만 순회 (자식은 ReadProperty에서 처리)

            return result;
        }

        /// <summary>
        /// 단일 SerializedProperty의 값을 읽어 JSON-직렬화 가능한 객체로 반환한다.
        /// </summary>
        public static object ReadProperty(SerializedProperty prop, int depth = 0)
        {
            if (depth > MaxRecursionDepth)
                return $"<max depth exceeded>";

            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer:
                    return prop.intValue;
                case SerializedPropertyType.Boolean:
                    return prop.boolValue;
                case SerializedPropertyType.Float:
                    return prop.floatValue;
                case SerializedPropertyType.String:
                    return prop.stringValue;
                case SerializedPropertyType.Enum:
                    return prop.enumValueIndex >= 0 && prop.enumValueIndex < prop.enumDisplayNames.Length
                        ? prop.enumDisplayNames[prop.enumValueIndex]
                        : prop.enumValueIndex.ToString();
                case SerializedPropertyType.Color:
                    var c = prop.colorValue;
                    return new Dictionary<string, float>
                        { ["r"] = c.r, ["g"] = c.g, ["b"] = c.b, ["a"] = c.a };
                case SerializedPropertyType.Vector2:
                    var v2 = prop.vector2Value;
                    return new Dictionary<string, float> { ["x"] = v2.x, ["y"] = v2.y };
                case SerializedPropertyType.Vector3:
                    var v3 = prop.vector3Value;
                    return new Dictionary<string, float>
                        { ["x"] = v3.x, ["y"] = v3.y, ["z"] = v3.z };
                case SerializedPropertyType.Vector4:
                    var v4 = prop.vector4Value;
                    return new Dictionary<string, float>
                        { ["x"] = v4.x, ["y"] = v4.y, ["z"] = v4.z, ["w"] = v4.w };
                case SerializedPropertyType.Rect:
                    var r = prop.rectValue;
                    return new Dictionary<string, float>
                        { ["x"] = r.x, ["y"] = r.y, ["width"] = r.width, ["height"] = r.height };
                case SerializedPropertyType.Bounds:
                    var b = prop.boundsValue;
                    return new Dictionary<string, object>
                    {
                        ["center"] = new Dictionary<string, float>
                            { ["x"] = b.center.x, ["y"] = b.center.y, ["z"] = b.center.z },
                        ["size"] = new Dictionary<string, float>
                            { ["x"] = b.size.x, ["y"] = b.size.y, ["z"] = b.size.z }
                    };
                case SerializedPropertyType.Vector2Int:
                    var v2i = prop.vector2IntValue;
                    return new Dictionary<string, int> { ["x"] = v2i.x, ["y"] = v2i.y };
                case SerializedPropertyType.Vector3Int:
                    var v3i = prop.vector3IntValue;
                    return new Dictionary<string, int>
                        { ["x"] = v3i.x, ["y"] = v3i.y, ["z"] = v3i.z };
                case SerializedPropertyType.ObjectReference:
                    if (prop.objectReferenceValue == null)
                        return null;
                    var objRef = prop.objectReferenceValue;
                    var assetPath = AssetDatabase.GetAssetPath(objRef);
                    return new Dictionary<string, object>
                    {
                        ["name"] = objRef.name,
                        ["type"] = objRef.GetType().Name,
                        ["instanceId"] = objRef.GetInstanceID(),
                        ["assetPath"] = string.IsNullOrEmpty(assetPath) ? null : assetPath
                    };
                case SerializedPropertyType.LayerMask:
                    return prop.intValue;
                case SerializedPropertyType.ArraySize:
                    return prop.intValue;
                case SerializedPropertyType.Quaternion:
                    var q = prop.quaternionValue;
                    return new Dictionary<string, float>
                        { ["x"] = q.x, ["y"] = q.y, ["z"] = q.z, ["w"] = q.w };

                // Generic (struct 등) — 자식 프로퍼티를 재귀 탐색
                case SerializedPropertyType.Generic:
                    return ReadGenericProperty(prop, depth);

                default:
                    return $"<{prop.propertyType}>";
            }
        }

        private static object ReadGenericProperty(SerializedProperty prop, int depth)
        {
            // Array/List
            if (prop.isArray)
            {
                var list = new List<object>();
                for (int i = 0; i < Mathf.Min(prop.arraySize, MaxArrayElements); i++)
                {
                    list.Add(ReadProperty(prop.GetArrayElementAtIndex(i), depth + 1));
                }
                if (prop.arraySize > MaxArrayElements)
                    list.Add($"... +{prop.arraySize - MaxArrayElements} more");
                return list;
            }

            // Struct — 자식 프로퍼티 순회
            var dict = new Dictionary<string, object>();
            var child = prop.Copy();
            var end = prop.Copy();
            var parentDepth = prop.depth;
            end.Next(false);

            if (child.Next(true))
            {
                do
                {
                    if (child.depth <= parentDepth) break;
                    if (SerializedProperty.EqualContents(child, end)) break;
                    dict[child.name] = ReadProperty(child, depth + 1);
                }
                while (child.Next(false));
            }

            return dict;
        }
    }
}
