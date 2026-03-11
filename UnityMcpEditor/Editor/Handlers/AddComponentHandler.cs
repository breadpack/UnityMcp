using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace UnityMcp.Editor
{
    public class AddComponentHandler : IRequestHandler
    {
        public string ToolName => "unity_add_component";

        public object Handle(JObject @params)
        {
            var go = GameObjectResolver.Resolve(@params);
            var componentType = @params?["componentType"]?.Value<string>();

            if (string.IsNullOrEmpty(componentType))
                throw new System.ArgumentException("componentType is required");

            var type = ComponentResolver.Resolve(componentType);
            var component = UndoHelper.AddComponent(go, type);
            UndoHelper.MarkDirty(go);

            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Select(f => new { name = f.Name, type = f.FieldType.Name })
                .ToList();

            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanWrite && p.GetIndexParameters().Length == 0)
                .Select(p => new { name = p.Name, type = p.PropertyType.Name })
                .ToList();

            return new
            {
                gameObject = go.name,
                path = GameObjectResolver.GetPath(go),
                componentType = type.Name,
                fullTypeName = type.FullName,
                fields,
                properties
            };
        }
    }
}
