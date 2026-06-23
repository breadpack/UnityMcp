using Newtonsoft.Json.Linq;
using UnityEngine;

namespace BreadPack.Mcp.Unity
{
    public class CreateGameObjectHandler : IRequestHandler
    {
        public string ToolName => "unity_create_gameobject";

        public object Handle(JObject @params)
        {
            var name = @params?["name"]?.Value<string>() ?? "GameObject";
            var parent = GameObjectResolver.ResolveParent(@params);

            var go = new GameObject(name);
            if (parent != null)
                go.transform.SetParent(parent.transform, false);
            else if (PrefabStageContext.IsInPrefabMode && PrefabStageContext.PrefabRoot != null)
                // Prefab 모드에서 부모 미지정 → prefab 의 단일 루트 아래에 둔다.
                // 스테이지 씬 루트에 형제로 두면 SaveAsPrefabAsset(루트만 직렬화)에서 유실된다.
                go.transform.SetParent(PrefabStageContext.PrefabRoot.transform, false);

            UndoHelper.RegisterCreated(go, $"Create {name}");
            UndoHelper.MarkDirty(go);

            return new
            {
                name = go.name,
                path = GameObjectResolver.GetPath(go),
                instanceId = go.GetInstanceID()
            };
        }
    }
}
