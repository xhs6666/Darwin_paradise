using UnityEngine;

namespace DarwinPark.Planet
{
    // ✦ 新增：地理数据由星球局部坐标派生，移动、旋转、缩放和读档后不会沿用旧世界坐标。
    public readonly struct PlanetCoordinate
    {
        public readonly Vector3 LocalPosition, Direction;
        public readonly float GroundRadius, WaterRadius;
        public readonly int Cell, Region;
        public Vector2 UV => PlanetTerrainSampler.DirectionToUV(Direction);
        public float Longitude => (UV.x - .5f) * 360f;
        public float Latitude => (UV.y - .5f) * 180f;
        public float Altitude => LocalPosition.magnitude - GroundRadius;
        public float WaterDepth => Mathf.Max(0, WaterRadius - GroundRadius);
        public float Immersion => WaterRadius - LocalPosition.magnitude;
        public bool IsWater => WaterDepth > 0;

        private PlanetCoordinate(PlanetTerrainData data, Vector3 local)
        {
            LocalPosition = local;
            Direction = local.sqrMagnitude > 1e-12f ? local.normalized : Vector3.up;
            GroundRadius = SurfaceRadius(data, Direction);
            WaterRadius = WaterSurfaceRadius(data, Direction);
            Cell = data.Navigation != null ? data.Navigation.Cell(Direction) : -1;
            Region = Cell >= 0 ? data.Navigation.Regions[Cell] : 0;
        }

        public static PlanetCoordinate At(PlanetTerrainMeshBuilder planet, Vector3 worldPosition)
            => AtLocal(planet.Data, planet.transform.InverseTransformPoint(worldPosition));

        public static PlanetCoordinate AtLocal(PlanetTerrainData data, Vector3 localPosition)
            => new PlanetCoordinate(data, localPosition);

        // ✦ 修复（Bug回归）：寻路和受伤都取实际三角面，避免海底插值高度被误认为陆地。
        public static float SurfaceRadius(PlanetTerrainData data, Vector3 direction)
            => RadialTerrainSurface.TrySampleLocal(data, direction, out var hit)
                ? hit.point.magnitude
                : data.Radius + PlanetTerrainSampler.SampleHeight(data, direction) * data.HeightScale;

        public static float WaterSurfaceRadius(PlanetTerrainData data, Vector3 direction)
            => data.Radius + (data.Hydrology?.Water(direction) ?? data.SeaLevel) * data.HeightScale;
    }
}
