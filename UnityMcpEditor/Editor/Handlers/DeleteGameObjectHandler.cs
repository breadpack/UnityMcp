using Newtonsoft.Json.Linq;
using UnityEngine;

namespace BreadPack.Mcp.Unity
{
    public class DeleteGameObjectHandler : IRequestHandler
    {
        public string ToolName => "unity_delete_gameobject";

        public object Handle(JObject @params)
        {
            var go = GameObjectResolver.Resolve(@params);
            var includeChildren = @params?["includeChildren"]?.Value<bool>() ?? true;
            var dryRun = @params?["dryRun"]?.Value<bool>() ?? false;
            var name = go.name;
            var path = GameObjectResolver.GetPath(go);

            if (dryRun)
            {
                var childCount = go.transform.childCount;
                var componentCount = go.GetComponents<Component>().Length;
                return new
                {
                    dryRun = true,
                    target = name,
                    path,
                    childCount,
                    componentCount,
                    includeChildren
                };
            }

            if (!includeChildren && go.transform.childCount > 0)
            {
                var parent = go.transform.parent;
                var children = new Transform[go.transform.childCount];
                for (int i = 0; i < go.transform.childCount; i++)
                    children[i] = go.transform.GetChild(i);
                foreach (var child in children)
                    UndoHelper.SetTransformParent(child, parent, $"Reparent {child.name}");
            }

            UndoHelper.DestroyObject(go, $"Delete {name}");

            return new { deleted = name, path, deletedCount = 1 };
        }
    }
}
