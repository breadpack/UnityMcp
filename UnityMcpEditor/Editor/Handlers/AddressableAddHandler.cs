#if UNITY_MCP_ADDRESSABLES
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;

namespace BreadPack.Mcp.Unity
{
    public class AddressableAddHandler : IRequestHandler
    {
        public string ToolName => "unity_addressable_add";

        public object Handle(JObject @params)
        {
            var asset = AssetResolver.Resolve(@params);
            var assetPath = AssetDatabase.GetAssetPath(asset);
            var guid = AssetDatabase.AssetPathToGUID(assetPath);

            var groupName = @params?["groupName"]?.Value<string>();
            var address = @params?["address"]?.Value<string>();

            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
                throw new System.InvalidOperationException(
                    "Addressable Asset Settings not found. Initialize Addressables first.");

            AddressableAssetGroup group;
            if (!string.IsNullOrEmpty(groupName))
            {
                group = settings.FindGroup(groupName);
                if (group == null)
                    throw new System.ArgumentException(
                        $"Addressable group not found: {groupName}");
            }
            else
            {
                group = settings.DefaultGroup;
            }

            var existingEntry = settings.FindAssetEntry(guid);
            if (existingEntry != null)
            {
                if (!string.IsNullOrEmpty(address))
                    existingEntry.address = address;

                return new
                {
                    assetPath,
                    address = existingEntry.address,
                    group = existingEntry.parentGroup.Name,
                    alreadyRegistered = true
                };
            }

            var entry = settings.CreateOrMoveEntry(guid, group, false, false);
            if (!string.IsNullOrEmpty(address))
                entry.address = address;

            settings.SetDirty(
                AddressableAssetSettings.ModificationEvent.EntryCreated, entry, true);

            return new
            {
                assetPath,
                address = entry.address,
                group = group.Name,
                alreadyRegistered = false
            };
        }
    }
}
#endif
