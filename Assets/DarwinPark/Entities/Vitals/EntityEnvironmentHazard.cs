using UnityEngine;
using DarwinPark.Core;
using DarwinPark.Environment;
using DarwinPark.Genetics;
using DarwinPark.Entities.Stats;
using DarwinPark.Planet;

namespace DarwinPark.Entities.Vitals
{
    /// <summary>
    /// 温度与溺水环境伤害。
    /// ★ 新增：环境伤害从 EntityVitals 中独立出来，便于单独配置、调试和回滚。
    /// 所有阈值/伤害值来自 EnvironmentConfig（JSON），基因只提供“环境抵抗力”与耐寒/耐热方向。
    /// </summary>
    [RequireComponent(typeof(EntityStats), typeof(EntityGeneController))]
    public class EntityEnvironmentHazard : MonoBehaviour
    {
        private EntityStats _stats;
        private EntityGeneController _genes;
        private PlanetTerrainMeshBuilder _planetBuilder;

        private EnvironmentConfig _config;
        private bool _loggedMissingConfig;

        public float CurrentTemperature { get; private set; }
        public float CurrentHumidity { get; private set; }
        public bool IsDrowning { get; private set; }
        /// <summary>0~1，环境已经“压不住”的程度。</summary>
        public float StressLevel { get; private set; }

        private bool _isColdHazard;
        private bool _isHeatHazard;
        private float _lastTemperatureSeverity;
        private float _temperatureDamageScale;
        private float _lastDrowningSeverity;
        /// <summary>当前温度方向上的适应系数：1=完全适应，0.3=方向相反，0=没有适应基因。</summary>
        private float _adaptationFactor;

        private void Awake()
        {
            _stats = GetComponent<EntityStats>();
            _genes = GetComponent<EntityGeneController>();
        }

        private void Start()
        {
            FindPlanetBuilder();
        }

        private void Update()
        {
            if (ServiceLocator.TryGet<SimulationClock>(out var clock) && !clock.IsRunning) return;
            if (_stats == null) return;

            // ★ 修复（防Bug回归）：环境损耗随模拟时间推进，暂停时步长归零。
            float dt = clock != null ? clock.GameDeltaTime : Time.deltaTime;
            if (dt <= 0f) return;

            if (_config == null && ServiceLocator.TryGet<EnvironmentSystem>(out var env))
                _config = env.Config;
            if (_config == null)
            {
                if (!_loggedMissingConfig)
                {
                    _loggedMissingConfig = true;
                    Log.Error(Log.Tag.Environment, $"{name} 找不到 EnvironmentConfig，环境伤害使用默认值");
                }
                _config = new EnvironmentConfig();
            }

            UpdateTemperature(dt);
            UpdateDrowning(dt);
            ApplySelectionPressure();

            if (_stats.TryGet("health", out float hp) && hp <= _config.deathHealthThreshold)
            {
                Log.Info(Log.Tag.Entity, $"{name} 因环境伤害死亡");
                Destroy(gameObject);
            }
        }

        private void UpdateTemperature(float dt)
        {
            float temperature;
            float humidity;

            if (PlanetClimateSystem.Instance != null)
            {
                var climate = PlanetClimateSystem.Instance.GetClimateAt(transform.position);
                temperature = climate.Temperature;
                humidity = climate.Humidity;
            }
            else if (ServiceLocator.TryGet<EnvironmentSystem>(out var env))
            {
                // 气候系统未就绪时的最小回退，只使用全局温度。
                temperature = env.CurrentTemperature;
                humidity = env.CurrentHumidity;
            }
            else
            {
                _temperatureDamageScale = 0f;
                _lastTemperatureSeverity = 0f;
                _adaptationFactor = 0f;
                _isColdHazard = false;
                _isHeatHazard = false;
                return;
            }

            CurrentTemperature = temperature;
            CurrentHumidity = humidity;
            _isColdHazard = temperature < _config.coldThreshold;
            _isHeatHazard = temperature > _config.heatThreshold;
            _temperatureDamageScale = 0f;

            if (!_isColdHazard && !_isHeatHazard)
            {
                _lastTemperatureSeverity = 0f;
                _adaptationFactor = 0f;
                return;
            }

            float deviation = _isColdHazard
                ? _config.coldThreshold - temperature
                : temperature - _config.heatThreshold;
            float severity = Mathf.Clamp01(deviation / Mathf.Max(0.1f, _config.severityReferenceDelta));
            _lastTemperatureSeverity = severity;

            float resistance = GetEnvironmentResistance();
            float stateFactor = GetAdaptationStateFactor(_isColdHazard);
            // ★ 修复（防Bug回归）：方向匹配但抵抗不足时仍有适应缺口。
            _adaptationFactor = Mathf.Clamp01(resistance / 100f) * stateFactor;
            float mitigation = Mathf.Clamp01(resistance / 100f)
                * _config.maxEnvironmentResistance
                * stateFactor;
            float damageScale = severity * (1f - mitigation);
            _temperatureDamageScale = damageScale;

            if (damageScale <= 0f) return;

            // 环境压力同时消耗饱食度和生命值。用 Modify(..., false) 避免每帧闪红。
            _stats.Modify("nutrient", -_config.temperatureNutrientDrainPerSecond * damageScale * dt, false);
            _stats.Modify("health", -_config.temperatureHealthDamagePerSecond * damageScale * dt, false);
        }

        private void UpdateDrowning(float dt)
        {
            FindPlanetBuilder();
            if (_planetBuilder == null || _planetBuilder.Data == null)
            {
                IsDrowning = false;
                _lastDrowningSeverity = 0f;
                return;
            }

            float distanceToCenter = (transform.position - _planetBuilder.transform.position).magnitude;
            float waterRadius = _planetBuilder.WaterRadius;
            float depth = waterRadius - distanceToCenter;

            IsDrowning = depth > _config.drowningDepthThreshold;
            if (!IsDrowning)
            {
                _lastDrowningSeverity = 0f;
                return;
            }

            float severity = Mathf.Clamp01(depth / Mathf.Max(0.1f, _config.drowningSeverityDepth));
            _lastDrowningSeverity = severity;

            // ★ 新增：溺水伤害。环境抵抗力不抵消溺水，避免耐热/耐寒基因意外获得水下呼吸。
            _stats.Modify("nutrient", -_config.drowningNutrientDrainPerSecond * severity * dt, false);
            _stats.Modify("health", -_config.drowningHealthDamagePerSecond * severity * dt, false);
        }

        /// <summary>
        /// ★ 修复（防Bug回归）：按有效抵抗能力与温度方向计算适应缺口，
        /// 不能仅凭存在耐寒/耐热状态就认定完全适应。环境压力抑制所有基因，
        /// 同时提高环境适应基因变异概率；溺水不定向产生耐寒/耐热突变。
        /// </summary>
        private void ApplySelectionPressure()
        {
            if (_genes == null || _config == null) return;

            // UI 仍显示最大压力（包括溺水），但基因压力只来自温度适应失败。
            StressLevel = Mathf.Max(_temperatureDamageScale, _lastDrowningSeverity);
            float suppression = 0f;
            float mutationChance = 0f;
            float targetValue = -1f;

            float temperatureStress = Mathf.Clamp01(_temperatureDamageScale);
            float adaptationGap = Mathf.Clamp01(1f - _adaptationFactor);
            bool hasTemperatureHazard = _isColdHazard || _isHeatHazard;

            if (hasTemperatureHazard
                && adaptationGap > 0.01f
                && temperatureStress > _config.environmentStressThreshold)
            {
                float t = Mathf.InverseLerp(
                    _config.environmentStressThreshold, 1f, temperatureStress);
                suppression = t * _config.maxGlobalGeneSuppression * adaptationGap;
                mutationChance = _config.adaptationMutationChance * adaptationGap;

                if (_isColdHazard) targetValue = ResolveAdaptationTarget(_config.coldAdaptedStateId, 20f);
                else targetValue = ResolveAdaptationTarget(_config.heatAdaptedStateId, 80f);
            }

            _genes.ApplyEnvironmentPressure(
                suppression, mutationChance,
                _config.environmentGeneId, targetValue);
        }

        private float ResolveAdaptationTarget(string stateId, float fallback)
        {
            var def = GeneDatabase.Get(_config.environmentGeneId);
            if (def != null)
            {
                foreach (var state in def.States)
                {
                    if (state.StateId == stateId) return state.Center;
                }
            }
            return fallback;
        }

        private void FindPlanetBuilder()
        {
            if (_planetBuilder != null) return;
            var planet = GameObject.Find("Planet");
            if (planet != null) _planetBuilder = planet.GetComponent<PlanetTerrainMeshBuilder>();
        }

        private float GetEnvironmentResistance()
        {
            if (_genes == null) return 0f;
            return _genes.GetAttribute(_config.environmentResistanceAttribute);
        }

        /// <summary>
        /// 耐寒基因在寒冷环境发挥 100%，在炎热环境只剩配置比例；耐热基因反之。
        /// 没有适应基因时返回 0（没有可用的抵抗方向）。
        /// </summary>
        private float GetAdaptationStateFactor(bool isColdHazard)
        {
            if (_genes == null || _genes.Genome == null) return 0f;

            bool coldAdapted = false;
            bool heatAdapted = false;

            foreach (var gene in _genes.Genome.Genes)
            {
                if (gene == null || !gene.Enabled) continue;
                var def = GeneDatabase.Get(gene.DefinitionId);
                if (def == null) continue;
                var state = def.ResolveState(gene.Value, gene.MissingStateIds);
                if (state == null) continue;

                if (state.StateId == _config.coldAdaptedStateId) coldAdapted = true;
                else if (state.StateId == _config.heatAdaptedStateId) heatAdapted = true;
            }

            if (isColdHazard)
            {
                if (coldAdapted) return 1f;
                if (heatAdapted) return _config.mismatchedResistanceFactor;
                return 0f;
            }

            if (heatAdapted) return 1f;
            if (coldAdapted) return _config.mismatchedResistanceFactor;
            return 0f;
        }

        /// <summary>供基因面板显示当前环境压力，避免玩家只能看到温度却不知道是否受伤。</summary>
        public string GetStatusLine()
        {
            float suppression = _genes != null ? _genes.EnvironmentGeneSuppression : 0f;
            string pressure = suppression > 0.5f
                ? $" | <color=#FF6B6B>适应缺口 {suppression:F0}（所有基因受抑）</color>"
                : "";

            if (IsDrowning)
                return $"<color=#FF6B6B>溺水 ({_lastDrowningSeverity * 100f:F0}%) | 温度 {CurrentTemperature:F1}°C</color>{pressure}";
            if (_isColdHazard)
                return $"<color=#6EC6FF>寒冷 ({_lastTemperatureSeverity * 100f:F0}%) | 温度 {CurrentTemperature:F1}°C</color>{pressure}";
            if (_isHeatHazard)
                return $"<color=#FF9A5C>酷热 ({_lastTemperatureSeverity * 100f:F0}%) | 温度 {CurrentTemperature:F1}°C</color>{pressure}";
            return $"舒适 | 温度 {CurrentTemperature:F1}°C | 湿度 {CurrentHumidity:F1}%";
        }
    }
}
