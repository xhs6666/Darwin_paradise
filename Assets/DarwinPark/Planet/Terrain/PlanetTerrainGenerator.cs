using System.Collections.Generic;
using UnityEngine;
using DarwinPark.Planet.Erosion;

namespace DarwinPark.Planet
{
    /// <summary>
    /// Cube-Sphere 行星地形生成器。
    /// ★ 修复（防Bug回归）：旧实现的山脉使用“1-|2m-1|”折叠噪声，
    /// 负值会被平方放大，且盆地只有很窄的一条，实际几乎看不到山脉/盆地。
    /// 现在按“大陆基底 → 山脉带 → 盆地 → 峡谷 → 侵蚀”分层雕刻，所有幅度来自配置。
    /// </summary>
    public static class PlanetTerrainGenerator
    {
        private static readonly Vector3[] FaceNormals = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };

        // 用于把 6 个面接缝处的重复顶点合并，避免侵蚀后出现裂缝。
        private const float SeamQuantizeScale = 100000f;

        public static PlanetTerrainData Generate(PlanetTerrainConfig cfg, float seaLevelRatio, float continentRatio)
        {
            if (cfg == null) return null;

            // ✅ 适配：这里只编排生成阶段，不再混入每个地貌层的采样公式。
            var data = new PlanetHeightField(cfg).Build(continentRatio);
            int sub = data.Subdivisions;

            // 3. 对每个面执行水力/热力侵蚀，再合并接缝顶点。
            ApplyErosion(cfg, data.FaceHeights, sub);
            MergeSeamVertices(data.FaceHeights, sub);

            // ★ 适配：跨面采样邻域平滑坡度，再合并边缘消除浮点误差；世界场与碰撞共用修正后的数据。
            PlanetSeamContinuity.Smooth(data,cfg.erosion);
            MergeSeamVertices(data.FaceHeights,sub);

            // ★ 适配：优先洪泛得到真实下游连通，再下切河床；不在运行帧计算。
            if(cfg.landforms?.enabled==true)
            {
                // ★ 新增：盆地先雕刻后汇流，湖面由真实溢出口决定；旧版本不进入新版生成路径。
                if(cfg.landforms.version>=3)PlanetDrainageNetwork.CarveBasins(data,cfg.landforms,cfg.seed);
                data.Hydrology=PlanetHydrology.Build(data,cfg.landforms);data.Hydrology.Carve(data);MergeSeamVertices(data.FaceHeights,sub);
            }
            BuildMesh(data);
            // ★ 适配：焊接接缝共享索引，消除裂缝与两套法线。
            PlanetMeshTopology.Weld(data);
            PaintLakeBeds(data);
            // ★ 修复（防Bug回归）：避海图在最终网格完成后建立，不能使用与碰撞岸线不同的近似地形。
            data.Navigation=new LandNavigationGraph(data,cfg.navigation??new LandNavigationSettings());
            return data;
        }

        private static void PaintLakeBeds(PlanetTerrainData data)
        {
            if(data.Hydrology==null||data.Hydrology.LakeCells==0)return;
            var beds=new Color32[data.Vertices.Length];
            for(int i=0;i<beds.Length;i++)
            {
                var position=data.Vertices[i];
                float radius=position.magnitude;
                float level=data.Hydrology.LakeWater(position/radius);
                float depth=level>data.SeaLevel?data.Radius+level*data.HeightScale-radius:0;
                beds[i]=new Color32((byte)Mathf.RoundToInt(Mathf.Clamp01(depth/3f)*255),0,0,255);
            }
            data.LakeBedColors=beds;
        }

        internal static Vector3 FaceNormal(int face) => FaceNormals[face];

        private static void ApplyErosion(PlanetTerrainConfig cfg, float[][] faceHeights, int sub)
        {
            if (cfg.erosion == null) return;

            for (int face = 0; face < 6; face++)
            {
                var grid = new float[sub + 1, sub + 1];
                for (int y = 0; y <= sub; y++)
                {
                    for (int x = 0; x <= sub; x++)
                    {
                        grid[x, y] = faceHeights[face][y * (sub + 1) + x];
                    }
                }

                // 每个面使用独立随机序列，保证同一种子生成结果可复现。
                var rng = new DeterministicRandom(cfg.seed + face * 7919 + 17);
                if (cfg.erosion.enableHydraulic && cfg.erosion.hydraulicIterations > 0)
                {
                    HydraulicErosion.Apply(grid, sub, cfg.erosion.hydraulicIterations,
                        cfg.erosion.hydraulicStrength, rng);
                }
                if (cfg.erosion.enableThermal && cfg.erosion.thermalIterations > 0)
                {
                    ThermalErosion.Apply(grid, sub, cfg.erosion.thermalIterations,
                        cfg.erosion.thermalTalusAngle, cfg.erosion.thermalStrength);
                }

                for (int y = 0; y <= sub; y++)
                {
                    for (int x = 0; x <= sub; x++)
                    {
                        float eroded = grid[x, y];
                        // ★ 修复：侵蚀算法一旦产生 NaN/Inf，会污染整张高度图并让山脉消失。
                        if (float.IsNaN(eroded) || float.IsInfinity(eroded))
                            eroded = 0f;
                        // ★ 修复（防Bug回归）：独立侵蚀在六面边界形成不同坡度，仅平均边缘一行仍会出现直线山崖。
                        // 边界缓冲带平滑退回原始球面场，保证两边的高度与一阶变化连续。
                        float edge = Mathf.Min(Mathf.Min(x, y), Mathf.Min(sub - x, sub - y));
                        float weight = Mathf.SmoothStep(0f, 1f, edge / Mathf.Max(1f, cfg.erosion.seamBlendCells));
                        int index = y * (sub + 1) + x;
                        faceHeights[face][index] = Mathf.Clamp(
                            Mathf.Lerp(faceHeights[face][index], eroded, weight), 0f, PlanetTerrainData.MaxNormalizedHeight);
                    }
                }
            }
        }

        /// <summary>
        /// 侵蚀会分别修改 6 个面；接缝处是重复顶点，如果不取平均会出现裂缝。
        /// </summary>
        private static void MergeSeamVertices(float[][] faceHeights, int sub)
        {
            int perFace = (sub + 1) * (sub + 1);
            var keys = new Vector3Int[6][];
            var sums = new Dictionary<Vector3Int, float>(6 * perFace);
            var counts = new Dictionary<Vector3Int, int>(6 * perFace);

            for (int face = 0; face < 6; face++)
            {
                keys[face] = new Vector3Int[perFace];
                for (int y = 0; y <= sub; y++)
                {
                    for (int x = 0; x <= sub; x++)
                    {
                        int i = y * (sub + 1) + x;
                        float u = (float)x / sub * 2f - 1f;
                        float v = (float)y / sub * 2f - 1f;
                        Vector3 dir = FacePoint(FaceNormals[face], u, v).normalized;
                        var key = new Vector3Int(
                            Mathf.RoundToInt(dir.x * SeamQuantizeScale),
                            Mathf.RoundToInt(dir.y * SeamQuantizeScale),
                            Mathf.RoundToInt(dir.z * SeamQuantizeScale));
                        keys[face][i] = key;

                        sums.TryGetValue(key, out float sum);
                        counts.TryGetValue(key, out int count);
                        sums[key] = sum + faceHeights[face][i];
                        counts[key] = count + 1;
                    }
                }
            }

            for (int face = 0; face < 6; face++)
            {
                for (int i = 0; i < perFace; i++)
                {
                    var key = keys[face][i];
                    faceHeights[face][i] = sums[key] / Mathf.Max(1, counts[key]);
                }
            }
        }

        private static void BuildMesh(PlanetTerrainData data)
        {
            int sub = data.Subdivisions;
            int perFace = (sub + 1) * (sub + 1);
            data.Vertices = new Vector3[perFace * 6];
            var tris = new List<int>(sub * sub * 36);

            for (int face = 0; face < 6; face++)
            {
                int baseIdx = face * perFace;
                for (int y = 0; y <= sub; y++)
                {
                    for (int x = 0; x <= sub; x++)
                    {
                        int i = baseIdx + y * (sub + 1) + x;
                        float u = (float)x / sub * 2f - 1f;
                        float v = (float)y / sub * 2f - 1f;
                        Vector3 dir = FacePoint(FaceNormals[face], u, v).normalized;
                        float h = data.FaceHeights[face][y * (sub + 1) + x];
                        // 乘上 HeightScale 之前，h 最大是 PlanetTerrainData.MaxNormalizedHeight
                        float r = data.Radius + h * data.HeightScale;
                        data.Vertices[i] = dir * r;
                    }
                }

                int first_i00 = baseIdx;
                int first_i10 = first_i00 + 1;
                int first_i11 = first_i00 + (sub + 1) + 1;
                // ★ 修复（防Bug回归）：用未变形球面判绕序；陡坡角点的法线不能代表整个立方体面。
                Vector3 p0 = data.Vertices[first_i00].normalized;
                Vector3 p1 = data.Vertices[first_i10].normalized;
                Vector3 p2 = data.Vertices[first_i11].normalized;
                // ★ 修复（防Bug回归）：高细分三角形叉积小于 Vector3.Normalize 的阈值，会归零并令三个面绕序反向。
                // 判正负无需单位化，保留原始叉积让碰撞射线和近景遮挡在六面都有效。
                Vector3 calcNormal = Vector3.Cross(p1 - p0, p2 - p0);
                bool flip = Vector3.Dot(calcNormal, FaceNormals[face]) < 0;

                for (int y = 0; y < sub; y++)
                {
                    for (int x = 0; x < sub; x++)
                    {
                        int i00 = baseIdx + y * (sub + 1) + x;
                        int i10 = i00 + 1;
                        int i01 = baseIdx + (y + 1) * (sub + 1) + x;
                        int i11 = i01 + 1;
                        if (!flip) { tris.Add(i00); tris.Add(i10); tris.Add(i11); tris.Add(i00); tris.Add(i11); tris.Add(i01); }
                        else { tris.Add(i00); tris.Add(i11); tris.Add(i10); tris.Add(i00); tris.Add(i01); tris.Add(i11); }
                    }
                }
            }
            data.Triangles = tris.ToArray();
        }

        internal static Vector3 FacePoint(Vector3 n, float u, float v)
        {
            if (Mathf.Abs(n.x) > 0.5f) return new Vector3(n.x, v, u);
            if (Mathf.Abs(n.y) > 0.5f) return new Vector3(u, n.y, v);
            return new Vector3(u, v, n.z);
        }
    }
}
