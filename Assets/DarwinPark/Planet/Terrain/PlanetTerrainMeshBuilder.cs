using UnityEngine;
using DarwinPark.Core;

namespace DarwinPark.Planet
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class PlanetTerrainMeshBuilder : MonoBehaviour
    {
        [Header("基础配置")]
        public int subdivisions = 256;
        public float radius = 300f;
        public int seed = 42;

        [Header("地形高度配置")]
        [Tooltip("归一化高度 0~1 对应的世界高度。★ 从 45 提到 90：45 时全星球最大起伏只有约 12 米，山脉在 300 半径的星球上几乎看不见")]
        public float heightScale = 90f;
        [Tooltip("历史参数：实际海平面由 continentRatio 的分位值决定；保留该字段仅为兼容旧场景")]
        [Range(0.3f, 0.7f)]
        public float seaLevelRatio = 0.45f;

        [Header("噪声配置 (FBM)")]
        public float baseFrequency = 0.8f; // 基础地形频率（越小大陆越连贯）
        public float baseAmplitude = 1.0f;
        public int baseOctaves = 4;

        public float detailFrequency = 3.0f; // 细节频率（增加起伏）
        public float detailAmplitude = 0.15f;
        public int detailOctaves = 3;

        [Header("地貌雕刻（数据驱动）")]
        [Tooltip("海岸相对海平面的抬升")]
        public float coastalRise = 0.012f;
        [Tooltip("大陆内部整体抬升，越大高原越明显")]
        public float inlandRise = 0.22f;
        [Tooltip("内陆抬升曲线，>1 时沿海更平坦")]
        public float inlandCurve = 1.35f;

        [Header("山脉")]
        [Tooltip("山脊相对大陆基底的最大抬升（归一化高度）")]
        public float mountainHeight = 0.72f;
        // ★ 适配：独立造山模型的可调参数。
        public float mountainCoreWidth = 0.015f;
        public float mountainBeltWidth = 0.16f;
        [Range(0f, 1f)] public float mountainDetailStrength = 0.65f;
        [Tooltip("山脉频率。方向向量是单位球，频率低于 2 时整个星球只有一两个鼓包，看不出山脉")]
        public float mountainFrequency = 1.65f;
        public int mountainOctaves = 2;
        [Range(0.5f, 6f)] public float mountainSharpness = 2.4f;
        [Range(0f, 1f)] public float mountainStart = 0.03f;
        [Range(0.01f, 1f)] public float mountainFull = 0.32f;
        public float mountainWarp = 0.45f;
        [Range(0f, 1f)] public float mountainBeltStrength = 0.75f;

        [Header("盆地与峡谷")]
        public float basinDepth = 0.10f;
        public float basinFrequency = 1.35f;
        [Range(0f, 1f)] public float basinThreshold = 0.28f;
        [Range(0.01f, 0.5f)] public float basinWidth = 0.30f;
        public float valleyDepth = 0.085f;
        public float valleyFrequency = 3.8f;
        [Range(0.5f, 6f)] public float valleySharpness = 3.2f;
        public float detailStrength = 0.02f;
        [Range(0.05f, 0.9f)] public float oceanFloorRatio = 0.22f;

        [Header("侵蚀（数据驱动）")]
        public ErosionConfig erosion = new ErosionConfig();

        [Header("渲染")]
        public Material TerrainMaterial;

        public PlanetTerrainData Data { get; private set; }
        private Mesh _mesh;

        private void Start() => Build();

        [Range(0.2f, 0.8f)]
        [Tooltip("陆地占比：0.2=海洋多，0.8=陆地多。海平面取大陆噪声的对应分位值")]
        public float continentRatio = 0.5f;

        /// <summary>
        /// ★ 适配：实体溺水/水下判断统一使用真实海平面，而不是 seaLevelRatio。
        /// 旧代码用 heightScale * seaLevelRatio 计算水位，会把整个大陆的低地误判成水下。
        /// </summary>
        public float WaterRadius
        {
            get
            {
                if (Data == null) return radius;
                return radius + Data.SeaLevel * heightScale;
            }
        }

        /// <summary>★ 适配：判断世界坐标是否位于海平面以下。</summary>
        public bool IsSubmerged(Vector3 worldPosition, float depthMargin = 0f)
        {
            return (worldPosition - transform.position).magnitude < WaterRadius - Mathf.Max(0f, depthMargin);
        }

        public void Build()
        {
            // 1. 构建配置对象（所有地貌参数从 Inspector/配置传入，避免写死在生成器）
            var cfg = new PlanetTerrainConfig
            {
                id = "dynamic",
                subdivisions = this.subdivisions,
                radius = this.radius,
                heightScale = this.heightScale,
                seed = this.seed,

                coastalRise = this.coastalRise,
                inlandRise = this.inlandRise,
                inlandCurve = this.inlandCurve,

                mountainHeight = this.mountainHeight,
                mountainCoreWidth = this.mountainCoreWidth,
                mountainBeltWidth = this.mountainBeltWidth,
                mountainDetailStrength = this.mountainDetailStrength,
                mountainFrequency = this.mountainFrequency,
                mountainOctaves = this.mountainOctaves,
                mountainSharpness = this.mountainSharpness,
                mountainStart = this.mountainStart,
                mountainFull = this.mountainFull,
                mountainWarp = this.mountainWarp,
                mountainBeltStrength = this.mountainBeltStrength,

                basinDepth = this.basinDepth,
                basinFrequency = this.basinFrequency,
                basinThreshold = this.basinThreshold,
                basinWidth = this.basinWidth,
                valleyDepth = this.valleyDepth,
                valleyFrequency = this.valleyFrequency,
                valleySharpness = this.valleySharpness,
                detailStrength = this.detailStrength,
                oceanFloorRatio = this.oceanFloorRatio,
                erosion = this.erosion ?? new ErosionConfig()
            };

            // 填入噪声参数
            cfg.baseNoise.frequency = baseFrequency;
            cfg.baseNoise.amplitude = baseAmplitude;
            cfg.baseNoise.octaves = baseOctaves;

            cfg.detailNoise.frequency = detailFrequency;
            cfg.detailNoise.amplitude = detailAmplitude;
            cfg.detailNoise.octaves = detailOctaves;

            // 2. 调用生成器，获取数据
            Data = PlanetTerrainGenerator.Generate(cfg, seaLevelRatio, continentRatio);
            if (Data == null)
            {
                Log.Error(Log.Tag.Planet, "地形生成失败：生成器返回空数据");
                return;
            }

            // 3. 构建 Mesh 网格
            if (_mesh == null)
            {
                _mesh = new Mesh { name = "PlanetTerrain" };
                _mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            }
            _mesh.Clear();
            _mesh.vertices = Data.Vertices;
            _mesh.triangles = Data.Triangles;
            _mesh.RecalculateNormals();
            // ★ 修复：Cube-Sphere 的 6 个面使用重复顶点，RecalculateNormals 会在接缝
            // 产生两套不同法线，气候/高程图层叠加阴影后会出现直线断层。这里按位置合并法线。
            SmoothSeamNormals(_mesh);
            _mesh.RecalculateBounds();

            GetComponent<MeshFilter>().mesh = _mesh;

            // 4. 配置渲染器并传递 Shader 参数
            var mr = GetComponent<MeshRenderer>();
            if (TerrainMaterial != null) mr.sharedMaterial = TerrainMaterial;

            // ★ 核心修复：把当前配置传给 Shader，防止 Shader 里写死
            var block = new MaterialPropertyBlock();
            mr.GetPropertyBlock(block);
            block.SetFloat("_PlanetRadius", radius);
            block.SetFloat("_HeightScale", heightScale);
            // ★ 核心修复：用动态算出来的海平面阈值，而不是输入的参数！
            block.SetFloat("_SeaLevel", Data.SeaLevel);
            // ★ 适配：Shader 的雪线/陆地归一化不再写死 0.9，统一从地形数据读取上限。
            block.SetFloat("_MaxHeight", PlanetTerrainData.MaxNormalizedHeight);
            mr.SetPropertyBlock(block);

            // 5. 配置碰撞体 (只获取一次，绝不重复)
            var mc = GetComponent<MeshCollider>();
            if (mc == null) mc = gameObject.AddComponent<MeshCollider>();
            mc.sharedMesh = _mesh;

            // ★ 适配：独立海面/云层复用本次生成的焊接网格，重建地形时同步刷新。
            GetComponent<PlanetWeatherVisuals>()?.Rebuild();

            // 6. 完成日志（同时输出高度范围，便于确认山脉/盆地是否真的生成）
            float minHeight = float.MaxValue;
            float maxHeight = float.MinValue;
            int mountainCells = 0;
            int totalCells = 0;
            for (int face = 0; face < 6; face++)
            {
                var hs = Data.FaceHeights[face];
                if (hs == null) continue;
                for (int i = 0; i < hs.Length; i++)
                {
                    float h = hs[i];
                    if (h < minHeight) minHeight = h;
                    if (h > maxHeight) maxHeight = h;
                    if (h > Data.SeaLevel + 0.10f) mountainCells++;
                    totalCells++;
                }
            }
            float peakAboveSea = (maxHeight - Data.SeaLevel) * heightScale;
            float oceanDepth = (Data.SeaLevel - minHeight) * heightScale;
            Log.Info(Log.Tag.Planet,
                $"地形生成完成：vert={Data.Vertices.Length}, seaLevel={Data.SeaLevel:F3}, waterRadius={WaterRadius:F1}, " +
                $"height=[{minHeight:F3},{maxHeight:F3}], peakAboveSea={peakAboveSea:F1}m, oceanDepth={oceanDepth:F1}m, " +
                $"highLandVertices={mountainCells}({100f * mountainCells / Mathf.Max(1, totalCells):F1}%)");
        }

        /// <summary>把同一位置（主要是 6 面接缝）的重复顶点法线取平均并归一化。</summary>
        private static void SmoothSeamNormals(Mesh mesh)
        {
            var vertices = mesh.vertices;
            var normals = mesh.normals;
            if (vertices == null || normals == null || vertices.Length != normals.Length) return;

            var sums = new System.Collections.Generic.Dictionary<Vector3Int, Vector3>(vertices.Length);
            var counts = new System.Collections.Generic.Dictionary<Vector3Int, int>(vertices.Length);
            var keys = new Vector3Int[vertices.Length];
            const float posScale = 10000f;

            for (int i = 0; i < vertices.Length; i++)
            {
                var p = vertices[i];
                var key = new Vector3Int(
                    Mathf.RoundToInt(p.x * posScale),
                    Mathf.RoundToInt(p.y * posScale),
                    Mathf.RoundToInt(p.z * posScale));
                keys[i] = key;

                sums.TryGetValue(key, out var sum);
                counts.TryGetValue(key, out var count);
                sums[key] = sum + normals[i];
                counts[key] = count + 1;
            }

            for (int i = 0; i < vertices.Length; i++)
            {
                var key = keys[i];
                if (counts[key] <= 1) continue;
                var normal = sums[key];
                if (normal.sqrMagnitude > 1e-8f) normals[i] = normal.normalized;
            }

            mesh.normals = normals;
        }
    }
}
