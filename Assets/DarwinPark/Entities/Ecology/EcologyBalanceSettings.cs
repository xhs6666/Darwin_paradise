using System;
using System.IO;
using DarwinPark.Core;
using UnityEngine;

namespace DarwinPark.Entities.Ecology
{
    // ★ 新增：生态数值集中于配置并随世界保存，避免 Inspector 默认值与已有预制体数值互相覆盖。
    [Serializable] public sealed class EcologyBalanceSettings
    {
        public int version;
        public string sizeAttribute,nutrientAttribute,reserveAttribute,healthAttribute,movementAttribute,nutritionGene;
        public string[] producerStates;
        public float maximumPreySizeRatio,minimumSize,baseMetabolismScale,movementMetabolismScale;
        public float photoProductionRate,mineralExtractionRate,competitionRadius,competitionPerNeighbor;
        public string photoState,chemicalState,absorptionAttribute,reproductionSpeedAttribute,gestationEfficiencyAttribute;
        public float reserveFeedingThreshold,minimumReproductionSize;
        public float consumerFoodCompetitionRadius,consumersPerProducer,consumerFoodPressureStrength;
        public float producerGestationDays,consumerGestationDays,referenceReproductionSpeed,minimumReproductionSpeed;
        public float producerCooldownDays,consumerCooldownDays,gestationFoodPerSecond,birthFoodCost;
        private static EcologyBalanceSettings _current;private static string _json;
        public static string ConfiguredPath=>Path.Combine(GamePaths.Mods,"Default","ecology_balance.json");
        public static string ReadConfiguredJson()
        {
            if(File.Exists(ConfiguredPath))return File.ReadAllText(ConfiguredPath);
            var asset=Resources.Load<TextAsset>("Entities/ecology_balance");
            if(asset==null)throw new InvalidDataException("缺少 Entities/ecology_balance");return asset.text;
        }
        public static EcologyBalanceSettings Current {get {if(_current==null)UseSnapshot(null);return _current;}}
        public static string CurrentJson {get {_=Current;return _json;}}
        public static EcologyBalanceSettings Parse(string json)
        {
            var value=JsonUtility.FromJson<EcologyBalanceSettings>(json);
            if(value==null||value.version!=1||value.producerStates==null||value.producerStates.Length==0)throw new InvalidDataException("生态平衡配置格式无效");
            foreach(string text in new[]{value.sizeAttribute,value.nutrientAttribute,value.reserveAttribute,value.healthAttribute,value.movementAttribute,value.nutritionGene,value.photoState,value.chemicalState,value.absorptionAttribute,value.reproductionSpeedAttribute,value.gestationEfficiencyAttribute})
                if(string.IsNullOrWhiteSpace(text))throw new InvalidDataException("生态平衡配置缺少属性或基因 ID");
            foreach(float number in new[]{value.maximumPreySizeRatio,value.minimumSize,value.baseMetabolismScale,value.movementMetabolismScale,value.photoProductionRate,value.mineralExtractionRate,value.competitionRadius,value.competitionPerNeighbor,value.producerGestationDays,value.consumerGestationDays,value.referenceReproductionSpeed,value.minimumReproductionSpeed,value.producerCooldownDays,value.consumerCooldownDays,value.gestationFoodPerSecond,value.birthFoodCost,value.reserveFeedingThreshold,value.minimumReproductionSize,value.consumerFoodCompetitionRadius,value.consumersPerProducer,value.consumerFoodPressureStrength})
                if(float.IsNaN(number)||float.IsInfinity(number)||number<0)throw new InvalidDataException("生态平衡配置必须为非负有限数值");
            if(value.maximumPreySizeRatio<=0||value.maximumPreySizeRatio>1||value.minimumSize<=0||value.competitionRadius<=0||value.referenceReproductionSpeed<=0||value.minimumReproductionSpeed<=0||value.minimumReproductionSize<=0||value.reserveFeedingThreshold<=0||value.consumerFoodCompetitionRadius<=0||value.consumersPerProducer<=0)throw new InvalidDataException("体型比例须在 (0,1]，体型/半径/速度参照/储粮阈值/食物比例须为正数");
            return value;
        }
        // ★ 修复（防Bug回归）：先完整解析再替换，损坏快照不能留下旧数值配新 JSON 的半更新状态。
        public static void UseSnapshot(string json){string raw=string.IsNullOrWhiteSpace(json)?ReadConfiguredJson():json;var parsed=Parse(raw);_current=parsed;_json=raw;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] public static void Clear(){_current=null;_json=null;}
    }
}
