using Newtonsoft.Json.Linq;
using System;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace BreadPack.Mcp.Unity
{
    public class ProjectSettingsHandler : IRequestHandler
    {
        public string ToolName => "unity_project_settings";

        public object Handle(JObject @params)
        {
            var action = @params["action"]?.Value<string>();
            var category = @params["category"]?.Value<string>();
            var propertyName = @params["propertyName"]?.Value<string>();
            var value = @params["value"]?.Value<string>();

            if (string.IsNullOrEmpty(action))
                throw new ArgumentException("action is required");
            if (string.IsNullOrEmpty(category))
                throw new ArgumentException("category is required");

            return action switch
            {
                "get" => HandleGet(category, propertyName),
                "set" => HandleSet(category, propertyName, value),
                _ => throw new ArgumentException($"Unknown action: {action}. Use 'get' or 'set'")
            };
        }

        private static object HandleGet(string category, string propertyName)
        {
            if (!string.IsNullOrEmpty(propertyName))
                return GetSingleProperty(category, propertyName);

            return category switch
            {
                "player" => new
                {
                    companyName = PlayerSettings.companyName,
                    productName = PlayerSettings.productName,
                    bundleVersion = PlayerSettings.bundleVersion,
                    defaultScreenWidth = PlayerSettings.defaultScreenWidth,
                    defaultScreenHeight = PlayerSettings.defaultScreenHeight
                },
                "quality" => new
                {
                    names = QualitySettings.names,
                    currentLevel = QualitySettings.GetQualityLevel()
                },
                "physics" => new
                {
                    gravity = Physics.gravity,
                    defaultContactOffset = Physics.defaultContactOffset
                },
                "time" => new
                {
                    fixedDeltaTime = Time.fixedDeltaTime,
                    timeScale = Time.timeScale
                },
                _ => throw new ArgumentException(
                    $"Unknown category: {category}. Use 'player', 'quality', 'physics', or 'time'")
            };
        }

        private static object GetSingleProperty(string category, string propertyName)
        {
            var type = GetTypeForCategory(category);
            var prop = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Static);
            if (prop == null)
                throw new ArgumentException($"Property '{propertyName}' not found in {type.Name}");

            var val = prop.GetValue(null);
            return new { propertyName, value = val };
        }

        private static object HandleSet(string category, string propertyName, string value)
        {
            if (string.IsNullOrEmpty(propertyName))
                throw new ArgumentException("propertyName is required for 'set' action");
            if (value == null)
                throw new ArgumentException("value is required for 'set' action");

            var type = GetTypeForCategory(category);
            var prop = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Static);
            if (prop == null)
                throw new ArgumentException($"Property '{propertyName}' not found in {type.Name}");
            if (!prop.CanWrite)
                throw new ArgumentException($"Property '{propertyName}' in {type.Name} is read-only");

            var converted = ConvertValue(value, prop.PropertyType);
            prop.SetValue(null, converted);

            return new
            {
                success = true,
                propertyName,
                value = prop.GetValue(null)
            };
        }

        private static Type GetTypeForCategory(string category)
        {
            return category switch
            {
                "player" => typeof(PlayerSettings),
                "quality" => typeof(QualitySettings),
                "physics" => typeof(Physics),
                "time" => typeof(Time),
                _ => throw new ArgumentException(
                    $"Unknown category: {category}. Use 'player', 'quality', 'physics', or 'time'")
            };
        }

        private static object ConvertValue(string value, Type targetType)
        {
            if (targetType == typeof(string))
                return value;
            if (targetType == typeof(int))
                return int.Parse(value, CultureInfo.InvariantCulture);
            if (targetType == typeof(float))
                return float.Parse(value, CultureInfo.InvariantCulture);
            if (targetType == typeof(bool))
                return bool.Parse(value);
            if (targetType == typeof(Vector3))
            {
                var arr = JArray.Parse(value);
                return new Vector3(arr[0].Value<float>(), arr[1].Value<float>(), arr[2].Value<float>());
            }

            var converter = TypeDescriptor.GetConverter(targetType);
            if (converter.CanConvertFrom(typeof(string)))
                return converter.ConvertFromInvariantString(value);

            throw new ArgumentException($"Cannot convert '{value}' to {targetType.Name}");
        }
    }
}
