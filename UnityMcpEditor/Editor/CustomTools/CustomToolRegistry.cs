using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace BreadPack.Mcp.Unity
{
    public static class CustomToolRegistry
    {
        private static readonly Dictionary<string, CustomToolInfo> _tools = new();

        public static IReadOnlyDictionary<string, CustomToolInfo> Tools => _tools;

        public static void ScanAndRegister(McpRequestDispatcher dispatcher)
        {
            _tools.Clear();

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        foreach (var method in type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                        {
                            var attr = method.GetCustomAttribute<McpToolAttribute>();
                            if (attr == null) continue;

                            var toolInfo = new CustomToolInfo(attr.Name, attr.Description, method);
                            _tools[attr.Name] = toolInfo;

                            dispatcher.Register(new CustomToolHandler(toolInfo));
                        }
                    }
                }
                catch
                {
                    // Skip assemblies that fail to enumerate types
                }
            }

            if (_tools.Count > 0)
                Debug.Log($"[MCP] Registered {_tools.Count} custom tools: {string.Join(", ", _tools.Keys)}");
        }
    }

    public class CustomToolInfo
    {
        public string Name { get; }
        public string Description { get; }
        public MethodInfo Method { get; }
        public ParameterInfo[] Parameters { get; }

        public CustomToolInfo(string name, string description, MethodInfo method)
        {
            Name = name;
            Description = description;
            Method = method;
            Parameters = method.GetParameters();
        }

        public object GetParameterList()
        {
            return Parameters.Select(p =>
            {
                var paramAttr = p.GetCustomAttribute<McpToolParamAttribute>();
                return new
                {
                    name = p.Name,
                    type = GetFriendlyTypeName(p.ParameterType),
                    description = paramAttr?.Description ?? "",
                    required = !p.HasDefaultValue,
                    defaultValue = p.HasDefaultValue ? p.DefaultValue?.ToString() : null
                };
            }).ToArray();
        }

        private static string GetFriendlyTypeName(Type t)
        {
            if (t == typeof(string)) return "string";
            if (t == typeof(int)) return "int";
            if (t == typeof(float)) return "float";
            if (t == typeof(bool)) return "bool";
            if (t == typeof(double)) return "double";
            if (t == typeof(long)) return "long";
            return t.Name;
        }
    }

    public class CustomToolHandler : IRequestHandler
    {
        private readonly CustomToolInfo _info;

        public string ToolName => _info.Name;

        public CustomToolHandler(CustomToolInfo info)
        {
            _info = info;
        }

        public object Handle(JObject @params)
        {
            var args = new object[_info.Parameters.Length];

            for (int i = 0; i < _info.Parameters.Length; i++)
            {
                var p = _info.Parameters[i];
                var token = @params?[p.Name];

                if (token == null || token.Type == JTokenType.Null)
                {
                    if (p.HasDefaultValue)
                        args[i] = p.DefaultValue;
                    else
                        throw new ArgumentException($"Required parameter '{p.Name}' is missing");
                }
                else
                {
                    args[i] = token.ToObject(p.ParameterType);
                }
            }

            try
            {
                var result = _info.Method.Invoke(null, args);
                return new { success = true, result = result?.ToString(), resultType = result?.GetType().Name ?? "void" };
            }
            catch (TargetInvocationException tie)
            {
                var ex = tie.InnerException ?? tie;
                return new { success = false, error = ex.Message, stackTrace = ex.StackTrace };
            }
        }
    }
}
