#if UNITY_MCP_ADDRESSABLES
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;

namespace UnityMcp.Editor
{
    public class AddressableSetAddressHandler : IRequestHandler
    {
        public string ToolName => "unity_addressable_set_address";

        public object Handle(JObject @params)
        {
            var asset = AssetResolver.Resolve(@params);
            var assetPath = AssetDatabase.GetAssetPath(asset);
            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            var address = @params?["address"]?.Value<string>();
            var labelsToken = @params?["labels"] as JArray;

            if (string.IsNullOrEmpty(address))
                throw new System.ArgumentException("address is required");

            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
                throw new System.InvalidOperationException(
                    "Addressable Asset Settings not found");

            var entry = settings.FindAssetEntry(guid);
            if (entry == null)
                throw new System.ArgumentException(
                    $"Asset is not registered as Addressable: {assetPath}");

            entry.address = address;

            if (labelsToken != null)
            {
                var labels = labelsToken.Select(t => t.Value<string>()).ToList();

                foreach (var existing in entry.labels.ToList())
                    entry.SetLabel(existing, false);

                foreach (var label in labels)
                {
                    settings.AddLabel(label);
                    entry.SetLabel(label, true);
                }
            }

            settings.SetDirty(
                AddressableAssetSettings.ModificationEvent.EntryModified, entry, true);

            return new
            {
                assetPath,
                address = entry.address,
                labels = entry.labels.ToList(),
                group = entry.parentGroup.Name
            };
        }
    }
}
#endif
