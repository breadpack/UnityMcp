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
                case "save_and_exit":
                    return HandleSaveAndExit();
                case "discard_and_exit":
                    return HandleDiscardAndExit();
                case "status":
                    return HandleStatus();
                default:
                    throw new System.ArgumentException(
                        $"알 수 없는 action입니다: {action}. " +
                        "'enter', 'save', 'exit', 'save_and_exit', 'discard_and_exit', 'status' 중 하나를 사용하세요");
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
            var stage = RequireCurrentStage();
            var wasDirty = stage.scene.isDirty;
            SaveStage(stage);

            return new
            {
                action = "save",
                assetPath = stage.assetPath,
                saved = true,
                hadUnsavedChanges = wasDirty,
                hasUnsavedChanges = stage.scene.isDirty
            };
        }

        private object HandleExit()
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage == null)
            {
                return new
                {
                    action = "exit",
                    returned = true,
                    alreadyInMainStage = true
                };
            }

            if (stage.scene.isDirty)
            {
                throw new System.InvalidOperationException(
                    "UNSAVED_PREFAB_CHANGES: 저장되지 않은 Prefab 변경사항이 있어 종료하지 않았습니다. " +
                    "저장 후 종료하려면 action='save_and_exit', 폐기 후 종료하려면 " +
                    "action='discard_and_exit'를 사용하세요.");
            }

            return ExitStage(stage, "exit", saved: false, discarded: false);
        }

        private object HandleSaveAndExit()
        {
            var stage = RequireCurrentStage();
            var wasDirty = stage.scene.isDirty;
            SaveStage(stage);
            return ExitStage(
                stage,
                "save_and_exit",
                saved: true,
                discarded: false,
                hadUnsavedChanges: wasDirty);
        }

        private object HandleDiscardAndExit()
        {
            var stage = RequireCurrentStage();
            var wasDirty = stage.scene.isDirty;

            // Stage 전환 전에 dirty 플래그를 제거해야 Unity의 저장 확인 모달이 열리지 않는다.
            // 이후 GoToMainStage가 PrefabStage를 폐기하므로 디스크에는 변경사항이 기록되지 않는다.
            if (wasDirty)
                stage.ClearDirtiness();

            return ExitStage(
                stage,
                "discard_and_exit",
                saved: false,
                discarded: wasDirty,
                hadUnsavedChanges: wasDirty);
        }

        private static PrefabStage RequireCurrentStage()
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage == null)
                throw new System.InvalidOperationException("현재 Prefab 편집 모드가 아닙니다");
            return stage;
        }

        private static void SaveStage(PrefabStage stage)
        {
            if (EditorApplication.isCompiling)
            {
                throw new System.InvalidOperationException(
                    "PREFAB_SAVE_BLOCKED_COMPILING: 컴파일이 끝난 뒤 Prefab을 저장하세요.");
            }

            PrefabUtility.SaveAsPrefabAsset(
                stage.prefabContentsRoot,
                stage.assetPath,
                out var savedSuccessfully);

            if (!savedSuccessfully)
            {
                throw new System.InvalidOperationException(
                    $"PREFAB_SAVE_FAILED: Prefab 저장에 실패했습니다: {stage.assetPath}");
            }

            // SaveAsPrefabAsset만 호출하면 PrefabStage의 dirty 상태가 남을 수 있다.
            // Unity의 PrefabStage.SavePrefab 내부 구현과 동일하게 성공 후 명시적으로 정리한다.
            stage.ClearDirtiness();

            if (stage.scene.isDirty)
            {
                throw new System.InvalidOperationException(
                    $"PREFAB_DIRTY_CLEAR_FAILED: 저장 후 Prefab Stage의 dirty 상태를 제거하지 못했습니다: {stage.assetPath}");
            }
        }

        private static object ExitStage(
            PrefabStage stage,
            string action,
            bool saved,
            bool discarded,
            bool hadUnsavedChanges = false)
        {
            var assetPath = stage.assetPath;
            StageUtility.GoToMainStage();

            var currentStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (currentStage != null)
            {
                throw new System.InvalidOperationException(
                    $"PREFAB_STAGE_EXIT_FAILED: Prefab Stage를 종료하지 못했습니다: {currentStage.assetPath}");
            }

            return new
            {
                action,
                assetPath,
                returned = true,
                saved,
                discarded,
                hadUnsavedChanges
            };
        }
    }
}
