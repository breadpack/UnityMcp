using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace BreadPack.Mcp.Unity
{
    /// <summary>
    /// AnimatorController 자산을 Edit Mode 에서 직접 편집한다. 상태 그래프는 layerIndex(기본 0)
    /// 레이어의 stateMachine 을 대상으로 하며, add_transition 의 from="AnyState" 는
    /// AnyState 전이로 취급한다. motion 연결은 unity_animation_clip 으로 만든 클립을 참조한다.
    /// </summary>
    public class AnimatorControllerHandler : IRequestHandler
    {
        public string ToolName => "unity_animator_controller";

        public object Handle(JObject @params)
        {
            var action = @params?["action"]?.Value<string>();
            if (string.IsNullOrEmpty(action))
                throw new ArgumentException("action is required");

            switch (action)
            {
                case "create": return HandleCreate(@params);
                case "get_info": return HandleGetInfo(@params);
                case "add_state": return HandleAddState(@params);
                case "remove_state": return HandleRemoveState(@params);
                case "set_state_motion": return HandleSetStateMotion(@params);
                case "add_transition": return HandleAddTransition(@params);
                case "remove_transition": return HandleRemoveTransition(@params);
                case "add_parameter": return HandleAddParameter(@params);
                case "remove_parameter": return HandleRemoveParameter(@params);
                case "assign": return HandleAssign(@params);
                default:
                    throw new ArgumentException(
                        $"Unknown action '{action}'. Valid actions: create, get_info, add_state, remove_state, " +
                        "set_state_motion, add_transition, remove_transition, add_parameter, remove_parameter, assign");
            }
        }

        // ---- create / get_info ----------------------------------------------------

        private static object HandleCreate(JObject @params)
        {
            var savePath = @params?["assetPath"]?.Value<string>();
            if (string.IsNullOrEmpty(savePath))
                throw new ArgumentException("assetPath is required");
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(savePath) != null)
                throw new ArgumentException($"An AnimatorController already exists at '{savePath}'");

            var directory = System.IO.Path.GetDirectoryName(savePath);
            if (!string.IsNullOrEmpty(directory) && !AssetDatabase.IsValidFolder(directory))
                CreateFolderRecursive(directory);

            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(savePath);

            return new
            {
                assetPath = savePath,
                guid = AssetDatabase.AssetPathToGUID(savePath),
                layers = ctrl.layers.Select(l => l.name).ToArray()
            };
        }

        private static object HandleGetInfo(JObject @params)
        {
            var (ctrl, assetPath) = ResolveController(@params);

            var layers = new JArray();
            foreach (var layer in ctrl.layers)
            {
                var sm = layer.stateMachine;
                var states = new JArray();
                foreach (var cs in sm.states)
                {
                    var state = cs.state;
                    states.Add(new JObject
                    {
                        ["name"] = state.name,
                        ["isDefault"] = sm.defaultState == state,
                        ["motionPath"] = state.motion != null ? AssetDatabase.GetAssetPath(state.motion) : null,
                        ["speed"] = state.speed,
                        ["tag"] = state.tag
                    });
                }

                var transitions = new JArray();
                foreach (var t in sm.anyStateTransitions)
                    transitions.Add(SerializeTransition("AnyState", t));
                foreach (var cs in sm.states)
                    foreach (var t in cs.state.transitions)
                        transitions.Add(SerializeTransition(cs.state.name, t));

                layers.Add(new JObject
                {
                    ["name"] = layer.name,
                    ["defaultState"] = sm.defaultState != null ? sm.defaultState.name : null,
                    ["states"] = states,
                    ["transitions"] = transitions
                });
            }

            var parameters = new JArray();
            foreach (var p in ctrl.parameters)
                parameters.Add(new JObject { ["name"] = p.name, ["type"] = p.type.ToString() });

            return new
            {
                assetPath,
                guid = AssetDatabase.AssetPathToGUID(assetPath),
                layers,
                parameters
            };
        }

        // ---- state ----------------------------------------------------

        private static object HandleAddState(JObject @params)
        {
            var (ctrl, _) = ResolveController(@params);
            var sm = ResolveStateMachine(ctrl, @params);
            var name = RequireString(@params, "name");

            if (FindState(sm, name) != null)
                throw new ArgumentException($"State '{name}' already exists");

            var position = new Vector3(300, 50 + sm.states.Length * 60, 0);
            var state = sm.AddState(name, position);

            var motionPath = @params?["motionPath"]?.Value<string>();
            if (!string.IsNullOrEmpty(motionPath))
                state.motion = LoadClip(motionPath);

            var isDefault = @params?["isDefault"]?.Value<bool?>() ?? false;
            if (isDefault) sm.defaultState = state;

            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();

            return new
            {
                name = state.name,
                isDefault = sm.defaultState == state,
                motionPath = state.motion != null ? AssetDatabase.GetAssetPath(state.motion) : null
            };
        }

        private static object HandleRemoveState(JObject @params)
        {
            var (ctrl, _) = ResolveController(@params);
            var sm = ResolveStateMachine(ctrl, @params);
            var name = RequireString(@params, "name");
            var state = FindState(sm, name) ?? throw new ArgumentException($"State '{name}' not found");

            sm.RemoveState(state);
            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();

            return new { removed = name };
        }

        private static object HandleSetStateMotion(JObject @params)
        {
            var (ctrl, _) = ResolveController(@params);
            var sm = ResolveStateMachine(ctrl, @params);
            var name = RequireString(@params, "name");
            var state = FindState(sm, name) ?? throw new ArgumentException($"State '{name}' not found");
            var motionPath = RequireString(@params, "motionPath");

            state.motion = LoadClip(motionPath);
            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();

            return new { name, motionPath };
        }

        // ---- transition ----------------------------------------------------

        private static object HandleAddTransition(JObject @params)
        {
            var (ctrl, _) = ResolveController(@params);
            var sm = ResolveStateMachine(ctrl, @params);
            var from = RequireString(@params, "from");
            var to = RequireString(@params, "to");
            var toState = FindState(sm, to) ?? throw new ArgumentException($"State '{to}' not found");

            AnimatorStateTransition transition;
            if (IsAnyState(from))
            {
                transition = sm.AddAnyStateTransition(toState);
            }
            else
            {
                var fromState = FindState(sm, from) ?? throw new ArgumentException($"State '{from}' not found");
                transition = fromState.AddTransition(toState);
            }

            if (@params?["hasExitTime"] != null) transition.hasExitTime = @params["hasExitTime"].Value<bool>();
            if (@params?["exitTime"] != null) transition.exitTime = @params["exitTime"].Value<float>();
            if (@params?["duration"] != null) transition.duration = @params["duration"].Value<float>();

            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();

            return new
            {
                from,
                to,
                hasExitTime = transition.hasExitTime,
                exitTime = transition.exitTime,
                duration = transition.duration
            };
        }

        private static object HandleRemoveTransition(JObject @params)
        {
            var (ctrl, _) = ResolveController(@params);
            var sm = ResolveStateMachine(ctrl, @params);
            var from = RequireString(@params, "from");
            var to = RequireString(@params, "to");

            if (IsAnyState(from))
            {
                var t = sm.anyStateTransitions.FirstOrDefault(x => x.destinationState != null && x.destinationState.name == to);
                if (t == null) throw new ArgumentException($"AnyState transition to '{to}' not found");
                sm.RemoveAnyStateTransition(t);
            }
            else
            {
                var fromState = FindState(sm, from) ?? throw new ArgumentException($"State '{from}' not found");
                var t = fromState.transitions.FirstOrDefault(x => x.destinationState != null && x.destinationState.name == to);
                if (t == null) throw new ArgumentException($"Transition '{from}' -> '{to}' not found");
                fromState.RemoveTransition(t);
            }

            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();

            return new { removed = $"{from} -> {to}" };
        }

        // ---- parameter ----------------------------------------------------

        private static object HandleAddParameter(JObject @params)
        {
            var (ctrl, _) = ResolveController(@params);
            var name = RequireString(@params, "parameterName");
            var typeStr = RequireString(@params, "parameterType");

            if (ctrl.parameters.Any(p => p.name == name))
                throw new ArgumentException($"Parameter '{name}' already exists");

            var paramType = ParseParamType(typeStr);
            ctrl.AddParameter(name, paramType);

            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();

            return new { name, type = paramType.ToString() };
        }

        private static object HandleRemoveParameter(JObject @params)
        {
            var (ctrl, _) = ResolveController(@params);
            var name = RequireString(@params, "parameterName");
            var param = ctrl.parameters.FirstOrDefault(p => p.name == name)
                        ?? throw new ArgumentException($"Parameter '{name}' not found");

            ctrl.RemoveParameter(param);
            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();

            return new { removed = name };
        }

        // ---- assign ----------------------------------------------------

        private static object HandleAssign(JObject @params)
        {
            var (ctrl, assetPath) = ResolveController(@params);
            var go = GameObjectResolver.Resolve(@params);
            var animator = go.GetComponent<Animator>();
            if (animator == null)
                throw new InvalidOperationException(
                    $"No Animator component found on '{go.name}'. Use unity_add_component to add one first.");

            UndoHelper.RecordObject(animator, "Assign AnimatorController");
            animator.runtimeAnimatorController = ctrl;
            EditorUtility.SetDirty(animator);

            return new { target = go.name, controllerPath = assetPath };
        }

        // ---- helpers ----------------------------------------------------

        private static (AnimatorController ctrl, string assetPath) ResolveController(JObject @params)
        {
            var asset = AssetResolver.Resolve(@params);
            if (asset is not AnimatorController ctrl)
                throw new ArgumentException($"Asset is not an AnimatorController: '{AssetDatabase.GetAssetPath(asset)}'");
            return (ctrl, AssetDatabase.GetAssetPath(ctrl));
        }

        private static AnimatorStateMachine ResolveStateMachine(AnimatorController ctrl, JObject @params)
        {
            var layerIndex = @params?["layerIndex"]?.Value<int?>() ?? 0;
            if (layerIndex < 0 || layerIndex >= ctrl.layers.Length)
                throw new ArgumentException(
                    $"layerIndex {layerIndex} out of range (controller has {ctrl.layers.Length} layer(s))");
            return ctrl.layers[layerIndex].stateMachine;
        }

        private static AnimatorState FindState(AnimatorStateMachine sm, string name)
        {
            foreach (var cs in sm.states)
                if (cs.state.name == name) return cs.state;
            return null;
        }

        private static bool IsAnyState(string stateName) =>
            string.Equals(stateName, "AnyState", StringComparison.OrdinalIgnoreCase);

        private static AnimationClip LoadClip(string motionPath)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(motionPath);
            if (clip == null)
                throw new ArgumentException($"AnimationClip not found at '{motionPath}'");
            return clip;
        }

        private static string RequireString(JObject @params, string key)
        {
            var value = @params?[key]?.Value<string>();
            if (string.IsNullOrEmpty(value))
                throw new ArgumentException($"'{key}' is required");
            return value;
        }

        private static AnimatorControllerParameterType ParseParamType(string typeStr)
        {
            return typeStr.ToLowerInvariant() switch
            {
                "trigger" => AnimatorControllerParameterType.Trigger,
                "bool" => AnimatorControllerParameterType.Bool,
                "int" => AnimatorControllerParameterType.Int,
                "float" => AnimatorControllerParameterType.Float,
                _ => throw new ArgumentException($"Unknown parameter type '{typeStr}'. Valid: trigger, bool, int, float")
            };
        }

        private static JObject SerializeTransition(string from, AnimatorStateTransition t)
        {
            var conditions = new JArray();
            foreach (var c in t.conditions)
                conditions.Add(new JObject
                {
                    ["parameter"] = c.parameter,
                    ["mode"] = c.mode.ToString(),
                    ["threshold"] = c.threshold
                });

            return new JObject
            {
                ["from"] = from,
                ["to"] = t.destinationState != null ? t.destinationState.name : null,
                ["hasExitTime"] = t.hasExitTime,
                ["exitTime"] = t.exitTime,
                ["duration"] = t.duration,
                ["conditions"] = conditions
            };
        }

        private static void CreateFolderRecursive(string folderPath)
        {
            var parts = folderPath.Replace("\\", "/").Split('/');
            var current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
