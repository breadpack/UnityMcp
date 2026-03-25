using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace BreadPack.Mcp.Unity
{
    public class FindAssetsHandler : IRequestHandler
    {
        public string ToolName => "unity_find_assets";

        public object Handle(JObject @params)
        {
            string filter = @params?["filter"]?.Value<string>();
            if (string.IsNullOrEmpty(filter))
                throw new ArgumentException("'filter' parameter is required.");

            string searchInFolders = @params?["searchInFolders"]?.Value<string>();
            int maxResults = @params?["maxResults"]?.Value<int>() ?? 50;

            string[] guids;
            if (!string.IsNullOrEmpty(searchInFolders))
            {
                string[] folders = searchInFolders
                    .Split(',')
                    .Select(f => f.Trim())
                    .Where(f => !string.IsNullOrEmpty(f))
                    .ToArray();
                guids = AssetDatabase.FindAssets(filter, folders);
            }
            else
            {
                guids = AssetDatabase.FindAssets(filter);
            }

            int totalFound = guids.Length;
            var results = new List<object>();

            foreach (string guid in guids.Take(maxResults))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Type assetType = AssetDatabase.GetMainAssetTypeAtPath(path);

                results.Add(new
                {
                    path,
                    guid,
                    assetType = assetType != null ? assetType.Name : "Unknown"
                });
            }

            return new
            {
                results,
                totalFound,
                returnedCount = results.Count
            };
        }
    }
}
