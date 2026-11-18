using System;
using UnityEngine;

namespace DarwinPark.Planet
{
    /// <summary>气候图的一次生成结果（纯数据，方便在编辑器/测试中复用与验证）。</summary>


    /// <summary>
    /// ★ 新增：星球气候模型（纯 C#，不依赖场景对象）。
    /// 为什么独立：气候生成涉及“太阳辐射 + 海拔 + 海洋热惯性 + 洋流 + 风程 +
    /// 海岸距离 + 迎风坡 + 雨影 + 地形起伏 + 噪声”多个条件，放在 MonoBehaviour
    /// 里既难调参也难验证。这里只负责算数据，PlanetClimateMap 只负责上传纹理。
    ///
    /// 所有输入都是连续的场，颜色在 CPU 端连续混色；没有任何“if 湿度小于 X 就
    /// 变成沙漠”的硬分类，因此图层不会出现色块断层。
    /// </summary>
    public sealed class PlanetClimateModel
    {
        private readonly PlanetTerrainData _data;
        private readonly PlanetClimateConfig _cfg;
        private readonly int _seed;

        public PlanetClimateModel(PlanetTerrainData data, PlanetClimateConfig cfg, int seed)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
            _cfg = cfg ?? new PlanetClimateConfig();
            _seed = seed;
        }

        /// <summary>
        /// 生成气候图。tempOffset/humidityOffset 来自环境系统滑条与季节偏移，
        /// 每次重生成都叠加，保证“看到的图层”和“实体感受到的气候”一致。
        /// </summary>
        public PlanetClimateResult Generate(int resolution, float tempOffset, float humidityOffset)
        {
            int res = Mathf.Clamp(resolution, 64, 2048);
            int count = res * res;
            var result = new PlanetClimateResult
            {
                Resolution = res,
                SeaLevel = _data.SeaLevel,
                Height = new float[count],
                Temperature = new float[count],
                Humidity = new float[count],
                Precipitation = new float[count],
                CoastProximity = new float[count],
                WindFetch = new float[count],
                Continentality = new float[count],
                Relief = new float[count],
                Ocean = new bool[count],
                Pixels = new Color[count],
                Types = new ClimateType[count]
            };

            // ---------- 1. 采样高度 / 海洋，并计算海岸距离 ----------
            for (int y = 0; y < res; y++)
            {
                float v = (y + 0.5f) / res;
                for (int x = 0; x < res; x++)
                {
                    float u = (x + 0.5f) / res;
                    Vector3 dir = PlanetTerrainSampler.DirectionFromUV(u, v);
                    int i = y * res + x;
                    float h = PlanetTerrainSampler.SampleHeight(_data, dir);
                    result.Height[i] = h;
                    result.Ocean[i] = h < _data.SeaLevel;
                }
            }

            int[] coastDistance = BuildCoastDistance(result.Ocean, res);
            float coastScale = Mathf.Max(1f, res * Mathf.Max(0.01f, _cfg.coastFalloffFraction));

            // ---------- 2. 盛行风“上风方海洋距离”（风程）----------
            // 在 1/4 分辨率上做球面步进，再双线性插值到全分辨率：
            // 风程是连续场，不会像“最近海洋方向”那样在等距边界产生扇形断层。
            int coarseResolution = Mathf.Clamp(res / 4, 48, 256);
            float[] coarseFetch = BuildCoarseWindFetch(coarseResolution);
            if (_cfg.earth?.enabled == true)
                result.Circulation = new ClimateCirculation(_data,_cfg,Mathf.Clamp(coarseResolution,48,128));

            // ---------- 3. 温度/湿度噪声 ----------
            float altitudeRange = Mathf.Max(0.01f, PlanetTerrainData.MaxNormalizedHeight - _data.SeaLevel);
            new ClimateAtmosphere(_data,_cfg,_seed).Build(result,coastDistance,coarseFetch,coarseResolution,coastScale,altitudeRange,tempOffset,humidityOffset);

            SmoothLandField(result, res, result.Temperature, 3);
            SmoothLandField(result, res, result.Humidity, 3);

            BuildColorsAndStats(result, res, altitudeRange);

            // ★ 球面首尾经线一致 + 两极收敛为同一颜色，消除纹理接缝/极点 pinch 断层。
            // ★ 修复（防Bug回归）：像元中心不在经线/极点上，强行复制边界行列会制造一圈不连续。
            // 球面方向采样本身连续，纹理 U Repeat / V Clamp 负责边界插值。

            return result;
        }

        /// <summary>只对陆地像元做 3x3 平均，避免把海洋 100% 湿度扩散到海岸。</summary>
        private static void SmoothLandField(PlanetClimateResult result, int res, float[] field, int iterations)
        {
            var buffer = new float[field.Length];
            for (int pass = 0; pass < iterations; pass++)
            {
                for (int y = 0; y < res; y++)
                {
                    for (int x = 0; x < res; x++)
                    {
                        int i = y * res + x;
                        if (result.Ocean[i]) { buffer[i] = field[i]; continue; }

                        float sum = 0f;
                        int n = 0;
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            int ny = Mathf.Clamp(y + dy, 0, res - 1);
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                int nx = (x + dx + res) % res;
                                int ni = ny * res + nx;
                                if (result.Ocean[ni]) continue;
                                sum += field[ni];
                                n++;
                            }
                        }
                        buffer[i] = n > 0 ? sum / n : field[i];
                    }
                }
                System.Array.Copy(buffer, field, field.Length);
            }
        }

        private void BuildColorsAndStats(PlanetClimateResult result, int res, float altitudeRange)
        {
            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    int i = y * res + x;
                    bool isOcean = result.Ocean[i];
                    float temperature = result.Temperature[i];
                    float humidity = result.Humidity[i];
                    float altitudeNorm = isOcean
                        ? 0f
                        : Mathf.Clamp01((result.Height[i] - _data.SeaLevel) / altitudeRange);

                    var type = _cfg.Classify(temperature, humidity, isOcean);
                    result.Types[i] = type;
                    result.Pixels[i] = EvaluateBiomeColor(temperature, humidity, altitudeNorm,
                        result.CoastProximity[i], result.Relief[i], isOcean);

                    if (isOcean) continue;
                    result.LandCells++;
                    if (type == ClimateType.Desert) result.DesertCells++;
                    else if (type == ClimateType.PolarIce || type == ClimateType.Tundra) result.SnowCells++;
                    if (temperature < result.MinTemperature) result.MinTemperature = temperature;
                    if (temperature > result.MaxTemperature) result.MaxTemperature = temperature;
                    if (humidity < result.MinHumidity) result.MinHumidity = humidity;
                    if (humidity > result.MaxHumidity) result.MaxHumidity = humidity;
                }
            }
        }

        /// <summary>多源 BFS 求到最近海洋的像素距离（x 方向环绕）。</summary>
        private static int[] BuildCoastDistance(bool[] ocean, int res)
        {
            int count = res * res;
            var distance = new int[count];
            var queue = new System.Collections.Generic.Queue<int>(count / 4);

            for (int i = 0; i < count; i++)
            {
                if (ocean[i]) { distance[i] = 0; queue.Enqueue(i); }
                else distance[i] = -1;
            }

            while (queue.Count > 0)
            {
                int index = queue.Dequeue();
                int x = index % res;
                int y = index / res;
                int next = distance[index] + 1;

                Try(x == 0 ? res - 1 : x - 1, y);
                Try(x == res - 1 ? 0 : x + 1, y);
                Try(x, y - 1);
                Try(x, y + 1);

                void Try(int nx, int ny)
                {
                    if (ny < 0 || ny >= res) return;
                    int ni = ny * res + nx;
                    if (distance[ni] >= 0) return;
                    distance[ni] = next;
                    queue.Enqueue(ni);
                }
            }

            for (int i = 0; i < count; i++)
                if (distance[i] < 0) distance[i] = res;

            return distance;
        }

        /// <summary>
        /// 在 coarse 分辨率上，从每个陆地格沿上风方向步进，找到海洋为止。
        /// 返回值 0=紧邻海洋，1=上风方向一直是陆地（最干燥）。
        /// </summary>
        private float[] BuildCoarseWindFetch(int coarse)
        {
            int count = coarse * coarse;
            var fetch = new float[count];
            Vector3 windDir = _cfg.GetWindDirection();
            int steps = Mathf.Clamp(_cfg.windFetchSteps, 16, 160);
            float stepAngle = 2.0f / coarse;

            var heights = new float[count];
            var ocean = new bool[count];
            for (int y = 0; y < coarse; y++)
            {
                float v = (y + 0.5f) / coarse;
                for (int x = 0; x < coarse; x++)
                {
                    int i = y * coarse + x;
                    Vector3 dir = PlanetTerrainSampler.DirectionFromUV((x + 0.5f) / coarse, v);
                    heights[i] = PlanetTerrainSampler.SampleHeight(_data, dir);
                    ocean[i] = heights[i] < _data.SeaLevel;
                }
            }

            for (int y = 0; y < coarse; y++)
            {
                float v = (y + 0.5f) / coarse;
                for (int x = 0; x < coarse; x++)
                {
                    int i = y * coarse + x;
                    if (ocean[i]) { fetch[i] = 0f; continue; }

                    Vector3 p = PlanetTerrainSampler.DirectionFromUV((x + 0.5f) / coarse, v);
                    float found = 1f;
                    for (int step = 1; step <= steps; step++)
                    {
                        // 沿球面逆风走：把风向量投影到切平面，再向反方向旋转。
                        Vector3 tangent = Vector3.ProjectOnPlane(EarthClimateRules.Wind(p,_cfg), p);
                        if (tangent.sqrMagnitude < 1e-8f) break;
                        tangent.Normalize();
                        p = (p * Mathf.Cos(stepAngle) - tangent * Mathf.Sin(stepAngle)).normalized;

                        if (PlanetTerrainSampler.SampleHeight(_data, p) < _data.SeaLevel)
                        {
                            found = (float)step / steps;
                            break;
                        }
                    }
                    fetch[i] = found;
                }
            }

            // 轻量平滑，避免 coarse 格点插值后出现网格感。
            var smoothed = new float[count];
            for (int y = 0; y < coarse; y++)
            {
                for (int x = 0; x < coarse; x++)
                {
                    float sum = 0f;
                    int n = 0;
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        int ny = Mathf.Clamp(y + dy, 0, coarse - 1);
                        int row = ny * coarse;
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = (x + dx + coarse) % coarse;
                            sum += fetch[row + nx];
                            n++;
                        }
                    }
                    smoothed[y * coarse + x] = sum / n;
                }
            }
            return smoothed;
        }



        /// <summary>
        /// 迎风抬升 / 背风雨影，都写成连续权重 0~1。
        /// 旧版用 max(0, ±局部坡度) 在 1 像素内从 +26 翻到 -24，正是山区
        /// 湿度图出现狭长“断层”的原因；这里改为在上风方 8~30 像素窗口内
        /// 采样，让过渡自然铺开。
        /// </summary>


        /// <summary>
        /// 一次采样四邻域，同时得到局部起伏（山地更干）和盆地冷空气堆积。
        /// ★ 优化：旧版把这两件事拆成两次四邻域采样，气候图每像元多跑 4 次高度插值。
        /// </summary>


        /// <summary>连续混色：所有生态色都按权重叠加，没有离散 if/else。</summary>
        private Color EvaluateBiomeColor(float temperature, float humidity, float altitudeNorm,
            float coastProximity, float relief, bool isOcean)
        {
            if (isOcean)
            {
                float oceanWarmT = Mathf.InverseLerp(-5f, 30f, temperature);
                return Color.Lerp(_cfg.oceanCold, _cfg.oceanWarm, oceanWarmT);
            }

            float wet = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(18f, 85f, humidity));
            float warm = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(4f, 30f, temperature));
            // 干旱权重直接由湿度决定，和 Classify 的沙漠阈值保持一致，
            // 避免“分类是草原、颜色却是沙漠黄”的观感错位。
            float arid = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(38f, 14f, humidity))
                * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(4f, 18f, temperature));
            float cold = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(_cfg.tundraTemp + 6f, _cfg.tundraTemp - 6f, temperature));
            float ice = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(_cfg.polarIceTemp + 6f, _cfg.polarIceTemp - 6f, temperature));

            Color color = Color.Lerp(_cfg.savanna, _cfg.grassland, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.25f, 0.55f, wet)));
            color = Color.Lerp(color, _cfg.forest, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 0.72f, wet)));
            color = Color.Lerp(color, _cfg.rainforest, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.65f, 0.92f, wet)) * warm);
            color = Color.Lerp(color, _cfg.desert, arid * (1f - cold));
            color = Color.Lerp(color, _cfg.tundra, cold);
            color = Color.Lerp(color, _cfg.polarIce, ice);

            if(_cfg.earth?.enabled==true)color=EarthClimateRules.Color(temperature,humidity,_cfg);
            // 高山岩石与雪线：只影响真正的高海拔区域，避免整片大陆变白。
            float rock = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 0.75f, altitudeNorm))
                * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.1f, 0.5f, relief));
            color = Color.Lerp(color, _cfg.rock, rock * 0.75f);

            float snowLine = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 0.85f, altitudeNorm))
                * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(2f, -8f, temperature));
            color = Color.Lerp(color, _cfg.polarIce, snowLine * 0.85f);

            // 低海拔海岸沙滩，只影响很窄的一圈，让海岸线更自然。
            float beach = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f, 0.9f, coastProximity))
                * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.06f, 0.005f, altitudeNorm))
                * (1f - cold) * 0.55f;
            color = Color.Lerp(color, _cfg.beach, beach);

            return color;
        }

    }
}
