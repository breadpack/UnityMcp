using Newtonsoft.Json.Linq;
using UnityEditor;

namespace BreadPack.Mcp.Unity
{
    public class ManageAssetHandler : IRequestHandler
    {
        public string ToolName => "unity_manage_asset";

        public object Handle(JObject @params)
        {
            var action = @params?["action"]?.Value<string>()
                ?? throw new System.ArgumentException("action is required. Use 'move', 'copy', 'delete', or 'create_folder'");
            var assetPath = @params?["assetPath"]?.Value<string>()
                ?? throw new System.ArgumentException("assetPath is required");
            var destinationPath = @params?["destinationPath"]?.Value<string>();

            return action switch
            {
                "move" => HandleMove(assetPath, destinationPath),
                "copy" => HandleCopy(assetPath, destinationPath),
                "delete" => HandleDelete(assetPath),
                "create_folder" => HandleCreateFolder(assetPath),
                _ => throw new System.ArgumentException(
                    $"Unknown action: '{action}'. Use 'move', 'copy', 'delete', or 'create_folder'")
            };
        }

        private object HandleMove(string assetPath, string destinationPath)
        {
            if (string.IsNullOrEmpty(destinationPath))
                throw new System.ArgumentException("destinationPath is required for 'move' action");

            if (!AssetExists(assetPath))
                throw new System.ArgumentException($"Asset not found: {assetPath}");

            var validateResult = AssetDatabase.ValidateMoveAsset(assetPath, destinationPath);
            if (!string.IsNullOrEmpty(validateResult))
                throw new System.ArgumentException($"Cannot move asset: {validateResult}");

            var result = AssetDatabase.MoveAsset(assetPath, destinationPath);
            if (!string.IsNullOrEmpty(result))
                throw new System.Exception($"Failed to move asset: {result}");

            AssetDatabase.Refresh();

            return new
            {
                action = "move",
                path = destinationPath,
                success = true
            };
        }

        private object HandleCopy(string assetPath, string destinationPath)
        {
            if (string.IsNullOrEmpty(destinationPath))
                throw new System.ArgumentException("destinationPath is required for 'copy' action");

            if (!AssetExists(assetPath))
                throw new System.ArgumentException($"Asset not found: {assetPath}");

            var copied = AssetDatabase.CopyAsset(assetPath, destinationPath);
            if (!copied)
                throw new System.Exception($"Failed to copy asset from '{assetPath}' to '{destinationPath}'");

            AssetDatabase.Refresh();

            return new
            {
                action = "copy",
                path = destinationPath,
                success = true
            };
        }

        private object HandleDelete(string assetPath)
        {
            if (!AssetExists(assetPath))
                throw new System.ArgumentException($"Asset not found: {assetPath}");

            var deleted = AssetDatabase.DeleteAsset(assetPath);
            if (!deleted)
                throw new System.Exception($"Failed to delete asset: {assetPath}");

            AssetDatabase.Refresh();

            return new
            {
                action = "delete",
                path = assetPath,
                success = true
            };
        }

        private object HandleCreateFolder(string assetPath)
        {
            var lastSeparator = assetPath.LastIndexOf('/');
            if (lastSeparator < 0)
                throw new System.ArgumentException(
                    $"Invalid folder path: '{assetPath}'. Expected format: 'Assets/Parent/NewFolder'");

            var parentFolder = assetPath.Substring(0, lastSeparator);
            var newFolderName = assetPath.Substring(lastSeparator + 1);

            if (string.IsNullOrEmpty(newFolderName))
                throw new System.ArgumentException("Folder name cannot be empty");

            var guid = AssetDatabase.CreateFolder(parentFolder, newFolderName);
            if (string.IsNullOrEmpty(guid))
                throw new System.Exception(
                    $"Failed to create folder '{newFolderName}' in '{parentFolder}'");

            AssetDatabase.Refresh();

            return new
            {
                action = "create_folder",
                path = assetPath,
                success = true
            };
        }

        private bool AssetExists(string assetPath)
        {
            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            return !string.IsNullOrEmpty(guid);
        }
    }
}
