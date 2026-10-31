using System;
using System.IO;
using System.Linq;
using DarwinPark.Core;
using UnityEngine;

namespace DarwinPark.Entities.Ecology
{
    public enum AntRole { None, Soldier, Worker, Reproductive, Queen, Mate }
    [Serializable] public sealed class AntRoleRule
    {
        public string role,label;public float birthWeight,attackMultiplier,moveMultiplier;
        public bool frozenGenome;public string[] suppressedGenes;
        public AntRole Role=>(AntRole)Enum.Parse(typeof(AntRole),role);
    }
    // ★ 新增：蚁群规则随世界快照保存；角色比例、巡逻与运输参数均由配置控制。
    [Serializable] public sealed class AntColonySettings
    {
        public int version,supplyPointCount,maximumReproductives,minimumReproductives,reproductivePopulationThreshold,circleSegments;
        public AntRoleRule[] roles;
        public bool patrolRetaliationOnly=true;
        public float patrolDuration,patrolRestMin,patrolRestMax,retaliationSeconds,scanInterval;
        public float colonyRadius,minimumColonyRadius,soldierPatrolRadiusRatio,soldierPatrolSeconds,soldierRestSeconds;
        public float arrivalDistance,transportSearchRadius,transportReach,transportOffset,nurseryDeliveryFraction;
        public float biomassPerSize,minimumBiomass,queenFeedThreshold,memberFeedThreshold,feedRate,feedRadius,queenReserveTarget;
        public float queenGestationSeconds,queenCooldownSeconds,queenBirthCost,mateCost,foundingDistanceRatio;
        public float energyBlockSize,glowIntensity,glowRange,overlayWidth;
        public float geneSuppression;
        public string energyColor;
        private static AntColonySettings _current;private static string _json;
        public static AntColonySettings Current {get{if(_current==null)UseSnapshot(null);return _current;}}
        public static string CurrentJson {get{_=Current;return _json;}}
        public static string ReadConfiguredJson()
        {
            string path=Path.Combine(GamePaths.Mods,"Default","ant_colony.json");
            if(File.Exists(path))return File.ReadAllText(path);
            var text=Resources.Load<TextAsset>("Entities/ant_colony");
            if(text==null)throw new InvalidDataException("缺少 Entities/ant_colony");return text.text;
        }
        public static AntColonySettings Parse(string json)
        {
            var value=JsonUtility.FromJson<AntColonySettings>(json);
            if(value==null||value.version!=1||value.roles==null||value.roles.Length!=5)throw new InvalidDataException("蚁群配置版本或分工无效");
            var ids=value.roles.Select(r=>r.Role).ToArray();
            if(ids.Distinct().Count()!=5||ids.Contains(AntRole.None)||ids.Any(id=>!Enum.IsDefined(typeof(AntRole),id)))throw new InvalidDataException("蚁群角色重复或缺失");
            foreach(var rule in value.roles)
                if(string.IsNullOrWhiteSpace(rule.label)||rule.suppressedGenes==null||new[]{rule.birthWeight,rule.attackMultiplier,rule.moveMultiplier}.Any(n=>float.IsNaN(n)||float.IsInfinity(n)||n<0))
                    throw new InvalidDataException("蚁群角色参数无效："+rule.role);
            foreach(var field in typeof(AntColonySettings).GetFields().Where(f=>f.FieldType==typeof(float)))
            {float number=(float)field.GetValue(value);if(float.IsNaN(number)||float.IsInfinity(number)||number<0)throw new InvalidDataException("蚁群参数必须为非负有限值："+field.Name);}
            if(value.supplyPointCount<1||value.minimumReproductives<0||value.maximumReproductives<value.minimumReproductives||value.reproductivePopulationThreshold<1||value.geneSuppression<=0||value.circleSegments<12||value.scanInterval<=0||value.arrivalDistance<=0||value.colonyRadius<=0||value.minimumColonyRadius<=0||value.queenGestationSeconds<=0||value.feedRate<=0||value.feedRadius<=0||value.patrolRestMax<value.patrolRestMin||value.nurseryDeliveryFraction>1||value.foundingDistanceRatio<=1||value.roles.Where(r=>r.Role==AntRole.Soldier||r.Role==AntRole.Worker).Sum(r=>r.birthWeight)<=0||!ColorUtility.TryParseHtmlString(value.energyColor,out _))
                throw new InvalidDataException("蚁群半径、计时、比例、分工权重或颜色配置无效");
            return value;
        }
        private System.Collections.Generic.Dictionary<AntRole,AntRoleRule> _roleRules;
        public AntRoleRule Rule(AntRole role)
        {
            // ★ 修复（防Bug回归）：角色倍率是属性热路径，配置加载后只构建一次索引，不反复解析角色文本和枚举数组。
            if(_roleRules==null){_roleRules=new System.Collections.Generic.Dictionary<AntRole,AntRoleRule>();foreach(var rule in roles)_roleRules.Add(rule.Role,rule);}
            return _roleRules[role];
        }
        public static void UseSnapshot(string raw){raw=string.IsNullOrWhiteSpace(raw)?ReadConfiguredJson():raw;var parsed=Parse(raw);_current=parsed;_json=raw;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] public static void Clear(){_current=null;_json=null;}
    }
}
