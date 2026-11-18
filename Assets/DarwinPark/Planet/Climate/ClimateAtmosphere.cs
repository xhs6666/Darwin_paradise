using UnityEngine;
namespace DarwinPark.Planet
{
    // ✦ 新增：每次生成独立的温湿度计算上下文，保持并行世界之间无共享可变状态。
    internal sealed class ClimateAtmosphere
    {
        private readonly PlanetTerrainData _data;private readonly PlanetClimateConfig _cfg;private readonly int _seed;
        private PlanetClimateResult result;private int res,coarseResolution;private int[] coastDistance;private float[] coarseFetch;
        private float coastScale,altitudeRange,tempOffset,humidityOffset;
        private FBMNoise temperatureNoise,currentNoise,moistureNoise;
        public ClimateAtmosphere(PlanetTerrainData data,PlanetClimateConfig config,int seed){_data=data;_cfg=config;_seed=seed;}
        public void Build(PlanetClimateResult output,int[] coast,float[] fetch,int coarse,float coastFalloff,float heightRange,float temperatureOffset,float moistureOffset)
        {
            result=output;res=output.Resolution;coastDistance=coast;coarseFetch=fetch;coarseResolution=coarse;
            coastScale=coastFalloff;altitudeRange=heightRange;tempOffset=temperatureOffset;humidityOffset=moistureOffset;
            temperatureNoise = new FBMNoise(new FBMConfig
            {
                octaves = 3,
                frequency = _cfg.temperatureNoiseFrequency,
                amplitude = 1f,
                lacunarity = 2f,
                gain = 0.5f,
                warpStrength = 0.35f
            }, _seed + 1701);

            currentNoise = new FBMNoise(new FBMConfig
            {
                octaves = 2,
                frequency = _cfg.oceanCurrentFrequency,
                amplitude = 1f,
                lacunarity = 2f,
                gain = 0.5f,
                warpStrength = 0.5f
            }, _seed + 2303);

            moistureNoise = new FBMNoise(new FBMConfig
            {
                octaves = 3,
                frequency = _cfg.moistureNoiseFrequency,
                amplitude = 1f,
                lacunarity = 2f,
                gain = 0.5f,
                warpStrength = 0.25f
            }, _seed + 9001);


            for (int y = 0; y < res; y++)
            {
                float v = (y + 0.5f) / res;
                float lat = (v - 0.5f) * Mathf.PI;
                float solar = Mathf.Pow(Mathf.Cos(lat), Mathf.Max(0.1f, _cfg.latitudeTempExponent));
                // ★ 修复（防Bug回归）：降低纬向辐射支配，温度由区域、海洋、海拔和水分共同决定。
                float regionalBase = _cfg.regionalTemperature;

                for (int x = 0; x < res; x++)
                {
                    FillCell(x,y,v,solar,regionalBase);
                }
            }
        }
        private void FillCell(int x,int y,float v,float solar,float regionalBase)
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
                    float oceanTemperature = Mathf.Lerp(regionalBase, Mathf.Lerp(_cfg.oceanPoleTemp, _cfg.oceanEquatorTemp, solar), EarthClimateRules.SolarWeight(_cfg))
                        + (result.Circulation?.Heat(u,v) ?? currentNoise.Sample(dir)) * _cfg.oceanCurrentAmplitude + tempOffset;

                    float temperature = Mathf.Lerp(regionalBase, Mathf.Lerp(_cfg.poleTemp, _cfg.equatorTemp, solar), EarthClimateRules.SolarWeight(_cfg))
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
                        ComputeWindExposure(dir, height, EarthClimateRules.Wind(dir,_cfg), out windLift, out float rainShadow01);
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
                        // ★ 适配：赤道辐合/副热带下沉及真实河湖补湿，沿海下限不能覆盖下沉干燥。
                        humidity+=EarthClimateRules.Humidity(dir,_cfg)+(_data.Hydrology?.Moisture(dir)??0);
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
    }
}
