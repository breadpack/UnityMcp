using System.Collections;
using UnityEngine;

namespace BreadPack.Mcp.Unity
{
    /// <summary>
    /// Play Mode PlayerLoop에서 WaitForEndOfFrame 코루틴을 실행하기 위한 임시 호스트.
    /// Editor 코드에서만 생성하며, 실제 코루틴은 PlayerLoop에서 실행된다.
    /// </summary>
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    public sealed class GameViewCaptureRunner : MonoBehaviour
    {
        public Coroutine Begin(IEnumerator routine)
        {
            return StartCoroutine(routine);
        }

        public void Cancel(Coroutine routine)
        {
            if (routine != null) StopCoroutine(routine);
        }
    }
}
