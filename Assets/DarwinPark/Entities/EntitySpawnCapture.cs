using DarwinPark.Core;
using DarwinPark.Genetics;
using UnityEngine;

namespace DarwinPark.Entities
{
    // ★ 修复（防Bug回归）：同步生成用注册事件取得身份，避免每个旧实体再次线性Contains旧数组的平方查找。
    public static class EntitySpawnCapture
    {
        public static long Captured { get; private set; }
        public static DarwinEntity Create(Genome genome, Vector3 position, string name = "")
        {
            DarwinEntity child = null;
            void Registered(DarwinEntity entity)
            {
                using var timing = PerformanceTimings.Measure(PerformanceTimings.Area.BirthIdentity);
                if (child == null) { child = entity; Captured++; }
            }
            EntityRegistry.OnRegistered += Registered;
            try { DarwinEntity.Spawner?.Invoke(genome, position, name); }
            finally { EntityRegistry.OnRegistered -= Registered; }
            return child;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => Captured = 0;
    }
}
