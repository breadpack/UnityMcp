using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMcp.Editor
{
    public class GetScreenHandler : IRequestHandler
    {
        public string ToolName => "unity_get_screen";

        public object Handle(JObject @params)
        {
            if (!EditorApplication.isPlaying)
                return new { isPlayMode = false, error = "Only available in Play Mode" };

            var uiDocuments = UnityEngine.Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None);
            if (uiDocuments.Length == 0)
                return new { isPlayMode = true, error = "No UIDocument found in scene" };

            var screens = new List<object>();
            foreach (var doc in uiDocuments)
            {
                if (doc.rootVisualElement == null) continue;

                screens.Add(new
                {
                    gameObject = doc.gameObject.name,
                    path = GetPath(doc.gameObject),
                    active = doc.gameObject.activeInHierarchy,
                    visualTreeAsset = doc.visualTreeAsset != null ? doc.visualTreeAsset.name : null,
                    panelSettings = doc.panelSettings != null ? doc.panelSettings.name : null
                });
            }

            return new
            {
                isPlayMode = true,
                uiDocumentCount = uiDocuments.Length,
                screens
            };
        }

        private static string GetPath(GameObject go)
        {
            var path = go.name;
            var current = go.transform.parent;
            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }
            return path;
        }
    }
}
