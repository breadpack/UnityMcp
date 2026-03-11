using Newtonsoft.Json.Linq;

namespace BreadPack.Mcp.Unity
{
    public class SetPropertyHandler : IRequestHandler
    {
        public string ToolName => "unity_set_property";

        public object Handle(JObject @params)
        {
            var go = GameObjectResolver.Resolve(@params);
            var componentType = @params?["componentType"]?.Value<string>();
            var index = @params?["index"]?.Value<int>() ?? 0;
            var properties = @params?["properties"] as JObject;

            if (string.IsNullOrEmpty(componentType))
                throw new System.ArgumentException("componentType is required");
            if (properties == null || !properties.HasValues)
                throw new System.ArgumentException("properties must be a non-empty object");

            var component = ComponentResolver.GetComponent(go, componentType, index);

            UndoHelper.RecordObject(component, $"Set Properties {component.GetType().Name}");
            var results = PropertySetter.SetProperties(component, properties);
            UndoHelper.MarkDirty(component);
            UndoHelper.MarkDirty(go);

            return new
            {
                gameObject = go.name,
                path = GameObjectResolver.GetPath(go),
                componentType = component.GetType().Name,
                results
            };
        }
    }
}
