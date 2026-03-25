using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace BreadPack.Mcp.Unity
{
    public class GetSelectionHandler : IRequestHandler
    {
        public string ToolName => "unity_get_selection";

        public object Handle(JObject @params)
        {
            bool includeComponents = @params?["includeComponents"]?.Value<bool>() ?? false;

            return new
            {
                activeObject = GetActiveObjectInfo(includeComponents),
                gameObjects = Selection.gameObjects
                    .Select(go => GetGameObjectInfo(go, includeComponents))
                    .ToArray(),
                selectedAssets = Selection.assetGUIDs
                    .Select(guid =>
                    {
                        var path = AssetDatabase.GUIDToAssetPath(guid);
                        var type = AssetDatabase.GetMainAssetTypeAtPath(path);
                        return new
                        {
                            path,
                            guid,
                            assetType = type?.Name ?? "Unknown"
                        };
                    })
                    .ToArray(),
                selectionCount = Selection.objects.Length
            };
        }

        private static object GetActiveObjectInfo(bool includeComponents)
        {
            var active = Selection.activeObject;
            if (active == null) return null;

            if (active is GameObject go)
            {
                return GetGameObjectInfo(go, includeComponents);
            }

            return new
            {
                name = active.name,
                instanceId = active.GetInstanceID(),
                type = active.GetType().Name
            };
        }

        private static object GetGameObjectInfo(GameObject go, bool includeComponents)
        {
            return new
            {
                name = go.name,
                path = GetHierarchyPath(go),
                instanceId = go.GetInstanceID(),
                activeSelf = go.activeSelf,
                tag = go.tag,
                layer = LayerMask.LayerToName(go.layer),
                components = includeComponents
                    ? go.GetComponents<Component>()
                        .Where(c => c != null)
                        .Select(c => c.GetType().Name)
                        .ToArray()
                    : null
            };
        }

        private static string GetHierarchyPath(GameObject go)
        {
            var path = go.name;
            var parent = go.transform.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            return path;
        }
    }
}
