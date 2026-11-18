using System.Collections.Generic;
using UnityEngine;

namespace DarwinPark.Planet
{
    // ✅ 新增：按种子生成基础大陆与地貌高度；侵蚀、网格、导航继续由生成入口编排。
    internal sealed class PlanetHeightField
    {
        private readonly PlanetTerrainConfig cfg;
        private readonly FBMNoise continentNoise, detailNoise, basinNoise, valleyNoise;
        private readonly PlanetMountainField mountains;
        private readonly PlanetLandformField landforms;
        public PlanetHeightField(PlanetTerrainConfig config)
        {
            cfg = config;
            // 每种地貌使用独立种子，避免山脉、盆地、峡谷全部叠在同一位置。
            continentNoise = new FBMNoise(cfg.baseNoise, cfg.seed);
            detailNoise = new FBMNoise(cfg.detailNoise, cfg.seed + 1337);

            // ★ 适配：由独立造山带模型提供连续山系。
            mountains = new PlanetMountainField(cfg);
            landforms=new PlanetLandformField(cfg.landforms??new LandformSettings{enabled=false},cfg.seed);

            var basinCfg = new FBMConfig
            {
                octaves = 3,
                frequency = cfg.basinFrequency,
                amplitude = 1f,
                lacunarity = 2f,
                gain = 0.5f,
                warpStrength = 0.15f
            };
            basinNoise = new FBMNoise(basinCfg, cfg.seed + 1777);

            var valleyCfg = new FBMConfig
            {
                octaves = 4,
                frequency = cfg.valleyFrequency,
                amplitude = 1f,
                lacunarity = 2f,
                gain = 0.5f,
                warpStrength = 0.25f
            };
            valleyNoise = new FBMNoise(valleyCfg, cfg.seed + 2663);

        }

        public PlanetTerrainData Build(float continentRatio)
        {
            int sub = Mathf.Max(2, cfg.subdivisions);
            int vertsPerFace = (sub + 1) * (sub + 1);
            var data = new PlanetTerrainData
            {
                Subdivisions = sub,
                Radius = cfg.radius,
                HeightScale = cfg.heightScale,
                FaceHeights = new float[6][]
            };

            var rawHeights = SampleContinents(sub, vertsPerFace, out var allHeights);
            allHeights.Sort();
            float landFraction = Mathf.Clamp01(continentRatio);
            int seaIndex = Mathf.FloorToInt((1.0f - landFraction) * allHeights.Count);
            seaIndex = Mathf.Clamp(seaIndex, 0, allHeights.Count - 1);
            float seaLevelThreshold = allHeights[seaIndex];
            // ★ 修复（防Bug回归）：FBM 实际最大值远低于理论上限 1，旧归一化会压低整个大陆的山脉遮罩。
            float landRange = Mathf.Max(0.0001f, allHeights[allHeights.Count - 1] - seaLevelThreshold);
            data.SeaLevel = seaLevelThreshold;

            ShapeFaces(data, rawHeights, seaLevelThreshold, landRange);
            return data;
        }

        private float[][] SampleContinents(int sub, int vertsPerFace, out List<float> allHeights)
        {
            var rawHeights = new float[6][];
            allHeights = new List<float>(6 * vertsPerFace);

            for (int face = 0; face < 6; face++)
            {
                var heights = new float[vertsPerFace];
                for (int y = 0; y <= sub; y++)
                {
                    for (int x = 0; x <= sub; x++)
                    {
                        float u = (float)x / sub * 2f - 1f;
                        float v = (float)y / sub * 2f - 1f;
                        Vector3 dir = PlanetTerrainGenerator.FacePoint(PlanetTerrainGenerator.FaceNormal(face), u, v).normalized;
                        float c = Noise01(continentNoise, dir);

                        heights[y * (sub + 1) + x] = c;
                        allHeights.Add(c);
                    }
                }
                rawHeights[face] = heights;
            }

            return rawHeights;
        }

        private void ShapeFaces(PlanetTerrainData data, float[][] rawHeights, float seaLevelThreshold, float landRange)
        {
            int sub = data.Subdivisions;
            int vertsPerFace = (sub + 1) * (sub + 1);
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
                        Vector3 dir = PlanetTerrainGenerator.FacePoint(PlanetTerrainGenerator.FaceNormal(face), u, v).normalized;

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
                            if (cfg.landforms is {enabled:true,version:>=5} water)
                            {
                                float offshore = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(1f, water.oceanShelfStart, oceanT));
                                float depth = Mathf.Max(0, seaLevelThreshold - floor);
                                floor = seaLevelThreshold - depth * Mathf.Lerp(1, water.oceanDepthMultiplier, offshore);
                            }
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

                            // ★ 适配：独立高原/盆地场在造山前塑形。
                            baseHeight=landforms.Shape(baseHeight,dir,seaLevelThreshold);
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

        }

        private static float Noise01(FBMNoise noise, Vector3 dir)
        {
            return Mathf.Clamp01((noise.Sample(dir) + 1f) * 0.5f);
        }
    }
}
