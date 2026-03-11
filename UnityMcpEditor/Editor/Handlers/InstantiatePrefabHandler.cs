using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace BreadPack.Mcp.Unity
{
    public class InstantiatePrefabHandler : IRequestHandler
    {
        public string ToolName => "unity_instantiate_prefab";

        public object Handle(JObject @params)
        {
            var asset = AssetResolver.Resolve(@params);

            if (asset is not GameObject prefab)
                throw new System.ArgumentException(
                    $"Asset is not a GameObject prefab: {asset.GetType().Name}");

            var parent = GameObjectResolver.ResolveParent(@params);
            var name = @params?["name"]?.Value<string>();

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);

            if (parent != null)
                instance.transform.SetParent(parent.transform, false);

            if (!string.IsNullOrEmpty(name))
                instance.name = name;

            UndoHelper.RegisterCreated(instance, $"Instantiate {instance.name}");
            UndoHelper.MarkDirty(instance);

            return new
            {
                name = instance.name,
                path = GameObjectResolver.GetPath(instance),
                instanceId = instance.GetInstanceID(),
                prefabPath = AssetDatabase.GetAssetPath(prefab)
            };
        }
    }
}
