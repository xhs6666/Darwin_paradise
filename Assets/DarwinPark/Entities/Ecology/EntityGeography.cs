using System.Runtime.CompilerServices;
using DarwinPark.Planet;
using UnityEngine;

namespace DarwinPark.Entities.Ecology
{
    // ✦ 新增：按需绑定实体地理快照，位置、星球变换或地形重建任一改变即失效。
    public static class EntityGeography
    {
        private sealed class Entry
        {
            public PlanetTerrainData terrain;
            public LandNavigationGraph navigation;

            public Vector3 position;
            public PlanetCoordinate coordinate;
        }
        private static ConditionalWeakTable<EntityGeneController, Entry> _entries = new();

        public static bool TryRead(EntityGeneController genes, out PlanetCoordinate coordinate)
        {
            coordinate = default;
            var planet = EcologyQueries.Planet;
            if (genes == null || planet == null || planet.Data == null) return false;
            var entry = _entries.GetOrCreateValue(genes);
            // ✦ 新增：已登记实体读取共享坐标；测试探针/尚未完成登记的实体保留直接采样入口。
            var entity = DarwinPark.Core.OptionalComponent.Read<DarwinEntity>(genes);
            var position = entity != null && EntityCoordinateGrid.TryGet(entity, out var shared) ? shared.localPosition : planet.transform.InverseTransformPoint(genes.transform.position);
            if (entry.terrain != planet.Data || entry.navigation != planet.Data.Navigation ||
                entry.position != position)
            {
                entry.terrain = planet.Data;
                entry.navigation = planet.Data.Navigation;
                entry.position = position;
                entry.coordinate = PlanetCoordinate.AtLocal(planet.Data, position);
            }
            coordinate = entry.coordinate;
            return true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => _entries = new();
    }
}
