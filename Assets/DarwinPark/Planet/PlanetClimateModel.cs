using System;
using UnityEngine;

namespace DarwinPark.Planet
{
    /// <summary>气候图的一次生成结果（纯数据，方便在编辑器/测试中复用与验证）。</summary>
    public sealed class PlanetClimateResult
    {
        public int Resolution;
        public float SeaLevel;

        public float[] Height;
        public float[] Temperature;
        public float[] Humidity;
        public float[] Precipitation;
        public float[] CoastProximity;
        public float[] WindFetch;
        public float[] Continentality;
        public float[] Relief;
        public bool[] Ocean;
        public Color[] Pixels;
        public ClimateType[] Types;

        public int LandCells;
        public int DesertCells;
        public int SnowCells;
        public float MinTemperature = float.MaxValue;
        public float MaxTemperature = float.MinValue;
        public float MinHumidity = float.MaxValue;
        public float MaxHumidity = float.MinValue;
    }

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

            // ---------- 3. 温度/湿度噪声 ----------
            var temperatureNoise = new FBMNoise(new FBMConfig
            {
                octaves = 3,
                frequency = _cfg.temperatureNoiseFrequency,
                amplitude = 1f,
                lacunarity = 2f,
                gain = 0.5f,
                warpStrength = 0.35f
            }, _seed + 1701);

            var currentNoise = new FBMNoise(new FBMConfig
            {
                octaves = 2,
                frequency = _cfg.oceanCurrentFrequency,
                amplitude = 1f,
                lacunarity = 2f,
                gain = 0.5f,
                warpStrength = 0.5f
            }, _seed + 2303);

            var moistureNoise = new FBMNoise(new FBMConfig
            {
                octaves = 3,
                frequency = _cfg.moistureNoiseFrequency,
                amplitude = 1f,
                lacunarity = 2f,
                gain = 0.5f,
                warpStrength = 0.25f
            }, _seed + 9001);

            Vector3 windDir = _cfg.GetWindDirection();
            float maxHeight = PlanetTerrainData.MaxNormalizedHeight;
            float altitudeRange = Mathf.Max(0.01f, maxHeight - _data.SeaLevel);

            for (int y = 0; y < res; y++)
            {
                float v = (y + 0.5f) / res;
                float lat = (v - 0.5f) * Mathf.PI;
                float solar = Mathf.Pow(Mathf.Cos(lat), Mathf.Max(0.1f, _cfg.latitudeTempExponent));
                // ★ 修复（防Bug回归）：降低纬向辐射支配，温度由区域、海洋、海拔和水分共同决定。
                float regionalBase = _cfg.regionalTemperature;

                for (int x = 0; x < res; x++)
                {
                    int i = y * res + x;
                    float u = (x + 0.5f) / res;
                    Vector3 dir = PlanetTerrainSampler.DirectionFromUV(u, v);
                    bool isOcean = result.Ocean[i];
                    float height = result.Height[i];
                    float altitudeNorm = Mathf.Clamp01((height - _data.SeaLevel) / altitudeRange);
                    float coastProximity = Mathf.Exp(-coastDistance[i] / coastScale);
                    float continentality = 1f - coastProximity;
                    float fetch = SampleCoarseBilinear(coarseFetch, coarseResolution, u, v);

                    result.CoastProximity[i] = coastProximity;
                    result.Continentality[i] = continentality;
                    result.WindFetch[i] = fetch;

                    // ---------- 温度：太阳辐射是基准，但不是唯一条件 ----------
                    float oceanTemperature = Mathf.Lerp(regionalBase, Mathf.Lerp(_cfg.oceanPoleTemp, _cfg.oceanEquatorTemp, solar), _cfg.solarTemperatureWeight)
                        + currentNoise.Sample(dir) * _cfg.oceanCurrentAmplitude + tempOffset;

                    float temperature = Mathf.Lerp(regionalBase, Mathf.Lerp(_cfg.poleTemp, _cfg.equatorTemp, solar), _cfg.solarTemperatureWeight)
                        + tempOffset
                        - altitudeNorm * _cfg.altitudeTempDrop
                        + temperatureNoise.Sample(dir) * _cfg.temperatureNoiseAmplitude
                            * (0.45f + 0.55f * continentality);

                    if (isOcean)
                    {
                        temperature = Mathf.Lerp(temperature, oceanTemperature, _cfg.oceanTempModeration);
                    }
                    else
                    {
                        // 近海被海水热惯性拉平，内陆温差更大。
                        temperature = Mathf.Lerp(temperature, oceanTemperature,
                            _cfg.coastalTempModeration * Mathf.Pow(coastProximity, 1.5f));
                    }

                    // ---------- 湿度：风程 + 海岸 + 迎风坡 + 雨影 + 起伏 ----------
                    float humidity;
                    float relief = 0f;
                    float windLift = 0f;
                    if (isOcean)
                    {
                        humidity = 100f;
                    }
                    else
                    {
                        SampleNeighbourhood(dir, height, out relief, out float coldPool);
                        result.Relief[i] = relief;
                        temperature -= coldPool * _cfg.basinColdPoolDrop;
                        float fetchMoisture = Mathf.Exp(-fetch / Mathf.Max(0.05f, _cfg.windFetchScale));
                        ComputeWindExposure(dir, height, windDir, out windLift, out float rainShadow01);
                        // windwardMoistureBonus / leewardDryness 继续作为迎风/背风的调参倍率，
                        // 与 orographic/rainShadow 的绝对幅度配合使用。
                        float orographic = windLift * _cfg.orographicMoistureBonus
                            * (1f + Mathf.Max(0f, _cfg.windwardMoistureBonus));
                        float rainShadow = rainShadow01 * _cfg.rainShadowDryness
                            * (1f + Mathf.Max(0f, _cfg.leewardDryness));

                        humidity = _cfg.baseHumidityReference + humidityOffset
                            + _cfg.windFetchMoisture * fetchMoisture
                            + _cfg.coastInfluence * coastProximity
                            - _cfg.inlandDryness * continentality
                            - relief * _cfg.terrainReliefMoisturePenalty
                            + orographic
                            - rainShadow
                            + moistureNoise.Sample(dir) * _cfg.moistureNoiseAmplitude;

                        // ★ 平滑海岸下限：用连续权重把海岸附近“托”到目标湿度，
                        // 代替旧版 `if (coastProximity >= threshold) humidity = max(...)`
                        // 的硬阈值——那条硬阈值正是气候图出现等距断层的根因之一。
                        float coastWeight = Mathf.SmoothStep(0f, 1f,
                            Mathf.InverseLerp(_cfg.coastProximityThreshold * 0.45f,
                                _cfg.coastProximityThreshold + 0.25f, coastProximity));
                        float coastTarget = _cfg.minCoastHumidity + humidityOffset * 0.5f;
                        humidity = Mathf.Lerp(humidity, Mathf.Max(humidity, coastTarget), coastWeight * 0.85f);
                    }

                    float precipitation = isOcean
                        ? 100f
                        : Mathf.Clamp(humidity - altitudeNorm * _cfg.altitudePrecipitationPenalty
                            + windLift * _cfg.orographicMoistureBonus * 0.3f, 0f, 100f);

                    // 干旱区增温：放在湿度之后，让“缺水 + 高温”形成正反馈，
                    // 同时不引入任何离散分支（连续权重）。
                    if (!isOcean)
                    {
                        float dryHeat = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(55f, 15f, humidity));
                        temperature += dryHeat * _cfg.aridHeatBonus * Mathf.Max(0f, solar);
                    }

                    result.Temperature[i] = Mathf.Clamp(temperature, -60f, 60f);
                    result.Humidity[i] = Mathf.Clamp(humidity, 4f, 100f);
                    result.Precipitation[i] = precipitation;
                }
            }

            // ★ 新增：对陆地温湿度做两次小范围平滑。
            // 迎风/背风在真实山脊上本来就是突变；低分辨率纹理若逐像素保留，
            // 会被玩家看成“气候断层”。扩散几像素后既保留雨影，又消除刀切边。
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
                        Vector3 tangent = Vector3.ProjectOnPlane(windDir, p);
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

        private static float SampleCoarseBilinear(float[] data, int coarse, float u, float v)
        {
            float fx = u * coarse - 0.5f;
            float fy = Mathf.Clamp(v * coarse - 0.5f, 0f, coarse - 1.001f);
            int x0 = Mathf.FloorToInt(fx);
            int y0 = Mathf.FloorToInt(fy);
            float tx = fx - x0;
            float ty = fy - y0;
            int x1 = (x0 + 1) % coarse;
            x0 = (x0 % coarse + coarse) % coarse;
            int y1 = Mathf.Clamp(y0 + 1, 0, coarse - 1);
            y0 = Mathf.Clamp(y0, 0, coarse - 1);

            float a = Mathf.Lerp(data[y0 * coarse + x0], data[y0 * coarse + x1], tx);
            float b = Mathf.Lerp(data[y1 * coarse + x0], data[y1 * coarse + x1], tx);
            return Mathf.Lerp(a, b, ty);
        }

        /// <summary>
        /// 迎风抬升 / 背风雨影，都写成连续权重 0~1。
        /// 旧版用 max(0, ±局部坡度) 在 1 像素内从 +26 翻到 -24，正是山区
        /// 湿度图出现狭长“断层”的原因；这里改为在上风方 8~30 像素窗口内
        /// 采样，让过渡自然铺开。
        /// </summary>
        private void ComputeWindExposure(Vector3 dir, float height, Vector3 windDir,
            out float lift01, out float rainShadow01)
        {
            lift01 = 0f;
            rainShadow01 = 0f;

            Vector3 tangent = Vector3.ProjectOnPlane(windDir, dir);
            if (tangent.sqrMagnitude < 1e-8f) return;
            tangent.Normalize();

            // 上风方三个距离：近处判断抬升，远处判断是否被山体挡住了水汽。
            float upNear = PlanetTerrainSampler.SampleHeight(_data, (dir - tangent * 0.015f).normalized);
            float upMid = PlanetTerrainSampler.SampleHeight(_data, (dir - tangent * 0.035f).normalized);
            float upFar = PlanetTerrainSampler.SampleHeight(_data, (dir - tangent * 0.065f).normalized);

            float rise = height - upNear;
            lift01 = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.01f, 0.12f, rise));

            float blocked = Mathf.Max(upMid, upFar) - height;
            float shadow = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.02f, 0.14f, blocked));
            rainShadow01 = shadow * (1f - lift01);
        }

        /// <summary>
        /// 一次采样四邻域，同时得到局部起伏（山地更干）和盆地冷空气堆积。
        /// ★ 优化：旧版把这两件事拆成两次四邻域采样，气候图每像元多跑 4 次高度插值。
        /// </summary>
        private void SampleNeighbourhood(Vector3 dir, float centerHeight,
            out float relief01, out float coldPool01)
        {
            Vector3 axis = Mathf.Abs(Vector3.Dot(dir, Vector3.up)) > 0.99f ? Vector3.right : Vector3.up;
            Vector3 tangent = Vector3.Cross(dir, axis).normalized;
            Vector3 bitangent = Vector3.Cross(dir, tangent).normalized;
            const float reliefStep = 0.006f;
            const float basinStep = 0.018f;

            float h1 = PlanetTerrainSampler.SampleHeight(_data, (dir + tangent * reliefStep).normalized);
            float h2 = PlanetTerrainSampler.SampleHeight(_data, (dir - tangent * reliefStep).normalized);
            float h3 = PlanetTerrainSampler.SampleHeight(_data, (dir + bitangent * reliefStep).normalized);
            float h4 = PlanetTerrainSampler.SampleHeight(_data, (dir - bitangent * reliefStep).normalized);

            float maxDiff = Mathf.Max(
                Mathf.Max(Mathf.Abs(h1 - centerHeight), Mathf.Abs(h2 - centerHeight)),
                Mathf.Max(Mathf.Abs(h3 - centerHeight), Mathf.Abs(h4 - centerHeight)));
            relief01 = Mathf.Clamp01(maxDiff * 12f);

            // 用较远一圈的四个方向估计周围地势，避免把山峰本身当成盆地。
            float b1 = PlanetTerrainSampler.SampleHeight(_data, (dir + tangent * basinStep).normalized);
            float b2 = PlanetTerrainSampler.SampleHeight(_data, (dir - tangent * basinStep).normalized);
            float b3 = PlanetTerrainSampler.SampleHeight(_data, (dir + bitangent * basinStep).normalized);
            float b4 = PlanetTerrainSampler.SampleHeight(_data, (dir - bitangent * basinStep).normalized);
            float avg = (b1 + b2 + b3 + b4) * 0.25f;
            coldPool01 = Mathf.Clamp01((avg - centerHeight) * 8f);
        }

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
