using System;
using System.Collections.Generic;

namespace DarwinPark.Entities
{
    /// <summary>
    /// 全局实体追踪。
    /// ★ 新增：有性繁殖找伴侣、统计、编辑器工具均依赖此注册表，避免到处 FindObjectsOfType。
    /// </summary>
    public static class EntityRegistry
    {
        private static readonly HashSet<DarwinEntity> _all = new();

        public static IReadOnlyCollection<DarwinEntity> All => _all;
        public static event Action<DarwinEntity> OnRegistered;
        public static event Action<DarwinEntity> OnUnregistered;

        public static void Register(DarwinEntity e)
        {
            if (e == null || !_all.Add(e)) return;
            // ★ 适配：先更新共享空间快照，再通知可能立即查询邻居的订阅方。
            Ecology.EntityNeighborhood.MembershipChanged(e,false);
            OnRegistered?.Invoke(e);
        }

        public static void Unregister(DarwinEntity e)
        {
            if (e == null || !_all.Remove(e)) return;
            Ecology.EntityNeighborhood.MembershipChanged(e,true);
            StationaryBodyRest.Remove(e.GetComponent<UnityEngine.Rigidbody>());
            OnUnregistered?.Invoke(e);
        }

        // ★ 适配：换世界同步释放空间缓存，不能保留上一局引用。
        public static void Clear(){_all.Clear();Ecology.EntityNeighborhood.Clear();StationaryBodyRest.Clear();}
    }
}
