using System;

namespace BreadPack.Mcp.Unity
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class McpToolAttribute : Attribute
    {
        public string Name { get; }
        public string Description { get; }

        public McpToolAttribute(string name, string description)
        {
            Name = name;
            Description = description;
        }
    }

    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
    public class McpToolParamAttribute : Attribute
    {
        public string Description { get; }

        public McpToolParamAttribute(string description)
        {
            Description = description;
        }
    }
}
