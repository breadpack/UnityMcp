using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace BreadPack.Mcp.Unity.Input
{
    public static class InputBackendRouter
    {
        private static List<IInputBackend> _backends;

        static InputBackendRouter()
        {
            AssemblyReloadEvents.afterAssemblyReload += Reset;
            EditorApplication.playModeStateChanged += _ => Reset();
        }

        public static IInputBackend Resolve(InputCapabilities capability, ResolvedTarget target = null)
        {
            InputSystemGuard.EnsurePlayMode();
            if (target != null) InputSystemGuard.EnsureReady(target.Kind);

            var backend = GetBackends()
                .Where(candidate => candidate.Supports(capability, target))
                .OrderByDescending(candidate => candidate.Priority)
                .FirstOrDefault();

            if (backend == null)
            {
                var targetDescription = target == null ? "현재 프로젝트" : $"{target.Kind} 타깃";
                throw new NotSupportedException(
                    $"{targetDescription}에서 {capability} 입력을 주입할 수 있는 backend가 없습니다. " +
                    "Legacy Input은 uGUI 의미 이벤트만 지원하며, raw 키보드/터치 입력에는 New Input System이 필요합니다.");
            }

            backend.EnsureReady(capability, target);
            return backend;
        }

        internal static void Reset()
        {
            _backends = null;
        }

        public static JObject AddMetadata(JObject json, IInputBackend backend)
        {
            json["inputBackend"] = backend.Name;
            json["delivery"] = backend.Delivery;
            return json;
        }

        private static IReadOnlyList<IInputBackend> GetBackends()
        {
            if (_backends != null) return _backends;

            _backends = new List<IInputBackend>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var assemblyName = assembly.GetName().Name;
                if (string.IsNullOrEmpty(assemblyName)
                    || !assemblyName.StartsWith("BreadPack.Mcp.Unity", StringComparison.Ordinal))
                    continue;

                foreach (var type in GetLoadableTypes(assembly))
                {
                    if (type.IsAbstract || type.IsInterface || !typeof(IInputBackend).IsAssignableFrom(type))
                        continue;

                    if (type.GetConstructor(Type.EmptyTypes) == null)
                        continue;

                    _backends.Add((IInputBackend)Activator.CreateInstance(type));
                }
            }

            return _backends;
        }

        private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(type => type != null);
            }
        }
    }
}
