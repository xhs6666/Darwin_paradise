using UnityEngine;
using DarwinPark.Core;

namespace DarwinPark.Planet
{
    public enum ClimateType
    {
        Ocean,          // 海洋
        TropicalRain,   // 热带雨林
        Desert,         // 沙漠
        Grassland,      // 草原
        Forest,         // 温带森林
        Tundra,         // 苔原
        PolarIce        // 极地冰盖
    }

    public struct ClimateData
    {
        public float Temperature;   // 温度 (-50 ~ 50)
        public float Humidity;      // 湿度 (0 ~ 100)
        public float Precipitation; // 降雨量 (0 ~ 100)
        public ClimateType Type;    // 气候类型
    }

    /// <summary>
    /// 星球气候系统。提供接口给实体查询当前气候。
    /// ★ 修复：气候图生成与实体查询共用同一气候模型；实体查询优先读取预计算气候图，
    /// 避免旧版“两张气候表”导致的沿海沙漠、温湿度不一致。
    /// </summary>
    public class PlanetClimateSystem : MonoBehaviour
    {
        public static PlanetClimateSystem Instance { get; private set; }

        [Header("星球基准")]
        public float planetRadius = 300f;

        [Header("气候模型（数据驱动）")]
        public PlanetClimateConfig config = new PlanetClimateConfig();

        private PlanetTerrainMeshBuilder _builder;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;

            if (config == null)
            {
                Log.Error(Log.Tag.Planet, "PlanetClimateConfig 为空，使用代码默认气候参数");
                config = new PlanetClimateConfig();
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            _builder = GetComponent<PlanetTerrainMeshBuilder>();
            if (_builder == null)
                Log.Error(Log.Tag.Planet, "PlanetClimateSystem 找不到 PlanetTerrainMeshBuilder，海陆判断将回退为球面半径");
        }

        /// <summary>
        /// 获取世界坐标处的气候数据。
        /// </summary>
        public ClimateData GetClimateAt(Vector3 worldPos)
        {
            // ★ 修复：通过 Core 的 ClimateDataHub 读取季节偏移，
            // 避免 Planet 引用 Environment 而 Environment 又引用 Planet 的循环依赖。
            float seasonalTempOffset = ClimateDataHub.SeasonalTemperatureOffset;
            float seasonalHumidityOffset = ClimateDataHub.SeasonalHumidityOffset;

            // ★ 修复：优先使用气候图预计算值，实体感知与图层显示保持一致。
            if (PlanetClimateMap.Instance != null &&
                PlanetClimateMap.Instance.SampleClimate(worldPos, seasonalTempOffset, seasonalHumidityOffset, out var mapped))
            {
                return mapped;
            }

            return ComputeFallback(worldPos, seasonalTempOffset, seasonalHumidityOffset);
        }

        private ClimateData ComputeFallback(Vector3 worldPos, float tempOffset, float humidityOffset)
        {
            if (_builder == null) _builder = GetComponent<PlanetTerrainMeshBuilder>();
            if (_builder == null) _builder = FindFirstObjectByType<PlanetTerrainMeshBuilder>();

            Vector3 localPos = worldPos - transform.position;
            float distance = localPos.magnitude;
            // ★ 修复：地形数据在星球本地空间；星球被旋转时不能直接用世界方向采样，
            // 否则海陆/海拔判断会随星球自转而漂移。
            Vector3 unitDir = transform.InverseTransformDirection(localPos).normalized;
            // ★ 修复：0°=赤道、90°=极地；旧代码用 Acos(y) 会把极地当成赤道。
            float latDeg = Mathf.Asin(Mathf.Clamp(Mathf.Abs(unitDir.y), 0f, 1f)) * Mathf.Rad2Deg;

            float seaLevel = _builder != null && _builder.Data != null ? _builder.Data.SeaLevel : 0.45f;
            float height = 0f;
            float waterRadius = _builder != null ? _builder.WaterRadius : planetRadius;
            bool isOcean = distance < waterRadius;

            if (_builder != null && _builder.Data != null)
            {
                height = PlanetTerrainSampler.SampleHeight(_builder.Data, unitDir);
            }

            float altitudeNorm = Mathf.Clamp01(
                (height - seaLevel) / Mathf.Max(0.01f, PlanetTerrainData.MaxNormalizedHeight - seaLevel));
            var cfg = config ?? new PlanetClimateConfig();
            float latCurve = Mathf.Pow(Mathf.Cos(latDeg * Mathf.Deg2Rad), Mathf.Max(0.1f, cfg.latitudeTempExponent));


            // ★ 适配：初始化回退也必须遵循辐射权重，避免前后出现完全不同的纬度气候带。
            float temp = Mathf.Lerp(cfg.regionalTemperature, Mathf.Lerp(cfg.poleTemp, cfg.equatorTemp, latCurve), cfg.solarTemperatureWeight);
            temp += ClimateDataHub.BaseTemperature - cfg.baseTemperatureReference;
            temp -= altitudeNorm * cfg.altitudeTempDrop;

            // 气候图未就绪时的最小回退：只代表“高空/内陆更干”，不参与正式气候生成。
            float humidity = 55f
                + ClimateDataHub.BaseHumidity - cfg.baseHumidityReference
                - altitudeNorm * 18f;

            if (isOcean)
            {
                float oceanTarget = Mathf.Lerp(cfg.regionalTemperature, Mathf.Lerp(cfg.oceanPoleTemp, cfg.oceanEquatorTemp, latCurve), cfg.solarTemperatureWeight);
                temp = Mathf.Lerp(temp, oceanTarget, cfg.oceanTempModeration);
                humidity = 100f;
            }

            temp = Mathf.Clamp(temp + tempOffset, -60f, 70f);
            humidity = Mathf.Clamp(humidity + humidityOffset, 0f, 100f);
            float precipitation = Mathf.Clamp(humidity - altitudeNorm * cfg.altitudePrecipitationPenalty, 0f, 100f);

            return new ClimateData
            {
                Temperature = temp,
                Humidity = humidity,
                Precipitation = precipitation,
                Type = cfg.Classify(temp, humidity, isOcean)
            };
        }
    }
}
