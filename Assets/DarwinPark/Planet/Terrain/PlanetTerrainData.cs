using UnityEngine;

namespace DarwinPark.Planet
{
    /// <summary>地形数据（cube-sphere 6 面）。</summary>
    public class PlanetTerrainData
    {
        /// <summary>
        /// ★ 新增：归一化高度的上限。海平面、气候海拔、Shader 都引用该值，
        /// 避免各处分别写死 0.9 导致山脉被截断或雪线错位。
        /// </summary>
        public const float MaxNormalizedHeight = 1f;

        public int Subdivisions;
        public float Radius;
        public float HeightScale;

        // 6 个面，每个面包含 (subdivisions + 1)^2 个高度数据
        public float[][] FaceHeights;

        public Vector3[] Vertices;
        public int[] Triangles;
        public Color[] Colors;
        public Bounds Bounds;
        public float SeaLevel;
    }
}