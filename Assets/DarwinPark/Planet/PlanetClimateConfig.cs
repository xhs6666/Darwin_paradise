using System;
using UnityEngine;

namespace DarwinPark.Planet
{
    /// <summary>
    /// 星球气候模型配置。
    /// ★ 修复：气候不再由“纬度湿度带”决定。温度只看太阳辐射作为基础，
    /// 再叠加海拔、离海距离、海洋热惯性、暖/寒洋流噪声与大陆性温度噪声；
    /// 湿度由盛行风的上风方海洋距离（风程）、海岸距离、迎风坡抬升、
    /// 背风雨影、地形起伏和斑块噪声共同决定。所有阈值/幅度都在这里配置。
    /// </summary>
    [Serializable]
    public class PlanetClimateConfig
    {
        [Header("温度条件（太阳辐射 + 海拔 + 海洋 + 大陆性）")]
        [Tooltip("赤道海平面年均温（摄氏度）")]
        public float equatorTemp = 31f;
        [Tooltip("极地海平面年均温（摄氏度）")]
        public float poleTemp = -26f;
        [Tooltip("太阳辐射纬度曲线指数，越大高纬度降温越快")]
        public float latitudeTempExponent = 1.1f;
        // ★ 修复（防Bug回归）：辐射只作为可调条件之一，禁止以纬度直接决定气候带。
        [Range(0f, 1f)] public float solarTemperatureWeight = 0.3f;
        public float regionalTemperature = 18f;
        [Tooltip("从海平面升到最高点时的大气降温（摄氏度）")]
        public float altitudeTempDrop = 22f;
        [Tooltip("基础温度基准：滑条设置为该值时气候图不偏移")]
        public float baseTemperatureReference = 20f;
        [Tooltip("大陆性温度噪声幅度：打破整齐的纬向温度带")]
        public float temperatureNoiseAmplitude = 5f;
        [Tooltip("大陆性温度噪声频率")]
        public float temperatureNoiseFrequency = 1.7f;
        [Tooltip("洋流温度异常幅度（暖流/寒流）")]
        public float oceanCurrentAmplitude = 5f;
        [Tooltip("洋流噪声频率")]
        public float oceanCurrentFrequency = 1.2f;
        [Tooltip("近海陆地温度向海水温度靠拢的比例")]
        [Range(0f, 1f)] public float coastalTempModeration = 0.5f;
        [Tooltip("盆地/谷地冷空气堆积造成的额外降温")]
        public float basinColdPoolDrop = 3f;
        [Tooltip("干旱区的地表增温：沙漠/荒漠比同纬度湿润区更热")]
        public float aridHeatBonus = 7f;

        [Header("海洋温度")]
        [Tooltip("海水温度向目标靠拢的比例")]
        [Range(0f, 1f)] public float oceanTempModeration = 0.6f;
        public float oceanEquatorTemp = 22f;
        public float oceanPoleTemp = -4f;

        [Header("湿度条件（风程 / 海岸 / 迎风坡 / 雨影）")]
        [Tooltip("基础湿度基准：湿度滑条设置为该值时气候图不偏移")]
        public float baseHumidityReference = 44f;
        [Tooltip("风从海洋吹到该点所携带的水汽上限")]
        public float windFetchMoisture = 24f;
        [Tooltip("风程衰减尺度：越大，海洋水汽能深入内陆越远")]
        [Range(0.05f, 1f)] public float windFetchScale = 0.24f;
        [Tooltip("计算风程时沿上风方向最多走多少步（性能与精度的平衡）")]
        [Range(16, 160)] public int windFetchSteps = 80;
        [Tooltip("海岸线额外带来的湿度")]
        public float coastInfluence = 16f;
        [Tooltip("深入内陆后损失的湿度；过大会让所有大陆内部都变成沙漠")]
        public float inlandDryness = 20f;
        [Tooltip("海岸湿度影响的归一化距离（占地图宽度比例）")]
        [Range(0.01f, 0.5f)] public float coastFalloffFraction = 0.1f;
        [Tooltip("海岸附近希望维持的湿度参考值（平滑混合，不是硬阈值）")]
        [Range(0f, 100f)] public float minCoastHumidity = 50f;
        [Tooltip("海岸判定阈值，用于平滑海岸下限的权重")]
        [Range(0f, 1f)] public float coastProximityThreshold = 0.45f;

        [Header("盛行风与地形降水")]
        [Tooltip("盛行风经度（度）。0=+Z，90=+X")]
        public float prevailingWindLongitudeDeg = 0f;
        [Tooltip("盛行风纬度（度）。正值向北")]
        public float prevailingWindLatitudeDeg = 0f;
        [Tooltip("迎风海岸的湿度加成倍率增量")]
        public float windwardMoistureBonus = 0.25f;
        [Tooltip("背风海岸的湿度折损倍率")]
        [Range(0f, 1f)] public float leewardDryness = 0.4f;
        [Tooltip("地形起伏/坡度造成的水分流失")]
        public float terrainReliefMoisturePenalty = 8f;
        [Tooltip("迎风坡抬升带来的额外降水")]
        public float orographicMoistureBonus = 26f;
        [Tooltip("背风坡下沉造成的雨影干燥")]
        public float rainShadowDryness = 24f;
        [Tooltip("湿度斑块噪声幅度")]
        public float moistureNoiseAmplitude = 6f;
        [Tooltip("湿度斑块噪声频率")]
        public float moistureNoiseFrequency = 2.8f;
        [Tooltip("高海拔降水折损")]
        public float altitudePrecipitationPenalty = 10f;

        [Header("气候分类阈值")]
        public float polarIceTemp = -22f;
        public float tundraTemp = -12f;
        public float tropicalTemp = 23f;
        public float desertTemp = 12f;
        public float desertHumidity = 32f;
        public float forestHumidity = 56f;
        public float tropicalHumidity = 74f;

        [Header("气候图层颜色（直接烘焙到纹理，避免硬分类断层）")]
        public Color oceanWarm = new Color(0.10f, 0.32f, 0.55f);
        public Color oceanCold = new Color(0.20f, 0.42f, 0.62f);
        public Color polarIce = new Color(0.91f, 0.93f, 0.97f);
        public Color tundra = new Color(0.47f, 0.50f, 0.43f);
        public Color desert = new Color(0.79f, 0.68f, 0.44f);
        public Color savanna = new Color(0.49f, 0.54f, 0.29f);
        public Color grassland = new Color(0.38f, 0.53f, 0.27f);
        public Color forest = new Color(0.15f, 0.35f, 0.18f);
        public Color rainforest = new Color(0.05f, 0.25f, 0.10f);
        public Color beach = new Color(0.72f, 0.67f, 0.49f);
        public Color rock = new Color(0.36f, 0.33f, 0.29f);

        public Vector3 GetWindDirection()
        {
            float lon = prevailingWindLongitudeDeg * Mathf.Deg2Rad;
            float lat = prevailingWindLatitudeDeg * Mathf.Deg2Rad;
            return new Vector3(
                Mathf.Cos(lat) * Mathf.Sin(lon),
                Mathf.Sin(lat),
                Mathf.Cos(lat) * Mathf.Cos(lon)).normalized;
        }

        /// <summary>
        /// 用于玩法（环境伤害）的离散气候分类。
        /// 图层颜色走连续混色，不依赖这里的硬阈值，因此不会产生色块断层。
        /// </summary>
        public ClimateType Classify(float temperature, float humidity, bool isOcean)
        {
            if (isOcean) return ClimateType.Ocean;
            if (temperature <= polarIceTemp) return ClimateType.PolarIce;
            if (temperature <= tundraTemp) return ClimateType.Tundra;
            if (temperature >= desertTemp && humidity <= desertHumidity) return ClimateType.Desert;
            if (temperature >= tropicalTemp && humidity >= tropicalHumidity) return ClimateType.TropicalRain;
            if (humidity >= forestHumidity) return ClimateType.Forest;
            return ClimateType.Grassland;
        }
    }
}
