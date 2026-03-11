using System;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BreadPack.Mcp.Unity
{
    public static class GameObjectResolver
    {
        public static GameObject Resolve(JObject @params)
        {
            int? instanceId = @params["instanceId"]?.Value<int>();
            string path = @params["path"]?.Value<string>();

            if (instanceId.HasValue)
            {
                var obj = UnityEditor.EditorUtility.InstanceIDToObject(instanceId.Value);
                if (obj is GameObject go) return go;
                if (obj is Component comp) return comp.gameObject;
                throw new ArgumentException(
                    $"InstanceID {instanceId.Value} does not refer to a GameObject or Component.");
            }

            if (!string.IsNullOrEmpty(path))
            {
                var go = FindByPath(path);
                if (go != null) return go;
                throw new ArgumentException($"GameObject not found at path: '{path}'");
            }

            throw new ArgumentException(
                "Either 'path' or 'instanceId' must be specified to resolve a GameObject.");
        }

        public static GameObject ResolveParent(
            JObject @params,
            string pathKey = "parentPath",
            string idKey = "parentId")
        {
            int? parentId = @params[idKey]?.Value<int>();
            string parentPath = @params[pathKey]?.Value<string>();

            if (parentId.HasValue)
            {
                var obj = UnityEditor.EditorUtility.InstanceIDToObject(parentId.Value);
                if (obj is GameObject go) return go;
                if (obj is Component comp) return comp.gameObject;
                throw new ArgumentException(
                    $"Parent instanceID {parentId.Value} does not refer to a GameObject or Component.");
            }

            if (!string.IsNullOrEmpty(parentPath))
            {
                var go = FindByPath(parentPath);
                if (go != null) return go;
                throw new ArgumentException($"Parent GameObject not found at path: '{parentPath}'");
            }

            return null;
        }

        public static GameObject FindByPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;

            string[] parts = path.Split('/');
            string rootName = parts[0];

            GameObject root = null;
            var scene = SceneManager.GetActiveScene();
            foreach (var go in scene.GetRootGameObjects())
            {
                if (go.name == rootName)
                {
                    root = go;
                    break;
                }
            }

            if (root == null) return null;
            if (parts.Length == 1) return root;

            // Build the remaining path for transform.Find
            string subPath = path.Substring(rootName.Length + 1);
            var child = root.transform.Find(subPath);
            return child != null ? child.gameObject : null;
        }

        public static string GetPath(GameObject go)
        {
            if (go == null) return string.Empty;

            string path = go.name;
            var current = go.transform.parent;
            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }

            return path;
        }
    }
}
