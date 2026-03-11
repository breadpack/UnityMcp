using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace BreadPack.Mcp.Unity
{
    public class SetAssetReferenceHandler : IRequestHandler
    {
        public string ToolName => "unity_set_asset_reference";

        public object Handle(JObject @params)
        {
            var go = GameObjectResolver.Resolve(@params);
            var componentType = @params?["componentType"]?.Value<string>();
            var index = @params?["index"]?.Value<int>() ?? 0;
            var propertyName = @params?["propertyName"]?.Value<string>();

            if (string.IsNullOrEmpty(componentType))
                throw new System.ArgumentException("componentType is required");
            if (string.IsNullOrEmpty(propertyName))
                throw new System.ArgumentException("propertyName is required");

            var component = ComponentResolver.GetComponent(go, componentType, index);
            var asset = AssetResolver.Resolve(@params);

            UndoHelper.RecordObject(component, $"Set Asset {propertyName}");

            var type = component.GetType();
            var field = type.GetField(propertyName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                if (!field.FieldType.IsAssignableFrom(asset.GetType()))
                    throw new System.ArgumentException(
                        $"Asset type {asset.GetType().Name} is not compatible with field type {field.FieldType.Name}");
                field.SetValue(component, asset);
            }
            else
            {
                var prop = type.GetProperty(propertyName,
                    BindingFlags.Public | BindingFlags.Instance);
                if (prop != null && prop.CanWrite)
                {
                    if (!prop.PropertyType.IsAssignableFrom(asset.GetType()))
                        throw new System.ArgumentException(
                            $"Asset type {asset.GetType().Name} is not compatible with property type {prop.PropertyType.Name}");
                    prop.SetValue(component, asset);
                }
                else
                    throw new System.ArgumentException(
                        $"Field or writable property '{propertyName}' not found on {type.Name}");
            }

            UndoHelper.MarkDirty(component);
            UndoHelper.MarkDirty(go);

            return new
            {
                gameObject = go.name,
                path = GameObjectResolver.GetPath(go),
                componentType = component.GetType().Name,
                propertyName,
                assetPath = AssetDatabase.GetAssetPath(asset),
                assetType = asset.GetType().Name
            };
        }
    }
}
