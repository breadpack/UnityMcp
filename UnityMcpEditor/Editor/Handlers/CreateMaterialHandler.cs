using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace BreadPack.Mcp.Unity
{
    public class CreateMaterialHandler : IRequestHandler
    {
        public string ToolName => "unity_create_material";

        public object Handle(JObject @params)
        {
            var savePath = @params?["savePath"]?.Value<string>();
            var shaderName = @params?["shaderName"]?.Value<string>();

            if (string.IsNullOrEmpty(savePath))
                throw new System.ArgumentException("savePath is required");

            var shader = FindShader(shaderName);
            if (shader == null)
                throw new System.ArgumentException($"Shader not found: {shaderName ?? "Standard / Universal Render Pipeline/Lit"}");

            var mat = new Material(shader);

            var directory = System.IO.Path.GetDirectoryName(savePath);
            if (!string.IsNullOrEmpty(directory) && !AssetDatabase.IsValidFolder(directory))
            {
                CreateFolderRecursive(directory);
            }

            AssetDatabase.CreateAsset(mat, savePath);
            AssetDatabase.Refresh();

            return new
            {
                path = savePath,
                shaderName = shader.name,
                guid = AssetDatabase.AssetPathToGUID(savePath)
            };
        }

        private static Shader FindShader(string shaderName)
        {
            if (!string.IsNullOrEmpty(shaderName))
            {
                var shader = Shader.Find(shaderName);
                if (shader != null) return shader;
                return null;
            }

            var standard = Shader.Find("Standard");
            if (standard != null) return standard;

            var urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit != null) return urpLit;

            return null;
        }

        private static void CreateFolderRecursive(string folderPath)
        {
            var parts = folderPath.Replace("\\", "/").Split('/');
            var current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }
    }
}
