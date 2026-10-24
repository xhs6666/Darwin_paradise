using UnityEngine;

namespace DarwinPark.Planet
{
    /// <summary>
    /// ★ 新增：Cube-Sphere 数据采样工具。
    /// 气候图、环境回退、实体地表高度查询共用同一套 UV/六个面的映射，
    /// 避免各脚本各写一份导致接缝/错位（历史 Bug：气候图与网格面朝向不一致）。
    /// </summary>
    public static class PlanetTerrainSampler
    {
        /// <summary>等距柱状投影 UV → 球面单位方向。u=0/1 为同一条经线。</summary>
        public static Vector3 DirectionFromUV(float u, float v)
        {
            float lon = (u - 0.5f) * 2f * Mathf.PI;
            float lat = (v - 0.5f) * Mathf.PI;
            return new Vector3(
                Mathf.Cos(lat) * Mathf.Sin(lon),
                Mathf.Sin(lat),
                Mathf.Cos(lat) * Mathf.Cos(lon)).normalized;
        }

        /// <summary>球面单位方向 → 等距柱状投影 UV（与 Shader 的 sphereUV 对齐）。</summary>
        public static Vector2 DirectionToUV(Vector3 dir)
        {
            float u = Mathf.Atan2(dir.x, dir.z) / (2f * Mathf.PI) + 0.5f;
            float v = Mathf.Asin(Mathf.Clamp(dir.y, -1f, 1f)) / Mathf.PI + 0.5f;
            return new Vector2(Mathf.Repeat(u, 1f), Mathf.Clamp01(v));
        }

        /// <summary>按世界方向双线性采样 Cube-Sphere 高度。</summary>
        public static float SampleHeight(PlanetTerrainData data, Vector3 dir)
        {
            if (data == null || data.FaceHeights == null) return 0f;

            // ★ 优化：六个面法线就是 ±轴，主面 = 绝对值最大的轴分量。
            // 旧版每次采样做 6 次点乘，气候图每个像元要调用十几次，属于纯浪费。
            float ax = Mathf.Abs(dir.x);
            float ay = Mathf.Abs(dir.y);
            float az = Mathf.Abs(dir.z);
            int face;
            float maxDot;
            if (ax >= ay && ax >= az) { maxDot = ax; face = dir.x >= 0f ? 0 : 1; }
            else if (ay >= az) { maxDot = ay; face = dir.y >= 0f ? 2 : 3; }
            else { maxDot = az; face = dir.z >= 0f ? 4 : 5; }
            if (maxDot <= 0.001f) return 0f;

            Vector3 local = dir * (1f / maxDot);
            float u, v;
            if (face <= 1) { u = local.z; v = local.y; }        // ±X 面
            else if (face <= 3) { u = local.x; v = local.z; }   // ±Y 面
            else { u = local.x; v = local.y; }                   // ±Z 面

            int sub = data.Subdivisions;
            float fx = (u + 1f) * 0.5f * sub;
            float fy = (v + 1f) * 0.5f * sub;

            int ix = Mathf.Clamp(Mathf.FloorToInt(fx), 0, sub - 1);
            int iy = Mathf.Clamp(Mathf.FloorToInt(fy), 0, sub - 1);
            float tx = Mathf.Clamp01(fx - ix);
            float ty = Mathf.Clamp01(fy - iy);

            var heights = data.FaceHeights[face];
            if (heights == null) return 0f;

            int stride = sub + 1;
            float h00 = heights[iy * stride + ix];
            float h10 = heights[iy * stride + ix + 1];
            float h01 = heights[(iy + 1) * stride + ix];
            float h11 = heights[(iy + 1) * stride + ix + 1];

            return Mathf.Lerp(Mathf.Lerp(h00, h10, tx), Mathf.Lerp(h01, h11, tx), ty);
        }

        /// <summary>把世界坐标转成星球局部方向（兼容星球被旋转/移动）。</summary>
        public static Vector3 WorldToLocalDirection(Transform planet, Vector3 worldPos)
        {
            if (planet == null) return Vector3.up;
            return planet.InverseTransformDirection(worldPos - planet.position).normalized;
        }
    }
}
