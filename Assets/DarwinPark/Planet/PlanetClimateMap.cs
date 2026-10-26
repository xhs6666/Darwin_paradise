using UnityEngine;
using DarwinPark.Core;

namespace DarwinPark.Planet
{
    /// <summary>
    /// 预计算全球气候图。
    /// ★ 修复：气候生成逻辑已抽到 <see cref="PlanetClimateModel"/>（纯数据、可单测）。
    /// 本组件只负责：取地形数据 → 调模型 → 上传纹理 → 绑定到星球材质。
    ///
    /// 图层不再由纬度湿度带或 Shader 硬分类生成：温度、湿度、降水全部是连续场，
    /// 颜色在 CPU 端连续混色后写入 sRGB 纹理，因此不会出现色块/等距断层。
    /// </summary>
    public class PlanetClimateMap : MonoBehaviour
    {
        public static PlanetClimateMap Instance { get; private set; }

        [Header("气候图")]
        [Tooltip("气候纹理分辨率。越高越清晰，但生成耗时按平方增长")]
        // ★ 修复（防Bug回归）：近距离观察需要更密集的气候采样，避免 512 图在地面视角被放大成色块。
        public int mapResolution = 1024;

        public Texture2D ClimateTexture { get; private set; }
        public bool IsReady { get; private set; }

        private PlanetTerrainMeshBuilder _terrainBuilder;
        private PlanetClimateSystem _climateSystem;
        private Color[] _pixels;
        private float[] _heightMap;
        private float[] _temperatureMap;
        private float[] _humidityMap;
        private float[] _precipitationMap;
        private bool[] _oceanMap;
        private bool _isDirty;
        private int _generatedResolution;
        private float _lastLogTime = -10f;
        private float _lastGenerateTime = -10f;
        private const float MinRegenerateInterval = 0.6f;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            _terrainBuilder = GetComponent<PlanetTerrainMeshBuilder>();
            _climateSystem = GetComponent<PlanetClimateSystem>();

            if (_terrainBuilder == null)
                Log.Error(Log.Tag.Planet, "PlanetClimateMap 需要同一星球上的 PlanetTerrainMeshBuilder");
            if (_climateSystem == null)
                Log.Error(Log.Tag.Planet, "PlanetClimateMap 需要同一星球上的 PlanetClimateSystem，气候参数将使用默认值");

            Invoke(nameof(InitialGenerate), 0.25f);
        }

        private void InitialGenerate(){if(!WorldRuntimeGate.Managed || WorldRuntimeGate.Ready)GenerateClimateMap();}
        public void MarkDirty() => _isDirty = true;

        private void Update()
        {
            if(WorldRuntimeGate.Managed&&!WorldRuntimeGate.Ready)return;
            if (!_isDirty) return;
            // ★ 修复：滑条拖动时限制重建频率。气候生成包含风程步进，比旧版更重，
            // 因此间隔提高到 0.6s，避免拖滑条时每帧卡顿。
            if (Time.unscaledTime - _lastGenerateTime < MinRegenerateInterval) return;
            _isDirty = false;
            GenerateClimateMap();
        }

        public void GenerateClimateMap()
        {
            _lastGenerateTime = Time.unscaledTime;

            if (_terrainBuilder == null || _terrainBuilder.Data == null)
            {
                // 地形可能比气候图晚一帧就绪；保持 dirty，下一帧重试而不是永久失败。
                _isDirty = true;
                if (Time.unscaledTime - _lastLogTime >= 1f)
                {
                    _lastLogTime = Time.unscaledTime;
                    Log.Warn(Log.Tag.Planet, "气候图生成跳过：地形数据尚未就绪");
                }
                return;
            }

            int res = Mathf.Clamp(mapResolution, 64, 1024);
            mapResolution = res;
            var data = _terrainBuilder.Data;
            var cfg = _climateSystem != null && _climateSystem.config != null
                ? _climateSystem.config
                : new PlanetClimateConfig();
            if (_climateSystem == null)
                Log.Warn(Log.Tag.Planet, "PlanetClimateSystem 缺失，气候图使用默认配置");

            // 纹理只烘焙“基准温湿度”，季节偏移在 SampleClimate 时实时叠加，
            // 避免实体感知与图层显示出现两份偏移。
            float baseTempOffset = ClimateDataHub.BaseTemperature - cfg.baseTemperatureReference;
            float baseHumidityOffset = ClimateDataHub.BaseHumidity - cfg.baseHumidityReference;

            var model = new PlanetClimateModel(data, cfg, _terrainBuilder.seed);
            var result = model.Generate(res, baseTempOffset, baseHumidityOffset);

            ApplyGeneratedClimate(result);
        }
        // ★ 适配：气候纯数据可后台计算，纹理与材质必须主线程上传。
        public void ApplyGeneratedClimate(PlanetClimateResult result)
        {
            int res=result.Resolution;_isDirty=false;_lastGenerateTime=Time.unscaledTime;
            _pixels = result.Pixels;
            _heightMap = result.Height;
            _temperatureMap = result.Temperature;
            _humidityMap = result.Humidity;
            _precipitationMap = result.Precipitation;
            _oceanMap = result.Ocean;
            _generatedResolution = res;

            if (ClimateTexture == null || ClimateTexture.width != res)
            {
                // 颜色贴图使用 sRGB，避免把手工调好的气候颜色当成线性数据而洗白。
                ClimateTexture = new Texture2D(res, res, TextureFormat.RGBA32, false, false)
                {
                    name = "PlanetClimateMap",
                    filterMode = FilterMode.Bilinear
                };
            }
            // ★ 修复：经度方向必须 Repeat、纬度方向 Clamp。
            // 旧版整体 Clamp，接缝处双线性采样会跨到错误边界，表现为一条经线断层。
            ClimateTexture.wrapModeU = TextureWrapMode.Repeat;
            ClimateTexture.wrapModeV = TextureWrapMode.Clamp;

            ClimateTexture.SetPixels(_pixels);
            ClimateTexture.Apply(false, false);
            IsReady = true;
            BindToPlanet();

            if (Time.unscaledTime - _lastLogTime >= 1f)
            {
                _lastLogTime = Time.unscaledTime;
                float landPercent = result.LandCells / Mathf.Max(1f, res * res) * 100f;
                float desertPercent = result.DesertCells / Mathf.Max(1f, result.LandCells) * 100f;
                float snowPercent = result.SnowCells / Mathf.Max(1f, result.LandCells) * 100f;
                Log.Info(Log.Tag.Planet,
                    $"气候图生成完成：{res}x{res}，温度[{result.MinTemperature:F1},{result.MaxTemperature:F1}]°C，" +
                    $"湿度[{result.MinHumidity:F1},{result.MaxHumidity:F1}]%，陆地{landPercent:F1}%，" +
                    $"沙漠/陆地{desertPercent:F1}%，雪地/陆地{snowPercent:F1}%");
            }
        }

        /// <summary>★ 适配：把气候贴图写入指定 MaterialPropertyBlock，供图层按钮复用。</summary>
        public void ApplyToBlock(MaterialPropertyBlock block)
        {
            if (block == null) return;
            if (ClimateTexture != null) block.SetTexture("_ClimateMap", ClimateTexture);
            block.SetFloat("_ClimateReady", IsReady && ClimateTexture != null ? 1f : 0f);
        }

        private void BindToPlanet()
        {
            var mr = GetComponent<MeshRenderer>();
            if (mr == null) return;

            var block = new MaterialPropertyBlock();
            mr.GetPropertyBlock(block);
            block.SetTexture("_ClimateMap", ClimateTexture);
            block.SetFloat("_ClimateReady", 1f);
            mr.SetPropertyBlock(block);
        }

        /// <summary>
        /// 世界坐标采样气候。气候系统和 UI 共用，保证“看到的图层”和“实体感受到的气候”一致。
        /// </summary>
        public bool SampleClimate(Vector3 worldPos, float tempOffset, float humidityOffset, out ClimateData data)
        {
            data = default;
            if (!IsReady || _temperatureMap == null) return false;

            Vector3 dir = transform.InverseTransformDirection(worldPos - transform.position).normalized;
            if (dir.sqrMagnitude < 1e-6f) return false;

            int res = _generatedResolution > 0 ? _generatedResolution : mapResolution;
            Vector2 uv = PlanetTerrainSampler.DirectionToUV(dir);
            float fx = uv.x * res - 0.5f;
            float fy = uv.y * res - 0.5f;
            int x0 = Mathf.FloorToInt(fx);
            int y0 = Mathf.FloorToInt(fy);
            float tx = fx - x0;
            float ty = fy - y0;

            int x1 = WrapIndex(x0 + 1, res);
            int y1 = Mathf.Clamp(y0 + 1, 0, res - 1);
            x0 = WrapIndex(x0, res);
            y0 = Mathf.Clamp(y0, 0, res - 1);

            int i00 = y0 * res + x0;
            int i10 = y0 * res + x1;
            int i01 = y1 * res + x0;
            int i11 = y1 * res + x1;

            float temp = Bilerp(_temperatureMap[i00], _temperatureMap[i10], _temperatureMap[i01], _temperatureMap[i11], tx, ty);
            float humidity = Bilerp(_humidityMap[i00], _humidityMap[i10], _humidityMap[i01], _humidityMap[i11], tx, ty);
            float precipitation = Bilerp(_precipitationMap[i00], _precipitationMap[i10], _precipitationMap[i01], _precipitationMap[i11], tx, ty);
            bool isOcean = _oceanMap[i00] && _oceanMap[i10] && _oceanMap[i01] && _oceanMap[i11];

            var cfg = _climateSystem != null && _climateSystem.config != null
                ? _climateSystem.config
                : new PlanetClimateConfig();
            temp = Mathf.Clamp(temp + tempOffset, -60f, 70f);
            humidity = Mathf.Clamp(humidity + humidityOffset, 0f, 100f);
            precipitation = Mathf.Clamp(precipitation, 0f, 100f);

            data = new ClimateData
            {
                Temperature = temp,
                Humidity = humidity,
                Precipitation = precipitation,
                Type = cfg.Classify(temp, humidity, isOcean)
            };
            return true;
        }

        private static float Bilerp(float v00, float v10, float v01, float v11, float tx, float ty)
        {
            return Mathf.Lerp(Mathf.Lerp(v00, v10, tx), Mathf.Lerp(v01, v11, tx), ty);
        }

        private static int WrapIndex(int index, int size)
        {
            int wrapped = index % size;
            return wrapped < 0 ? wrapped + size : wrapped;
        }
    }
}
