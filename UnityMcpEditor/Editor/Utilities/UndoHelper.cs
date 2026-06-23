using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BreadPack.Mcp.Unity
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
            // Prefab 편집 모드면 스테이지 씬도 dirty 로 표시 (미저장 변경 상태·auto-save 반영).
            // Prefab 모드가 아니면 no-op.
            PrefabStageContext.MarkTargetSceneDirty();
        }
    }
}
