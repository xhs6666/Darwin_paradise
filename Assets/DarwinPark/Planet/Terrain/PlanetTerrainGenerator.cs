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

            int sub = Mathf.Max(2, cfg.subdivisions);
            int vertsPerFace = (sub + 1) * (sub + 1);
            var data = new PlanetTerrainData
            {
                Subdivisions = sub,
                Radius = cfg.radius,
                HeightScale = cfg.heightScale,
                FaceHeights = new float[6][]
            };

            // 每种地貌使用独立种子，避免山脉、盆地、峡谷全部叠在同一位置。
            var continentNoise = new FBMNoise(cfg.baseNoise, cfg.seed);
            var detailNoise = new FBMNoise(cfg.detailNoise, cfg.seed + 1337);

            // ★ 适配：由独立造山带模型提供连续山系。
            var mountains = new PlanetMountainField(cfg);

            var basinCfg = new FBMConfig
            {
                octaves = 3,
                frequency = cfg.basinFrequency,
                amplitude = 1f,
                lacunarity = 2f,
                gain = 0.5f,
                warpStrength = 0.15f
            };
            var basinNoise = new FBMNoise(basinCfg, cfg.seed + 1777);

            var valleyCfg = new FBMConfig
            {
                octaves = 4,
                frequency = cfg.valleyFrequency,
                amplitude = 1f,
                lacunarity = 2f,
                gain = 0.5f,
                warpStrength = 0.25f
            };
            var valleyNoise = new FBMNoise(valleyCfg, cfg.seed + 2663);

            // 1. 先采样“大陆/海洋”基底，并按目标陆地比例求海平面分位值。
            var rawHeights = new float[6][];
            var allHeights = new List<float>(6 * vertsPerFace);

            for (int face = 0; face < 6; face++)
            {
                var heights = new float[vertsPerFace];
                for (int y = 0; y <= sub; y++)
                {
                    for (int x = 0; x <= sub; x++)
                    {
                        float u = (float)x / sub * 2f - 1f;
                        float v = (float)y / sub * 2f - 1f;
                        Vector3 dir = FacePoint(FaceNormals[face], u, v).normalized;
                        float c = Noise01(continentNoise, dir);

                        heights[y * (sub + 1) + x] = c;
                        allHeights.Add(c);
                    }
                }
                rawHeights[face] = heights;
            }

            allHeights.Sort();
            float landFraction = Mathf.Clamp01(continentRatio);
            int seaIndex = Mathf.FloorToInt((1.0f - landFraction) * allHeights.Count);
            seaIndex = Mathf.Clamp(seaIndex, 0, allHeights.Count - 1);
            float seaLevelThreshold = allHeights[seaIndex];
            // ★ 修复（防Bug回归）：FBM 实际最大值远低于理论上限 1，旧归一化会压低整个大陆的山脉遮罩。
            float landRange = Mathf.Max(0.0001f, allHeights[allHeights.Count - 1] - seaLevelThreshold);
            data.SeaLevel = seaLevelThreshold;

            // 2. 分层雕刻：大陆架 → 山脉带 → 盆地 → 峡谷 → 细节。
            for (int face = 0; face < 6; face++)
            {
                var heights = new float[vertsPerFace];
                for (int y = 0; y <= sub; y++)
                {
                    for (int x = 0; x <= sub; x++)
                    {
                        int i = y * (sub + 1) + x;
                        float c = rawHeights[face][i];
                        float u = (float)x / sub * 2f - 1f;
                        float v = (float)y / sub * 2f - 1f;
                        Vector3 dir = FacePoint(FaceNormals[face], u, v).normalized;

                        float height;
                        if (c < seaLevelThreshold)
                        {
                            // 海洋：近岸平缓、深海下凹，避免整片海床高度相同。
                            float oceanT = seaLevelThreshold > 1e-5f
                                ? Mathf.Clamp01(c / seaLevelThreshold)
                                : 0f;
                            float floor = Mathf.Lerp(
                                seaLevelThreshold * cfg.oceanFloorRatio,
                                seaLevelThreshold,
                                Mathf.Pow(oceanT, 1.8f));
                            floor += detailNoise.Sample(dir) * cfg.detailStrength * 0.4f * (1f - oceanT);
                            height = floor;
                        }
                        else
                        {
                            float land = Mathf.Clamp01((c - seaLevelThreshold) / landRange);

                            // 大陆基底：海岸低平，向内陆逐渐抬升。
                            float baseHeight = seaLevelThreshold + cfg.coastalRise
                                + cfg.inlandRise * Mathf.Pow(land, Mathf.Max(0.1f, cfg.inlandCurve));

                            // 大尺度平原微起伏。
                            baseHeight += detailNoise.Sample(dir) * cfg.detailStrength
                                * (0.4f + land * 0.8f);

                            // ★ 适配：山系只占连续造山带，低地不再被大量等值线山脊淹没。
                            float mountainMask = Mathf.SmoothStep(0f, 1f,
                                Mathf.InverseLerp(cfg.mountainStart, cfg.mountainFull, land));
                            float headroom = Mathf.Max(0f, PlanetTerrainData.MaxNormalizedHeight - baseHeight);
                            baseHeight += mountains.Sample(dir) * cfg.mountainHeight * mountainMask * headroom;

                            // 盆地：宽而浅的碗状洼地，避免旧版只有一条窄带。
                            float basinNoiseValue = Noise01(basinNoise, dir);
                            float basinMask = Mathf.SmoothStep(0f, 1f,
                                Mathf.InverseLerp(cfg.basinThreshold, cfg.basinThreshold + cfg.basinWidth, basinNoiseValue));
                            baseHeight -= basinMask * cfg.basinDepth * (0.35f + land * 0.65f);

                            // 峡谷：脊状噪声的高值线作为河道下切，形成可见的谷地网络。
                            float valleyRaw = valleyNoise.Sample(dir);
                            float valley = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(valleyRaw)), Mathf.Max(0.5f, cfg.valleySharpness));
                            float valleyMask = Mathf.SmoothStep(0.04f, 0.55f, land);
                            baseHeight -= valley * cfg.valleyDepth * valleyMask;

                            // 陆地最低保持在海平面以上一点点，避免内陆盆地变成整片海洋。
                            // ★ 修复（防Bug回归）：盆地可形成湖泊，禁止将所有低地裁成同高度沙色平台。
                            height = baseHeight;
                        }

                        heights[i] = Mathf.Clamp(height, 0f, PlanetTerrainData.MaxNormalizedHeight);
                    }
                }
                data.FaceHeights[face] = heights;
            }

            // 3. 对每个面执行水力/热力侵蚀，再合并接缝顶点。
            ApplyErosion(cfg, data.FaceHeights, sub);
            MergeSeamVertices(data.FaceHeights, sub);

            BuildMesh(data);
            // ★ 适配：焊接接缝共享索引，消除裂缝与两套法线。
            PlanetMeshTopology.Weld(data);
            return data;
        }

        private static float Noise01(FBMNoise noise, Vector3 dir)
        {
            return Mathf.Clamp01((noise.Sample(dir) + 1f) * 0.5f);
        }

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
                Vector3 calcNormal = Vector3.Cross(p1 - p0, p2 - p0).normalized;
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

        private static Vector3 FacePoint(Vector3 n, float u, float v)
        {
            if (Mathf.Abs(n.x) > 0.5f) return new Vector3(n.x, v, u);
            if (Mathf.Abs(n.y) > 0.5f) return new Vector3(u, n.y, v);
            return new Vector3(u, v, n.z);
        }
    }
}
