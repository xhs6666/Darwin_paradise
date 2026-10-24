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
            OnRegistered?.Invoke(e);
        }

        public static void Unregister(DarwinEntity e)
        {
            if (e == null || !_all.Remove(e)) return;
            OnUnregistered?.Invoke(e);
        }

        public static void Clear() => _all.Clear();
    }
}