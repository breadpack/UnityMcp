using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnityMcp.Editor
{
    public static class UndoHelper
    {
        public static void RecordObject(UnityEngine.Object target, string name)
        {
            Undo.RecordObject(target, $"[MCP] {name}");
        }

        public static void RegisterCreated(GameObject go, string name)
        {
            Undo.RegisterCreatedObjectUndo(go, $"[MCP] {name}");
        }

        public static void DestroyObject(Object target, string name)
        {
            Undo.DestroyObjectImmediate(target);
        }

        public static Component AddComponent(GameObject go, Type type)
        {
            return Undo.AddComponent(go, type);
        }

        public static void SetTransformParent(Transform child, Transform newParent, string name)
        {
            Undo.SetTransformParent(child, newParent, $"[MCP] {name}");
        }

        public static void MarkDirty(UnityEngine.Object target)
        {
            EditorUtility.SetDirty(target);
        }
    }
}
