using UnityEngine;
using DarwinPark.Core;

namespace DarwinPark.Environment
{
    public class EnvironmentSystem : MonoBehaviour
    {
        [Header("配置")]
        [Tooltip("Resources/Environment 下的配置 id（与文件名一致）")]
        public string ConfigId = "environment_default";

        [Header("全年平均气候基准")]
        public float BaseTemperature = 20f;
        public float BaseHumidity = 44f;

        [Header("季节波动幅度 (1年 = 360秒)")]
        public bool enableClimateCycle = true;
        public float TempSwing = 15f;
        public float HumSwing = 30f;

        public float CurrentTemperature { get; private set; }
        public float CurrentHumidity { get; private set; }

        /// <summary>★ 新增：环境伤害/舒适阈值统一读取该配置。</summary>
        public EnvironmentConfig Config { get; private set; }

        private Transform _planetRoot;

        private void Awake()
        {
            if (ServiceLocator.TryGet<EnvironmentSystem>(out _))
            {
                Debug.LogWarning("[Env] 重复实例，销毁");
                Destroy(this);
                return;
            }
            ServiceLocator.Register(this);

            Config = EnvironmentConfigDatabase.Instance.Get(ConfigId);
            if (Config == null)
            {
                Log.Error(Log.Tag.Environment, $"环境配置缺失：{ConfigId}，使用代码内默认值");
                Config = new EnvironmentConfig();
            }
            else
            {
                BaseTemperature = Config.baseTemperature;
                BaseHumidity = Config.baseHumidity;
                Log.Info(Log.Tag.Environment,
                    $"环境配置已加载：{Config.id}，基准 {BaseTemperature:F1}°C / {BaseHumidity:F1}%");
            }
        }

        private void Start()
        {
            // ★ 修复警告：将 FindObjectOfType 改为 FindFirstObjectByType
            var builder = FindFirstObjectByType<DarwinPark.Planet.PlanetTerrainMeshBuilder>();
            _planetRoot = builder != null ? builder.transform : null;
        }

        private void Update()
        {
            float time = ServiceLocator.TryGet<SimulationClock>(out var clock) ? clock.GameTime : 0f;

            if (enableClimateCycle)
            {
                float cycleRad = (time / 3600f) * Mathf.PI * 2f; // 改为 3600 秒为一年
                float seasonalTemp = Mathf.Sin(cycleRad) * TempSwing;
                float seasonalHum = Mathf.Cos(cycleRad) * HumSwing;

                CurrentTemperature = BaseTemperature + seasonalTemp;
                CurrentHumidity = BaseHumidity + seasonalHum;
            }
            else
            {
                CurrentTemperature = BaseTemperature;
                CurrentHumidity = BaseHumidity;
            }

            // ★ 新增：将当前基准温度湿度写入中转站，供 Planet 读取
            ClimateDataHub.BaseTemperature = BaseTemperature;
            ClimateDataHub.BaseHumidity = BaseHumidity;
            // ★ 修复：季节偏移也写入 Core 中转站，避免 Planet 程序集反向引用 Environment 造成循环依赖。
            ClimateDataHub.SeasonalTemperatureOffset = CurrentTemperature - BaseTemperature;
            ClimateDataHub.SeasonalHumidityOffset = CurrentHumidity - BaseHumidity;

            UpdatePlanetShader();
        }

        private void UpdatePlanetShader()
        {
            var planet = GameObject.Find("Planet");
            if (planet != null)
            {
                var mr = planet.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    var block = new MaterialPropertyBlock();
                    mr.GetPropertyBlock(block);
                    block.SetFloat("_TemperatureOffset", CurrentTemperature);
                    block.SetFloat("_HumidityOffset", CurrentHumidity);
                    mr.SetPropertyBlock(block);
                }
            }
        }

        private void OnDisable() => ServiceLocator.Unregister<EnvironmentSystem>();

        public EnvironmentSample Sample(Vector3 pos)
        {
            return new EnvironmentSample { Temperature = CurrentTemperature, Humidity = CurrentHumidity };
        }
    }
}