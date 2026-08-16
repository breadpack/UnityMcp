using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace BreadPack.Mcp.Unity
{
    /// <summary>
    /// AnimationClip 자산을 Edit Mode 에서 직접 편집한다. 커브는 EditorCurveBinding 기준으로
    /// targetPath(클립 루트 기준 상대 경로) + componentType + propertyPath 로 식별하며,
    /// propertyPath 는 Unity 직렬화 프로퍼티명(m_Alpha, m_AnchoredPosition.x 등)을 그대로 쓴다.
    /// GameObject 활성 토글(m_IsActive)은 componentType="GameObject"로 바인딩한다(Component 가 아님).
    /// ObjectReference 커브(스프라이트 교체 등) 쓰기는 미지원 — get_info 로 존재 여부만 확인 가능.
    /// </summary>
    public class AnimationClipHandler : IRequestHandler
    {
        public string ToolName => "unity_animation_clip";

        public object Handle(JObject @params)
        {
            var action = @params?["action"]?.Value<string>();
            if (string.IsNullOrEmpty(action))
                throw new ArgumentException("action is required");

            switch (action)
            {
                case "create": return HandleCreate(@params);
                case "get_info": return HandleGetInfo(@params);
                case "get_curve": return HandleGetCurve(@params);
                case "set_curve": return HandleSetCurve(@params);
                case "remove_curve": return HandleRemoveCurve(@params);
                case "set_settings": return HandleSetSettings(@params);
                case "sample": return HandleSample(@params);
                case "stop_sample": return HandleStopSample();
                default:
                    throw new ArgumentException(
                        $"Unknown action '{action}'. Valid actions: create, get_info, get_curve, set_curve, " +
                        "remove_curve, set_settings, sample, stop_sample");
            }
        }

        // ---- create ----------------------------------------------------

        private static object HandleCreate(JObject @params)
        {
            var savePath = @params?["assetPath"]?.Value<string>();
            if (string.IsNullOrEmpty(savePath))
                throw new ArgumentException("assetPath is required");
            if (AssetDatabase.LoadAssetAtPath<AnimationClip>(savePath) != null)
                throw new ArgumentException($"An AnimationClip already exists at '{savePath}'");

            var clip = new AnimationClip
            {
                frameRate = @params?["frameRate"]?.Value<float?>() ?? 60f
            };

            if (@params?["loopTime"] != null)
            {
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = @params["loopTime"].Value<bool>();
                AnimationUtility.SetAnimationClipSettings(clip, settings);
            }

            var directory = System.IO.Path.GetDirectoryName(savePath);
            if (!string.IsNullOrEmpty(directory) && !AssetDatabase.IsValidFolder(directory))
                CreateFolderRecursive(directory);

            AssetDatabase.CreateAsset(clip, savePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return new
            {
                assetPath = savePath,
                guid = AssetDatabase.AssetPathToGUID(savePath),
                frameRate = clip.frameRate
            };
        }

        // ---- get_info ----------------------------------------------------

        private static object HandleGetInfo(JObject @params)
        {
            var (clip, assetPath) = ResolveClip(@params);
            var settings = AnimationUtility.GetAnimationClipSettings(clip);

            var curves = new JArray();
            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            {
                var curve = AnimationUtility.GetEditorCurve(clip, binding);
                curves.Add(new JObject
                {
                    ["targetPath"] = binding.path,
                    ["componentType"] = binding.type?.Name,
                    ["propertyPath"] = binding.propertyName,
                    ["keyCount"] = curve?.length ?? 0,
                    ["isObjectReference"] = false
                });
            }
            foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
            {
                var objCurve = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                curves.Add(new JObject
                {
                    ["targetPath"] = binding.path,
                    ["componentType"] = binding.type?.Name,
                    ["propertyPath"] = binding.propertyName,
                    ["keyCount"] = objCurve?.Length ?? 0,
                    ["isObjectReference"] = true
                });
            }

            return new
            {
                assetPath,
                guid = AssetDatabase.AssetPathToGUID(assetPath),
                length = clip.length,
                frameRate = clip.frameRate,
                empty = clip.empty,
                legacy = clip.legacy,
                loopTime = settings.loopTime,
                loopBlend = settings.loopBlend,
                cycleOffset = settings.cycleOffset,
                startTime = settings.startTime,
                stopTime = settings.stopTime,
                curves
            };
        }

        // ---- get_curve / set_curve / remove_curve ------------------------

        private static object HandleGetCurve(JObject @params)
        {
            var (clip, assetPath) = ResolveClip(@params);
            var binding = ResolveBinding(@params);

            var curve = AnimationUtility.GetEditorCurve(clip, binding);
            if (curve == null)
                throw new ArgumentException(
                    $"No curve found for targetPath='{binding.path}' componentType='{binding.type?.Name}' " +
                    $"propertyPath='{binding.propertyName}'. Use get_info to list existing curves.");

            return new
            {
                assetPath,
                targetPath = binding.path,
                componentType = binding.type?.Name,
                propertyPath = binding.propertyName,
                keys = SerializeKeys(curve)
            };
        }

        private static object HandleSetCurve(JObject @params)
        {
            var (clip, assetPath) = ResolveClip(@params);
            var binding = ResolveBinding(@params);

            var keysArray = @params?["keys"] as JArray;
            if (keysArray == null || keysArray.Count == 0)
                throw new ArgumentException("'keys' must be a non-empty array");

            var tangentMode = @params?["tangentMode"]?.Value<string>();
            var curve = BuildCurve(keysArray, tangentMode);

            AnimationUtility.SetEditorCurve(clip, binding, curve);
            EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssets();

            return new
            {
                assetPath,
                targetPath = binding.path,
                componentType = binding.type?.Name,
                propertyPath = binding.propertyName,
                keyCount = curve.length
            };
        }

        private static object HandleRemoveCurve(JObject @params)
        {
            var (clip, assetPath) = ResolveClip(@params);
            var binding = ResolveBinding(@params);

            if (AnimationUtility.GetEditorCurve(clip, binding) == null)
                throw new ArgumentException(
                    $"No curve found for targetPath='{binding.path}' componentType='{binding.type?.Name}' " +
                    $"propertyPath='{binding.propertyName}'.");

            AnimationUtility.SetEditorCurve(clip, binding, null);
            EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssets();

            return new { assetPath, removed = true, targetPath = binding.path, propertyPath = binding.propertyName };
        }

        // ---- set_settings ----------------------------------------------------

        private static object HandleSetSettings(JObject @params)
        {
            var (clip, assetPath) = ResolveClip(@params);
            var settings = AnimationUtility.GetAnimationClipSettings(clip);

            if (@params?["loopTime"] != null) settings.loopTime = @params["loopTime"].Value<bool>();
            if (@params?["loopBlend"] != null) settings.loopBlend = @params["loopBlend"].Value<bool>();
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            if (@params?["frameRate"] != null)
                clip.frameRate = @params["frameRate"].Value<float>();

            EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssets();

            return new
            {
                assetPath,
                frameRate = clip.frameRate,
                loopTime = settings.loopTime,
                loopBlend = settings.loopBlend
            };
        }

        // ---- sample / stop_sample ----------------------------------------------------

        private static object HandleSample(JObject @params)
        {
            var (clip, assetPath) = ResolveClip(@params);
            var go = GameObjectResolver.Resolve(@params);
            var time = @params?["time"]?.Value<float?>() ?? 0f;

            if (!AnimationMode.InAnimationMode())
                AnimationMode.StartAnimationMode();

            AnimationMode.BeginSampling();
            try
            {
                AnimationMode.SampleAnimationClip(go, clip, time);
            }
            finally
            {
                AnimationMode.EndSampling();
            }
            UnityEditor.SceneView.RepaintAll();

            return new { assetPath, target = go.name, time, inAnimationMode = true };
        }

        private static object HandleStopSample()
        {
            var wasActive = AnimationMode.InAnimationMode();
            if (wasActive)
                AnimationMode.StopAnimationMode();
            return new { stopped = wasActive };
        }

        // ---- helpers ----------------------------------------------------

        private static (AnimationClip clip, string assetPath) ResolveClip(JObject @params)
        {
            var asset = AssetResolver.Resolve(@params);
            if (asset is not AnimationClip clip)
                throw new ArgumentException($"Asset is not an AnimationClip: '{AssetDatabase.GetAssetPath(asset)}'");
            return (clip, AssetDatabase.GetAssetPath(clip));
        }

        private static EditorCurveBinding ResolveBinding(JObject @params)
        {
            var targetPath = @params?["targetPath"]?.Value<string>() ?? "";
            var componentTypeName = @params?["componentType"]?.Value<string>();
            var propertyPath = @params?["propertyPath"]?.Value<string>();

            if (string.IsNullOrEmpty(componentTypeName))
                throw new ArgumentException("componentType is required");
            if (string.IsNullOrEmpty(propertyPath))
                throw new ArgumentException("propertyPath is required");

            var type = ResolveBindingType(componentTypeName);
            return EditorCurveBinding.FloatCurve(targetPath, type, propertyPath);
        }

        private static Type ResolveBindingType(string componentType)
        {
            if (string.Equals(componentType, "GameObject", StringComparison.OrdinalIgnoreCase))
                return typeof(GameObject);
            return ComponentResolver.Resolve(componentType);
        }

        private static JArray SerializeKeys(AnimationCurve curve)
        {
            var keys = new JArray();
            for (int i = 0; i < curve.length; i++)
            {
                var k = curve[i];
                keys.Add(new JObject
                {
                    ["time"] = k.time,
                    ["value"] = k.value,
                    ["inTangent"] = k.inTangent,
                    ["outTangent"] = k.outTangent
                });
            }
            return keys;
        }

        private static AnimationCurve BuildCurve(JArray keysArray, string tangentMode)
        {
            var keyframes = new List<Keyframe>();
            var explicitTangent = new List<bool>();
            foreach (var token in keysArray)
            {
                if (token is not JObject k)
                    throw new ArgumentException("Each key must be an object {time, value, ...}");
                if (k["time"] == null || k["value"] == null)
                    throw new ArgumentException("Each key requires 'time' and 'value'");

                var kf = new Keyframe(k["time"].Value<float>(), k["value"].Value<float>());
                bool hasExplicit = k["inTangent"] != null || k["outTangent"] != null;
                if (k["inTangent"] != null) kf.inTangent = k["inTangent"].Value<float>();
                if (k["outTangent"] != null) kf.outTangent = k["outTangent"].Value<float>();

                keyframes.Add(kf);
                explicitTangent.Add(hasExplicit);
            }

            var curve = new AnimationCurve(keyframes.ToArray());

            var mode = string.IsNullOrEmpty(tangentMode) ? "auto" : tangentMode.ToLowerInvariant();
            for (int i = 0; i < curve.length; i++)
            {
                if (explicitTangent[i])
                {
                    AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Free);
                    AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Free);
                    continue;
                }

                var tangentModeValue = mode switch
                {
                    "linear" => AnimationUtility.TangentMode.Linear,
                    "constant" => AnimationUtility.TangentMode.Constant,
                    "auto" => AnimationUtility.TangentMode.ClampedAuto,
                    _ => throw new ArgumentException($"Unknown tangentMode '{tangentMode}'. Valid: auto, linear, constant")
                };
                AnimationUtility.SetKeyLeftTangentMode(curve, i, tangentModeValue);
                AnimationUtility.SetKeyRightTangentMode(curve, i, tangentModeValue);
            }

            return curve;
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
