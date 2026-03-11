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
