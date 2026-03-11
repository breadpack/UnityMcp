using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace UnityMcp.Editor
{
    public interface IAsyncRequestHandler : IRequestHandler
    {
        UniTask<object> HandleAsync(JObject @params);

        // 동기 Handle은 사용하지 않으므로 기본 구현으로 예외 발생
        object IRequestHandler.Handle(JObject @params)
            => throw new System.NotSupportedException("Use HandleAsync instead");
    }
}
