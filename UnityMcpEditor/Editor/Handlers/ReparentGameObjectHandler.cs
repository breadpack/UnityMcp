using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace UnityMcp.Editor
{
    public class ReparentGameObjectHandler : IRequestHandler
    {
        public string ToolName => "unity_reparent_gameobject";

        public object Handle(JObject @params)
        {
            var go = GameObjectResolver.Resolve(@params);
            var newParent = GameObjectResolver.ResolveParent(@params, "newParentPath", "newParentId");
            var worldPositionStays = @params?["worldPositionStays"]?.Value<bool>() ?? true;

            if (newParent != null)
            {
                if (newParent == go)
                    throw new System.ArgumentException("Cannot parent a GameObject to itself");
                if (newParent.transform.IsChildOf(go.transform))
                    throw new System.ArgumentException("Cannot parent a GameObject to one of its descendants");
            }

            var undoGroup = Undo.GetCurrentGroup();

            UndoHelper.SetTransformParent(go.transform, newParent?.transform, $"Reparent {go.name}");

            if (!worldPositionStays)
            {
                UndoHelper.RecordObject(go.transform, $"Reset Transform {go.name}");
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;
            }

            Undo.CollapseUndoOperations(undoGroup);
            UndoHelper.MarkDirty(go);

            return new
            {
                name = go.name,
                newPath = GameObjectResolver.GetPath(go),
                instanceId = go.GetInstanceID()
            };
        }
    }
}
