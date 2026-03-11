using System;
using System.Collections.Generic;
using System.Reflection;

namespace BreadPack.Mcp.Unity
{
    public static class ViewModelReflector
    {
        public static Dictionary<string, object> GetProperties(object viewModel)
        {
            if (viewModel == null) return null;

            var result = new Dictionary<string, object>();
            var type = viewModel.GetType();

            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (prop.Name == "CancellationToken") continue;
                if (!prop.CanRead) continue;

                try
                {
                    var value = prop.GetValue(viewModel);
                    if (value != null && IsSimpleType(value.GetType()))
                        result[prop.Name] = value;
                }
                catch { /* skip */ }
            }

            return result;
        }

        private static bool IsSimpleType(Type type)
        {
            return type.IsPrimitive || type == typeof(string) || type == typeof(decimal)
                || type == typeof(DateTime) || type.IsEnum;
        }
    }
}
