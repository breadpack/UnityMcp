using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityMcp.Editor
{
    public class GetHierarchyHandler : IRequestHandler
    {
        public string ToolName => "unity_get_hierarchy";

        public object Handle(JObject @params)
        {
            int maxDepth = @params?["maxDepth"]?.Value<int>() ?? 5;
            bool includeComponents = @params?["includeComponents"]?.Value<bool>() ?? false;

            var scene = SceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects()
                .Select(go => SerializeGameObject(go, maxDepth, 0, includeComponents))
                .ToList();

            return new { scene = scene.name, rootObjects = roots };
        }

        private Dictionary<string, object> SerializeGameObject(GameObject go, int maxDepth, int depth, bool includeComponents)
        {
            var result = new Dictionary<string, object>
            {
                ["name"] = go.name,
                ["active"] = go.activeSelf
            };

            if (includeComponents)
                result["components"] = go.GetComponents<Component>()
                    .Where(c => c != null)
                    .Select(c => c.GetType().Name).ToList();

            if (depth < maxDepth)
            {
                var children = new List<Dictionary<string, object>>();
                for (int i = 0; i < go.transform.childCount; i++)
                    children.Add(SerializeGameObject(go.transform.GetChild(i).gameObject, maxDepth, depth + 1, includeComponents));
                if (children.Count > 0)
                    result["children"] = children;
            }

            return result;
        }
    }
}
