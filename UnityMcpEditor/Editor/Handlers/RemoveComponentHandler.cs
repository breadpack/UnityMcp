using Newtonsoft.Json.Linq;
using UnityEngine;

namespace BreadPack.Mcp.Unity
{
    public class RemoveComponentHandler : IRequestHandler
    {
        public string ToolName => "unity_remove_component";

        public object Handle(JObject @params)
        {
            var go = GameObjectResolver.Resolve(@params);
            var componentType = @params?["componentType"]?.Value<string>();
            var index = @params?["index"]?.Value<int>() ?? 0;
            var dryRun = @params?["dryRun"]?.Value<bool>() ?? false;

            if (string.IsNullOrEmpty(componentType))
                throw new System.ArgumentException("componentType is required");

            var type = ComponentResolver.Resolve(componentType);

            if (type == typeof(Transform) || type == typeof(RectTransform))
                throw new System.ArgumentException("Cannot remove Transform component");

            var component = ComponentResolver.GetComponent(go, componentType, index);
            var typeName = component.GetType().Name;

            if (dryRun)
            {
                return new
                {
                    dryRun = true,
                    gameObject = go.name,
                    path = GameObjectResolver.GetPath(go),
                    componentType = typeName,
                    index
                };
            }

            UndoHelper.DestroyObject(component, $"Remove {typeName}");
            UndoHelper.MarkDirty(go);

            return new
            {
                gameObject = go.name,
                path = GameObjectResolver.GetPath(go),
                removed = typeName
            };
        }
    }
}
