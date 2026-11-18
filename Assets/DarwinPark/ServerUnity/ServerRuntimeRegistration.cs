using DarwinPark.UI;
using UnityEngine;

namespace DarwinPark.Server
{
    // ✦ 新增：组装发生在场景 Awake 之前，服务端实现不再与会话/客户端处于同一程序集。
    public static class ServerRuntimeRegistration
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            LanRuntimeServices.CreateServer=session=>new LanServerRuntime(session);
            LanRuntimeServices.ApplyAuthority=LanEditExecutor.Apply;
        }
    }
}
