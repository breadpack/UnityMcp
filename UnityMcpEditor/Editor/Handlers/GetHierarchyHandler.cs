using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BreadPack.Mcp.Unity
{
    public class GetHierarchyHandler : IRequestHandler
    {
        public string ToolName => "unity_get_hierarchy";

        public object Handle(JObject @params)
        {
            int maxDepth = @params?["maxDepth"]?.Value<int>() ?? 5;
            bool includeComponents = @params?["includeComponents"]?.Value<bool>() ?? false;

            // Prefab 편집 모드면 메인 씬이 아니라 prefab 스테이지의 루트를 직렬화한다.
            // 스테이지 루트에서 직렬화하면 SaveAsPrefabAsset 이 저장할 그래프와 정확히 일치한다.
            var stage = PrefabStageContext.CurrentStage;
            if (stage != null)
            {
                var root = SerializeGameObject(stage.prefabContentsRoot, maxDepth, 0, includeComponents);
                return new
                {
                    scene = stage.scene.name,
                    isPrefabStage = true,
                    prefabAssetPath = stage.assetPath,
                    rootObjects = new List<Dictionary<string, object>> { root }
                };
            }

            var scene = SceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects()
                .Select(go => SerializeGameObject(go, maxDepth, 0, includeComponents))
                .ToList();

            return new { scene = scene.name, isPrefabStage = false, rootObjects = roots };
        }

        private Dictionary<string, object> SerializeGameObject(GameObject go, int maxDepth, int depth, bool includeComponents)
        {
            var result = new Dictionary<string, object>
            {
                ["name"] = go.name,
                // instanceId/path 를 함께 실어, prefab 스테이지 내부 오브젝트도 후속 도구가
                // 안정적으로 가리킬 수 있게 한다 (instanceId 우선 경로 활성화).
                ["instanceId"] = go.GetInstanceID(),
                ["path"] = GameObjectResolver.GetPath(go),
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
