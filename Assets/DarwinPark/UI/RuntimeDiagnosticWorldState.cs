using DarwinPark.Core;
using DarwinPark.Entities;
using UnityEngine;

namespace DarwinPark.UI
{
    // ★ 新增：世界信息从上层注入，Core日志程序集不反向依赖实体/UI，独立场景也可以记录基础诊断。
    public static class RuntimeDiagnosticWorldState
    {
        private static WorldSessionManager _world;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register(){_world=null;RuntimeCrashDiagnostics.SampleState=Sample;}
        private static RuntimeCrashDiagnostics.Snapshot Sample()
        {
            if(_world==null)_world=Object.FindFirstObjectByType<WorldSessionManager>();
            var lan=LanSession.Instance;
            return new RuntimeCrashDiagnostics.Snapshot{worldId=_world?.Current?.id,worldName=_world?.Current?.name,
                loadingStatus=_world?.Status,saveStatus=_world?.SaveStatus,loading=_world!=null&&_world.Busy,entities=EntityRegistry.All.Count,
                networkRole=lan==null||!lan.Online?"单人":lan.IsHost?"房主":"成员",members=lan?.MemberCount??0};
        }
    }
}
