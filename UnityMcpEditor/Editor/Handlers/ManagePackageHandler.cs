using System;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor.PackageManager;

namespace BreadPack.Mcp.Unity
{
    public class ManagePackageHandler : IAsyncRequestHandler
    {
        public string ToolName => "unity_manage_package";

        public async Task<object> HandleAsync(JObject @params)
        {
            var action = @params?["action"]?.Value<string>();
            var packageId = @params?["packageId"]?.Value<string>();

            switch (action)
            {
                case "list":
                    return await ListPackages();

                case "add":
                    if (string.IsNullOrEmpty(packageId))
                        throw new ArgumentException("packageId is required for 'add' action");
                    return await AddPackage(packageId);

                case "remove":
                    if (string.IsNullOrEmpty(packageId))
                        throw new ArgumentException("packageId is required for 'remove' action");
                    return await RemovePackage(packageId);

                default:
                    throw new ArgumentException($"Unknown action: {action}. Use 'list', 'add', or 'remove'");
            }
        }

        private static async Task<object> ListPackages()
        {
            var listRequest = Client.List(true);

            while (!listRequest.IsCompleted)
                await Task.Yield();

            if (listRequest.Status == StatusCode.Failure)
                throw new Exception($"패키지 목록 조회 실패: {listRequest.Error.message}");

            var packages = listRequest.Result.Select(p => new
            {
                name = p.name,
                version = p.version,
                displayName = p.displayName,
                source = p.source.ToString(),
                status = p.status.ToString()
            }).ToList();

            return new { action = "list", count = packages.Count, packages };
        }

        private static async Task<object> AddPackage(string packageId)
        {
            var addRequest = Client.Add(packageId);

            while (!addRequest.IsCompleted)
                await Task.Yield();

            if (addRequest.Status == StatusCode.Failure)
                throw new Exception($"패키지 추가 실패: {addRequest.Error.message}");

            return new
            {
                action = "add",
                packageId,
                name = addRequest.Result.name,
                version = addRequest.Result.version
            };
        }

        private static async Task<object> RemovePackage(string packageId)
        {
            var removeRequest = Client.Remove(packageId);

            while (!removeRequest.IsCompleted)
                await Task.Yield();

            if (removeRequest.Status == StatusCode.Failure)
                throw new Exception($"패키지 제거 실패: {removeRequest.Error.message}");

            return new { action = "remove", packageId };
        }
    }
}
