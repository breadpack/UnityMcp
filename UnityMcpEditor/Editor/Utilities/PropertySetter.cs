using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace BreadPack.Mcp.Unity
{
    public static class PropertySetter
    {
        private static readonly Dictionary<(Type, string), MemberInfo> _memberCache = new();

        private static MemberInfo GetCachedMember(Type type, string name)
        {
            var key = (type, name);
            if (_memberCache.TryGetValue(key, out var cached)) return cached;

            var member = (MemberInfo)type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                ?? type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            if (member != null) _memberCache[key] = member;
            return member;
        }

        public static Dictionary<string, object> SetProperties(Component component, JObject properties)
        {
            var results = new Dictionary<string, object>();

            foreach (var prop in properties)
            {
                try
                {
                    SetProperty(component, prop.Key, prop.Value);
                    results[prop.Key] = "ok";
                }
                catch (Exception ex)
                {
                    results[prop.Key] = $"error: {ex.Message}";
                }
            }

            return results;
        }

        public static void SetProperty(Component component, string key, JToken value)
        {
            if (component == null)
                throw new ArgumentNullException(nameof(component));

            string[] parts = key.Split('.');
            if (parts.Length == 1)
            {
                SetDirectProperty(component, component.GetType(), key, value);
            }
            else
            {
                SetNestedProperty(component, parts, value);
            }
        }

        public static void SetDirectProperty(object target, Type targetType, string name, JToken value)
        {
            var member = GetCachedMember(targetType, name);

            if (member is FieldInfo field)
            {
                var converted = ConvertValue(value, field.FieldType);
                field.SetValue(target, converted);
                return;
            }

            if (member is PropertyInfo prop && prop.CanWrite)
            {
                var converted = ConvertValue(value, prop.PropertyType);
                prop.SetValue(target, converted);
                return;
            }

            throw new ArgumentException(
                $"No writable field or property '{name}' found on type '{targetType.Name}'.");
        }

        public static void SetNestedProperty(Component component, string[] parts, JToken value)
        {
            // Walk down the chain, collecting values and types for struct write-back
            var chain = new List<(object obj, Type type, string memberName, bool isStruct)>();
            object current = component;
            Type currentType = component.GetType();

            for (int i = 0; i < parts.Length - 1; i++)
            {
                string memberName = parts[i];
                bool isStruct = false;
                var member = GetCachedMember(currentType, memberName);

                if (member is FieldInfo field)
                {
                    isStruct = field.FieldType.IsValueType && !field.FieldType.IsPrimitive
                               && !field.FieldType.IsEnum;
                    chain.Add((current, currentType, memberName, isStruct));
                    current = field.GetValue(current);
                    currentType = field.FieldType;
                    continue;
                }

                if (member is PropertyInfo prop && prop.CanRead)
                {
                    isStruct = prop.PropertyType.IsValueType && !prop.PropertyType.IsPrimitive
                               && !prop.PropertyType.IsEnum;
                    chain.Add((current, currentType, memberName, isStruct));
                    current = prop.GetValue(current);
                    currentType = prop.PropertyType;
                    continue;
                }

                throw new ArgumentException(
                    $"Member '{memberName}' not found on type '{currentType.Name}' " +
                    $"while traversing '{string.Join(".", parts)}'.");
            }

            // Set the final property
            string finalName = parts[parts.Length - 1];
            SetDirectProperty(current, currentType, finalName, value);

            // Write-back for structs (reverse order)
            object writeBackValue = current;
            for (int i = chain.Count - 1; i >= 0; i--)
            {
                var (parentObj, parentType, memberName, isStruct) = chain[i];
                if (!isStruct) break;

                var member = GetCachedMember(parentType, memberName);
                if (member is FieldInfo field)
                {
                    field.SetValue(parentObj, writeBackValue);
                    writeBackValue = parentObj;
                    continue;
                }

                if (member is PropertyInfo prop && prop.CanWrite)
                {
                    prop.SetValue(parentObj, writeBackValue);
                    writeBackValue = parentObj;
                }
            }
        }

        public static object ConvertValue(JToken token, Type targetType)
        {
            if (token == null || token.Type == JTokenType.Null)
                return null;

            // Asset reference
            if (token is JObject jObj && AssetResolver.IsAssetReference(token))
                return AssetResolver.ResolveFromToken(jObj);

            // Enum
            if (targetType.IsEnum)
            {
                if (token.Type == JTokenType.String)
                    return Enum.Parse(targetType, token.Value<string>());
                if (token.Type == JTokenType.Integer)
                    return Enum.ToObject(targetType, token.Value<int>());
            }

            // Vector2
            if (targetType == typeof(Vector2) && token is JObject v2)
                return new Vector2(
                    v2["x"]?.Value<float>() ?? 0f,
                    v2["y"]?.Value<float>() ?? 0f);

            // Vector3
            if (targetType == typeof(Vector3) && token is JObject v3)
                return new Vector3(
                    v3["x"]?.Value<float>() ?? 0f,
                    v3["y"]?.Value<float>() ?? 0f,
                    v3["z"]?.Value<float>() ?? 0f);

            // Vector4
            if (targetType == typeof(Vector4) && token is JObject v4)
                return new Vector4(
                    v4["x"]?.Value<float>() ?? 0f,
                    v4["y"]?.Value<float>() ?? 0f,
                    v4["z"]?.Value<float>() ?? 0f,
                    v4["w"]?.Value<float>() ?? 0f);

            // Quaternion
            if (targetType == typeof(Quaternion) && token is JObject q)
                return new Quaternion(
                    q["x"]?.Value<float>() ?? 0f,
                    q["y"]?.Value<float>() ?? 0f,
                    q["z"]?.Value<float>() ?? 0f,
                    q["w"]?.Value<float>() ?? 1f);

            // Color
            if (targetType == typeof(Color) && token is JObject c)
                return new Color(
                    c["r"]?.Value<float>() ?? 0f,
                    c["g"]?.Value<float>() ?? 0f,
                    c["b"]?.Value<float>() ?? 0f,
                    c["a"]?.Value<float>() ?? 1f);

            // Rect
            if (targetType == typeof(Rect) && token is JObject r)
                return new Rect(
                    r["x"]?.Value<float>() ?? 0f,
                    r["y"]?.Value<float>() ?? 0f,
                    r["width"]?.Value<float>() ?? 0f,
                    r["height"]?.Value<float>() ?? 0f);

            // Vector2Int
            if (targetType == typeof(Vector2Int) && token is JObject v2i)
                return new Vector2Int(
                    v2i["x"]?.Value<int>() ?? 0,
                    v2i["y"]?.Value<int>() ?? 0);

            // Vector3Int
            if (targetType == typeof(Vector3Int) && token is JObject v3i)
                return new Vector3Int(
                    v3i["x"]?.Value<int>() ?? 0,
                    v3i["y"]?.Value<int>() ?? 0,
                    v3i["z"]?.Value<int>() ?? 0);

            // Bounds
            if (targetType == typeof(Bounds) && token is JObject b)
            {
                var center = b["center"] as JObject;
                var size = b["size"] as JObject;
                return new Bounds(
                    new Vector3(
                        center?["x"]?.Value<float>() ?? 0f,
                        center?["y"]?.Value<float>() ?? 0f,
                        center?["z"]?.Value<float>() ?? 0f),
                    new Vector3(
                        size?["x"]?.Value<float>() ?? 0f,
                        size?["y"]?.Value<float>() ?? 0f,
                        size?["z"]?.Value<float>() ?? 0f));
            }

            // Array
            if (targetType.IsArray && token is JArray arrForArray)
            {
                var elemType = targetType.GetElementType();
                var array = Array.CreateInstance(elemType, arrForArray.Count);
                for (int i = 0; i < arrForArray.Count; i++)
                {
                    array.SetValue(ConvertValue(arrForArray[i], elemType), i);
                }

                return array;
            }

            // List<T>
            if (targetType.IsGenericType
                && targetType.GetGenericTypeDefinition() == typeof(List<>)
                && token is JArray arrForList)
            {
                var elemType = targetType.GetGenericArguments()[0];
                var list = (IList)Activator.CreateInstance(targetType);
                foreach (var item in arrForList)
                {
                    list.Add(ConvertValue(item, elemType));
                }

                return list;
            }

            // Primitives / fallback
            return token.ToObject(targetType);
        }
    }
}
