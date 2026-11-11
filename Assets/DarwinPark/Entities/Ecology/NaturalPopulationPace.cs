using System;
using System.IO;
using DarwinPark.Core;
using UnityEngine;
namespace DarwinPark.Entities.Ecology
{
    // ★ 新增：少量实体的自然代际节奏；没有人口强制增长/补员，年限仅用于参考基因的标定。
    [Serializable]public sealed class NaturalPopulationSettings
    {
        public int version=1,initialPopulation=10;public bool enabled=true;
        public float maturityYears=5,elderYears=35,lifespanYears=45,referenceGrowthSpeed=1.8f,referenceAgingSpeed=1.8f;
        public float minimumGestationDays=15,maximumGestationDays=360,producerGestationDays=180,consumerGestationDays=270,queenGestationDays=90;
        public float postpartumYears=9,queenCooldownDays=180,workerMaturityDays=30;
        public string producerTemplate="producer",mobileTemplate="pioneer_mobile";
    }
    public static class NaturalPopulationPace
    {
        private static NaturalPopulationSettings _current;private static long _stamp;private static float _nextPoll;
        public static NaturalPopulationSettings Current{get{if(_current==null)Reload();return _current;}}
        public static string FilePath=>Path.Combine(GamePaths.Root,"config","natural_population.json");
        public static void Reload()
        {
            var previous=_current;
            try{Load();}
            catch(Exception ex){if(previous==null){var fallback=Resources.Load<TextAsset>("Entities/natural_population");if(fallback==null)throw;_current=Parse(fallback.text);}Debug.LogWarning("[自然种群] 配置无效，保留有效值："+ex.Message);}
        }
        public static void Poll()
        {if(Time.unscaledTime<_nextPoll)return;_nextPoll=Time.unscaledTime+2;try{long stamp=File.GetLastWriteTimeUtc(FilePath).Ticks;if(_current==null||stamp!=_stamp){Reload();_stamp=File.GetLastWriteTimeUtc(FilePath).Ticks;}}catch(Exception ex){Debug.LogWarning("[自然种群] 无法重新载入："+ex.Message);}}
        private static void Load()
        {
            var asset=Resources.Load<TextAsset>("Entities/natural_population");if(asset==null)throw new InvalidDataException("缺少natural_population");string raw=File.Exists(FilePath)?File.ReadAllText(FilePath):asset.text;var value=Parse(raw);
            _current=value;if(!File.Exists(FilePath)){Directory.CreateDirectory(Path.GetDirectoryName(FilePath));File.WriteAllText(FilePath,asset.text,new System.Text.UTF8Encoding(false));}
            _stamp=File.GetLastWriteTimeUtc(FilePath).Ticks;
        }
        public static NaturalPopulationSettings Parse(string raw)
        {
            var value=JsonUtility.FromJson<NaturalPopulationSettings>(CommentedJson.Strip(raw));
            if(value==null||value.version!=1||value.initialPopulation<2||value.initialPopulation>1000||value.maturityYears<=0||value.elderYears<=value.maturityYears||value.lifespanYears<=value.elderYears||value.minimumGestationDays<=0||value.maximumGestationDays<value.minimumGestationDays)throw new InvalidDataException("自然种群年限或初始数量无效");
            foreach(var field in typeof(NaturalPopulationSettings).GetFields())if(field.FieldType==typeof(float)){float n=(float)field.GetValue(value);if(!float.IsFinite(n)||n<=0)throw new InvalidDataException("自然种群参数须为正有限数："+field.Name);}
            if(string.IsNullOrWhiteSpace(value.producerTemplate)||string.IsNullOrWhiteSpace(value.mobileTemplate))throw new InvalidDataException("初始种源模板不能为空");
            return value;
        }
        public static float LifeSpeed(EntityGeneController genes)
        {
            var life=LifeCycleSettings.Load();var cfg=Current;float year=life.secondsPerDay*365;
            if(!cfg.enabled)return genes.CurrentAge==DarwinPark.Entities.AgeState.Elder?Mathf.Max(life.minimumAgingSpeed,genes.GetAttribute("agingSpeed"))*life.agingProgressScale:Mathf.Max(0,genes.GetAttribute("growthSpeed"))*life.growthProgressScale;
            float aging=Mathf.Max(life.minimumAgingSpeed,genes.GetAttribute("agingSpeed"))/cfg.referenceAgingSpeed;
            float growth=Mathf.Max(0,genes.GetAttribute("growthSpeed"))/cfg.referenceGrowthSpeed;var member=OptionalComponent.Read<AntColonyMember>(genes);float mature=member!=null&&member.Active&&member.Role==AntRole.Worker?cfg.workerMaturityDays/365:cfg.maturityYears;
            return genes.CurrentAge==DarwinPark.Entities.AgeState.Infant?life.adultProgress/(mature*year)*growth:genes.CurrentAge==DarwinPark.Entities.AgeState.Adult?(life.elderProgress-life.adultProgress)/((cfg.elderYears-mature)*year)*aging:(life.deathProgress-life.elderProgress)/((cfg.lifespanYears-cfg.elderYears)*year)*aging;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]private static void Reset(){_current=null;_stamp=0;_nextPoll=0;}
    }
}
