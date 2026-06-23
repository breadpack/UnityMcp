using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BreadPack.Mcp.Unity
{
    public class PrefabEditHandler : IRequestHandler
    {
        public string ToolName => "unity_prefab_edit";

        public object Handle(JObject @params)
        {
            var action = @params?["action"]?.Value<string>();
            if (string.IsNullOrEmpty(action))
                throw new System.ArgumentException("action 파라미터가 필요합니다");

            switch (action)
            {
                case "enter":
                    return HandleEnter(@params);
                case "save":
                    return HandleSave();
                case "exit":
                    return HandleExit();
                case "status":
                    return HandleStatus();
                default:
                    throw new System.ArgumentException(
                        $"알 수 없는 action입니다: {action}. 'enter', 'save', 'exit', 'status' 중 하나를 사용하세요");
            }
        }

        private object HandleEnter(JObject @params)
        {
            var assetPath = @params?["assetPath"]?.Value<string>();
            if (string.IsNullOrEmpty(assetPath))
                throw new System.ArgumentException("enter 액션에는 assetPath가 필요합니다");

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab == null)
                throw new System.ArgumentException($"Prefab을 찾을 수 없습니다: {assetPath}");

            AssetDatabase.OpenAsset(prefab);

            // OpenAsset 은 동기로 스테이지를 연다 — 진입 직후 루트 핸들을 함께 반환해,
            // 에이전트가 get_hierarchy 없이도 바로 prefab 내부를 instanceId/path 로 가리킬 수 있게 한다.
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            var root = stage != null ? stage.prefabContentsRoot : null;

            return new
            {
                action = "enter",
                assetPath,
                opened = true,
                inPrefabMode = stage != null,
                rootName = root != null ? root.name : null,
                rootPath = root != null ? root.name : null,
                rootInstanceId = root != null ? root.GetInstanceID() : 0
            };
        }

        private object HandleStatus()
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage == null)
                return new { inPrefabMode = false };

            var root = stage.prefabContentsRoot;
            return new
            {
                inPrefabMode = true,
                assetPath = stage.assetPath,
                rootName = root != null ? root.name : null,
                rootPath = root != null ? root.name : null,
                rootInstanceId = root != null ? root.GetInstanceID() : 0,
                hasUnsavedChanges = stage.scene.isDirty
            };
        }

        private object HandleSave()
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage == null)
                throw new System.Exception("현재 Prefab 편집 모드가 아닙니다");

            PrefabUtility.SaveAsPrefabAsset(stage.prefabContentsRoot, stage.assetPath);

            return new
            {
                action = "save",
                assetPath = stage.assetPath,
                saved = true
            };
        }

        private object HandleExit()
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null)
                StageUtility.GoToMainStage();

            return new
            {
                action = "exit",
                returned = true
            };
        }
    }
}
