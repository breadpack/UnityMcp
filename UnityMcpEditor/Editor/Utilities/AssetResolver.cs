using System;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace BreadPack.Mcp.Unity
{
    public static class AssetResolver
    {
        public static UnityEngine.Object Resolve(JObject @params, string pathKey = "assetPath", string guidKey = "assetGuid")
        {
            string assetPath = @params[pathKey]?.Value<string>();
            string assetGuid = @params[guidKey]?.Value<string>();
            return Resolve(assetPath, assetGuid);
        }

        public static UnityEngine.Object Resolve(string assetPath, string assetGuid)
        {
            // GUID takes priority
            if (!string.IsNullOrEmpty(assetGuid))
            {
                string pathFromGuid = AssetDatabase.GUIDToAssetPath(assetGuid);
                if (string.IsNullOrEmpty(pathFromGuid))
                    throw new ArgumentException($"No asset found for GUID: '{assetGuid}'");

                var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(pathFromGuid);
                if (asset == null)
                    throw new ArgumentException(
                        $"Asset at path '{pathFromGuid}' (GUID: '{assetGuid}') could not be loaded.");
                return asset;
            }

            if (!string.IsNullOrEmpty(assetPath))
            {
                var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
                if (asset == null)
                    throw new ArgumentException($"Asset not found at path: '{assetPath}'");
                return asset;
            }

            throw new ArgumentException(
                "Either an asset path or asset GUID must be specified.");
        }

        public static UnityEngine.Object ResolveFromToken(JObject token)
        {
            string assetPath = token["$asset"]?.Value<string>();
            string assetGuid = token["$guid"]?.Value<string>();
            return Resolve(assetPath, assetGuid);
        }

        public static bool IsAssetReference(JToken token)
        {
            if (token is JObject obj)
            {
                return obj["$asset"] != null || obj["$guid"] != null;
            }

            return false;
        }
    }
}
