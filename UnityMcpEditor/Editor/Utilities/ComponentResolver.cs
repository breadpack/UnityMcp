using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BreadPack.Mcp.Unity
{
    public static class ComponentResolver
    {
        private static readonly Dictionary<string, Type> _typeCache = new();

        private static readonly string[] CommonNamespaces =
        {
            "UnityEngine",
            "UnityEngine.UI",
            "UnityEngine.UIElements",
            "TMPro",
            "UnityEngine.Rendering",
            "UnityEngine.Tilemaps",
            "UnityEngine.Audio"
        };

        public static Type Resolve(string componentType)
        {
            if (string.IsNullOrEmpty(componentType))
                throw new ArgumentException("Component type name must not be null or empty.");

            // Cache lookup
            if (_typeCache.TryGetValue(componentType, out var cached))
                return cached;

            // Step 1: Try full qualified name
            var type = Type.GetType(componentType);
            if (type != null && typeof(Component).IsAssignableFrom(type))
            {
                _typeCache[componentType] = type;
                return type;
            }

            // Step 2: Try common namespaces
            foreach (var ns in CommonNamespaces)
            {
                string fullName = ns + "." + componentType;
                type = Type.GetType(fullName);
                if (type != null && typeof(Component).IsAssignableFrom(type))
                {
                    _typeCache[componentType] = type;
                    return type;
                }
            }

            // Step 3: Full assembly scan fallback
            string bestMatch = null;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var t in GetTypesSafe(assembly))
                {
                    if (!typeof(Component).IsAssignableFrom(t))
                        continue;

                    if (t.Name == componentType)
                    {
                        _typeCache[componentType] = t;
                        return t;
                    }

                    if (bestMatch == null
                        && t.Name.IndexOf(componentType, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        bestMatch = t.FullName;
                    }
                }
            }

            var message = $"Component type '{componentType}' not found. " +
                          "Use 'unity_get_component_details' to check available components.";
            if (bestMatch != null)
                message += $" Did you mean '{bestMatch}'?";

            throw new ArgumentException(message);
        }

        public static Component GetComponent(GameObject go, string componentType, int index = 0)
        {
            if (go == null)
                throw new ArgumentNullException(nameof(go));

            var type = Resolve(componentType);
            var components = go.GetComponents(type);

            if (components == null || components.Length == 0)
                throw new ArgumentException(
                    $"No '{componentType}' component found on '{go.name}'.");

            if (index < 0 || index >= components.Length)
                throw new ArgumentException(
                    $"Component index {index} out of range. " +
                    $"'{go.name}' has {components.Length} '{componentType}' component(s).");

            return components[index];
        }

        private static Type[] GetTypesSafe(System.Reflection.Assembly assembly)
        {
            try { return assembly.GetTypes(); }
            catch (System.Reflection.ReflectionTypeLoadException ex)
            {
                return (ex.Types ?? Array.Empty<Type>()).Where(t => t != null).ToArray();
            }
        }
    }
}
