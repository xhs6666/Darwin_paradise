using System;
using UnityEngine;

namespace DarwinPark.Environment
{
    /// <summary>
    /// 星球环境配置（Resources/Environment/environment_default.json 加载）。
    /// ★ 修改：环境伤害阈值与数值全部数据化，避免在实体脚本里写死温度/溺水伤害。
    /// </summary>
    [Serializable]
    public class EnvironmentConfig
    {
        public string id = "environment_default";

        [Header("全年平均气候基准")]
        public float baseTemperature = 20f;
        public float baseHumidity = 44f;
        public float temperatureLapseRate = 0.05f;
        public float humidityFalloff = 0.02f;
        public float equatorHeatBonus = 15f;
        public float planetRadiusForLapse = 120f;

        [Header("温度舒适区与伤害")]
        [Tooltip("低于该温度开始受寒")]
        public float coldThreshold = 5f;
        [Tooltip("高于该温度开始受热")]
        public float heatThreshold = 32f;
        [Tooltip("温度超出舒适区多少度时环境伤害达到最大倍率")]
        public float severityReferenceDelta = 20f;
        [Tooltip("环境压力下每秒损失的饱食度（严重度为 1 时）")]
        public float temperatureNutrientDrainPerSecond = 1.5f;
        [Tooltip("环境压力下每秒损失的生命值（严重度为 1 时）")]
        public float temperatureHealthDamagePerSecond = 1.2f;
        [Tooltip("环境抵抗力最多能抵消多少比例的温度伤害（0~1）")]
        [Range(0f, 1f)] public float maxEnvironmentResistance = 0.8f;
        [Tooltip("基因状态与环境不匹配时，抵抗力只剩多少比例（0~1）")]
        [Range(0f, 1f)] public float mismatchedResistanceFactor = 0.3f;
        [Tooltip("环境抵抗属性 id，由基因属性曲线提供")]
        public string environmentResistanceAttribute = "environmentResistance";
        [Tooltip("耐寒状态 id")]
        public string coldAdaptedStateId = "cold_adapted";
        [Tooltip("耐热状态 id")]
        public string heatAdaptedStateId = "heat_adapted";

        [Header("溺水")]
        [Tooltip("身体中心低于水面多少米开始溺水")]
        public float drowningDepthThreshold = 0.8f;
        [Tooltip("水深达到该值后溺水伤害达到最大倍率")]
        public float drowningSeverityDepth = 4f;
        [Tooltip("溺水时每秒损失的饱食度")]
        public float drowningNutrientDrainPerSecond = 3f;
        [Tooltip("溺水时每秒损失的生命值")]
        public float drowningHealthDamagePerSecond = 10f;

        [Header("环境选择压力")]
        [Tooltip("环境压力超过该阈值后，对所有基因产生抑制")]
        [Range(0f, 1f)] public float environmentStressThreshold = 0.45f;
        [Tooltip("适应缺口达到最大时，所有基因有效强度被扣除的上限")]
        public float maxGlobalGeneSuppression = 18f;
        [Tooltip("环境压力下，每次变异优先作用于环境适应基因的概率")]
        [Range(0f, 1f)] public float adaptationMutationChance = 0.85f;
        [Tooltip("环境适应基因定义 id")]
        public string environmentGeneId = "gene_environment";

        [Header("死亡")]
        [Tooltip("生命值低于该值时判定死亡")]
        public float deathHealthThreshold = 0f;
    }
}
