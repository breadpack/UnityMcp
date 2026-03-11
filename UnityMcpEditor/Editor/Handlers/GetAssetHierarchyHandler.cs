using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace UnityMcp.Editor
{
    public class GetAssetHierarchyHandler : IRequestHandler
    {
        public string ToolName => "unity_get_asset_hierarchy";

        public object Handle(JObject @params)
        {
            string assetPath = @params?["assetPath"]?.Value<string>();
            string assetGuid = @params?["assetGuid"]?.Value<string>();
            int maxDepth = @params?["maxDepth"]?.Value<int>() ?? 5;
            bool includeComponents = @params?["includeComponents"]?.Value<bool>() ?? false;

            // GUID → 경로 변환
            if (!string.IsNullOrEmpty(assetGuid))
            {
                assetPath = AssetDatabase.GUIDToAssetPath(assetGuid);
                if (string.IsNullOrEmpty(assetPath))
                    throw new ArgumentException($"No asset found for GUID: '{assetGuid}'");
            }

            if (string.IsNullOrEmpty(assetPath))
                throw new ArgumentException("Either 'assetPath' or 'assetGuid' must be specified.");

            string extension = System.IO.Path.GetExtension(assetPath).ToLower();

            return extension switch
            {
                ".prefab" => HandlePrefab(assetPath, maxDepth, includeComponents),
                ".unity" => HandleScene(assetPath, maxDepth, includeComponents),
                _ => throw new ArgumentException(
                    $"Unsupported asset type '{extension}'. Expected '.prefab' or '.unity'.")
            };
        }

        private object HandlePrefab(string assetPath, int maxDepth, bool includeComponents)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab == null)
                throw new ArgumentException($"Prefab not found at path: '{assetPath}'");

            return new
            {
                assetPath,
                assetType = "Prefab",
                rootObjects = new[] { SerializeGameObject(prefab, maxDepth, 0, includeComponents) }
            };
        }

        private object HandleScene(string assetPath, int maxDepth, bool includeComponents)
        {
            // Additive로 열어서 현재 Scene에 영향 없이 조회
            var scene = EditorSceneManager.OpenScene(assetPath, OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects()
                    .Select(go => SerializeGameObject(go, maxDepth, 0, includeComponents))
                    .ToList();

                return new
                {
                    assetPath,
                    assetType = "Scene",
                    sceneName = scene.name,
                    rootObjects = roots
                };
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private Dictionary<string, object> SerializeGameObject(
            GameObject go, int maxDepth, int depth, bool includeComponents)
        {
            var result = new Dictionary<string, object>
            {
                ["name"] = go.name,
                ["active"] = go.activeSelf
            };

            if (includeComponents)
            {
                result["components"] = go.GetComponents<Component>()
                    .Where(c => c != null)
                    .Select(c => c.GetType().Name)
                    .ToList();
            }

            if (depth < maxDepth)
            {
                var children = new List<Dictionary<string, object>>();
                for (int i = 0; i < go.transform.childCount; i++)
                {
                    children.Add(SerializeGameObject(
                        go.transform.GetChild(i).gameObject, maxDepth, depth + 1, includeComponents));
                }
                if (children.Count > 0)
                    result["children"] = children;
            }

            return result;
        }
    }
}
