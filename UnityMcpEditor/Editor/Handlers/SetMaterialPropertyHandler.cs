using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace BreadPack.Mcp.Unity
{
    public class SetMaterialPropertyHandler : IRequestHandler
    {
        public string ToolName => "unity_set_material_property";

        public object Handle(JObject @params)
        {
            var materialPath = @params?["materialPath"]?.Value<string>();
            var propertyName = @params?["propertyName"]?.Value<string>();
            var value = @params?["value"]?.Value<string>();
            var propertyType = @params?["propertyType"]?.Value<string>()?.ToLowerInvariant();

            if (string.IsNullOrEmpty(materialPath))
                throw new System.ArgumentException("materialPath is required");
            if (string.IsNullOrEmpty(propertyName))
                throw new System.ArgumentException("propertyName is required");
            if (string.IsNullOrEmpty(value))
                throw new System.ArgumentException("value is required");
            if (string.IsNullOrEmpty(propertyType))
                throw new System.ArgumentException("propertyType is required");

            var mat = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (mat == null)
                throw new System.ArgumentException($"Material not found at path: {materialPath}");

            Undo.RecordObject(mat, $"Set Material Property {propertyName}");

            switch (propertyType)
            {
                case "color":
                    mat.SetColor(propertyName, ParseColor(value));
                    break;
                case "float":
                    mat.SetFloat(propertyName, float.Parse(value, System.Globalization.CultureInfo.InvariantCulture));
                    break;
                case "int":
                    mat.SetInt(propertyName, int.Parse(value, System.Globalization.CultureInfo.InvariantCulture));
                    break;
                case "texture":
                    var texture = AssetDatabase.LoadAssetAtPath<Texture>(value);
                    if (texture == null)
                        throw new System.ArgumentException($"Texture not found at path: {value}");
                    mat.SetTexture(propertyName, texture);
                    break;
                case "vector":
                    mat.SetVector(propertyName, ParseVector(value));
                    break;
                default:
                    throw new System.ArgumentException($"Unsupported propertyType: {propertyType}. Use: color, float, int, texture, vector");
            }

            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();

            return new
            {
                material = materialPath,
                property = propertyName,
                propertyType,
                success = true
            };
        }

        private static Color ParseColor(string value)
        {
            var jo = JObject.Parse(value);
            return new Color(
                jo["r"]?.Value<float>() ?? 0f,
                jo["g"]?.Value<float>() ?? 0f,
                jo["b"]?.Value<float>() ?? 0f,
                jo["a"]?.Value<float>() ?? 1f
            );
        }

        private static Vector4 ParseVector(string value)
        {
            var jo = JObject.Parse(value);
            return new Vector4(
                jo["x"]?.Value<float>() ?? 0f,
                jo["y"]?.Value<float>() ?? 0f,
                jo["z"]?.Value<float>() ?? 0f,
                jo["w"]?.Value<float>() ?? 0f
            );
        }
    }
}
