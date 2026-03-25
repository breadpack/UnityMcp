using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace BreadPack.Mcp.Unity
{
    public class ExecuteMenuItemHandler : IRequestHandler
    {
        public string ToolName => "unity_execute_menu_item";

        public object Handle(JObject @params)
        {
            bool listOnly = @params["listOnly"]?.Value<bool>() ?? false;

            if (listOnly)
            {
                return ListMenuItems();
            }

            return ExecuteMenuItem(@params);
        }

        private object ListMenuItems()
        {
            var menuItems = new List<string>();

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        foreach (var method in type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                        {
                            var attrs = method.GetCustomAttributes(typeof(MenuItem), false);
                            foreach (MenuItem attr in attrs)
                            {
                                if (!attr.menuItem.StartsWith("CONTEXT/"))
                                    menuItems.Add(attr.menuItem);
                            }
                        }
                    }
                }
                catch
                {
                    // Skip assemblies that fail to load types
                }
            }

            menuItems.Sort();

            return new { menuItems = menuItems.Distinct().ToList() };
        }

        private object ExecuteMenuItem(JObject @params)
        {
            var menuPath = @params["menuPath"]?.Value<string>();
            if (string.IsNullOrEmpty(menuPath))
                throw new ArgumentException("menuPath is required");

            bool executed = EditorApplication.ExecuteMenuItem(menuPath);
            if (!executed)
                throw new Exception($"메뉴 아이템을 찾을 수 없습니다: {menuPath}");

            return new { executed = true, menuPath };
        }
    }
}
