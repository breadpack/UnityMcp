using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace BreadPack.Mcp.Unity
{
    public class AnimatorControlHandler : IRequestHandler
    {
        public string ToolName => "unity_animator_control";

        public object Handle(JObject @params)
        {
            if (!EditorApplication.isPlaying)
                throw new System.InvalidOperationException("This tool requires Play Mode. Enter Play Mode first.");

            var action = @params?["action"]?.Value<string>();
            if (string.IsNullOrEmpty(action))
                throw new System.ArgumentException("action is required");

            var go = GameObjectResolver.Resolve(@params);
            var animator = go.GetComponent<Animator>();
            if (animator == null)
                throw new System.InvalidOperationException($"No Animator component found on '{go.name}'");

            switch (action)
            {
                case "set_bool":
                    return HandleSetBool(animator, @params);
                case "set_int":
                    return HandleSetInt(animator, @params);
                case "set_float":
                    return HandleSetFloat(animator, @params);
                case "set_trigger":
                    return HandleSetTrigger(animator, @params);
                case "reset_trigger":
                    return HandleResetTrigger(animator, @params);
                case "get_parameters":
                    return HandleGetParameters(animator);
                case "get_current_state":
                    return HandleGetCurrentState(animator);
                default:
                    throw new System.ArgumentException(
                        $"Unknown action '{action}'. Valid actions: set_bool, set_int, set_float, set_trigger, reset_trigger, get_parameters, get_current_state");
            }
        }

        private static (string name, string value) GetRequiredParams(JObject @params)
        {
            var name = @params?["parameterName"]?.Value<string>();
            if (string.IsNullOrEmpty(name))
                throw new System.ArgumentException("parameterName is required for set_* actions");

            var value = @params?["value"]?.Value<string>();
            if (string.IsNullOrEmpty(value))
                throw new System.ArgumentException("value is required for this action");

            return (name, value);
        }

        private static string GetRequiredName(JObject @params)
        {
            var name = @params?["parameterName"]?.Value<string>();
            if (string.IsNullOrEmpty(name))
                throw new System.ArgumentException("parameterName is required for this action");
            return name;
        }

        private static object HandleSetBool(Animator animator, JObject @params)
        {
            var (name, value) = GetRequiredParams(@params);
            var boolValue = bool.Parse(value);
            animator.SetBool(name, boolValue);
            return new { action = "set_bool", parameterName = name, value = boolValue };
        }

        private static object HandleSetInt(Animator animator, JObject @params)
        {
            var (name, value) = GetRequiredParams(@params);
            var intValue = int.Parse(value);
            animator.SetInteger(name, intValue);
            return new { action = "set_int", parameterName = name, value = intValue };
        }

        private static object HandleSetFloat(Animator animator, JObject @params)
        {
            var (name, value) = GetRequiredParams(@params);
            var floatValue = float.Parse(value);
            animator.SetFloat(name, floatValue);
            return new { action = "set_float", parameterName = name, value = floatValue };
        }

        private static object HandleSetTrigger(Animator animator, JObject @params)
        {
            var name = GetRequiredName(@params);
            animator.SetTrigger(name);
            return new { action = "set_trigger", parameterName = name };
        }

        private static object HandleResetTrigger(Animator animator, JObject @params)
        {
            var name = GetRequiredName(@params);
            animator.ResetTrigger(name);
            return new { action = "reset_trigger", parameterName = name };
        }

        private static object HandleGetParameters(Animator animator)
        {
            var parameters = new JArray();
            foreach (var param in animator.parameters)
            {
                var entry = new JObject
                {
                    ["name"] = param.name,
                    ["type"] = param.type.ToString()
                };

                switch (param.type)
                {
                    case AnimatorControllerParameterType.Bool:
                        entry["defaultBool"] = param.defaultBool;
                        entry["currentValue"] = animator.GetBool(param.name);
                        break;
                    case AnimatorControllerParameterType.Int:
                        entry["defaultInt"] = param.defaultInt;
                        entry["currentValue"] = animator.GetInteger(param.name);
                        break;
                    case AnimatorControllerParameterType.Float:
                        entry["defaultFloat"] = param.defaultFloat;
                        entry["currentValue"] = animator.GetFloat(param.name);
                        break;
                    case AnimatorControllerParameterType.Trigger:
                        entry["currentValue"] = animator.GetBool(param.name);
                        break;
                }

                parameters.Add(entry);
            }

            return new { action = "get_parameters", parameters };
        }

        private static object HandleGetCurrentState(Animator animator)
        {
            var stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            return new
            {
                action = "get_current_state",
                layerIndex = 0,
                fullPathHash = stateInfo.fullPathHash,
                shortNameHash = stateInfo.shortNameHash,
                normalizedTime = stateInfo.normalizedTime,
                length = stateInfo.length,
                speed = stateInfo.speed,
                isLooping = stateInfo.loop
            };
        }
    }
}
