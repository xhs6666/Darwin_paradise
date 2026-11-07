using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DarwinPark.Core;
using DarwinPark.Genetics;
using UnityEngine;

namespace DarwinPark.Entities.Ecology
{
    [Serializable] public sealed class EcologyDefaultGene {public string id;public float value;public int tier;}
    [Serializable] public sealed class EcologyDefaultTemplate
    {
        public string id,name;public EcologyDefaultGene[] genes;
        public RandomSpawnRecipe Recipe()
        {
            var genome=new Genome();foreach(var gene in genes)genome.Add(GeneInstance.Create(gene.id,gene.value,gene.tier));
            return new RandomSpawnRecipe{method=RandomSpawnMethod.Template,name=name,template=genome};
        }
    }
    [Serializable] public sealed class EcologyDefaultsSettings
    {
        public int version,minimumPopulation,minimumWorkers,localProducerTarget,maximumFoundingMembers;
        public float producerRatio,mobileRatio,foundingFood,plantSpacing;
        public bool enableNewWorldRules,enableFoundingSupport;
        public string producerTemplate,mobileTemplate;public EcologyDefaultTemplate[] templates;
    }
    // ★ 新增：生态默认项是可编辑配置和世界快照；恢复旧世界不擅自覆盖其自定义维持规则。
    public static class EcologyDefaults
    {
        public const string ProducerRuleId="bd05a85fb9a740ac80a06eed1017da70",MobileRuleId="a982a1bc88ad45a5b5072f61421dcc30";
        private static EcologyDefaultsSettings _current;private static string _json;
        public static string ConfigurationPath=>Path.Combine(GamePaths.Root,"config","ecology_defaults.json");
        public static EcologyDefaultsSettings Current {get{if(_current==null)UseSnapshot(null);return _current;}}
        public static string CurrentJson {get{_=Current;return _json;}}
        public static string ReadConfiguredJson()
        {
            if(File.Exists(ConfigurationPath))return File.ReadAllText(ConfigurationPath);
            var asset=Resources.Load<TextAsset>("Entities/ecology_defaults");if(asset==null)throw new InvalidDataException("缺少 Entities/ecology_defaults");
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigurationPath));File.WriteAllText(ConfigurationPath,asset.text,new UTF8Encoding(false));return asset.text;
        }
        public static EcologyDefaultsSettings Parse(string json)
        {
            var value=JsonUtility.FromJson<EcologyDefaultsSettings>(CommentedJson.Strip(json));
            if(value==null||value.version!=1||value.minimumPopulation<1||value.minimumPopulation>100000||value.minimumWorkers<0||value.minimumWorkers>16||value.localProducerTarget<0||value.localProducerTarget>128||value.maximumFoundingMembers<1||!Finite(value.producerRatio)||!Finite(value.mobileRatio)||value.producerRatio<=0||value.mobileRatio<=0||Mathf.Abs(value.producerRatio+value.mobileRatio-1)>.00001f||!Finite(value.foundingFood)||value.foundingFood<0||!Finite(value.plantSpacing)||value.plantSpacing<=0||value.templates==null||value.templates.Length==0)throw new InvalidDataException("生态默认比例、创业储粮或数量配置无效");
            var ids=new HashSet<string>();foreach(var template in value.templates)
            {
                if(template==null||string.IsNullOrWhiteSpace(template.id)||!ids.Add(template.id)||string.IsNullOrWhiteSpace(template.name)||template.genes==null||template.genes.Length==0||template.genes.Length>PopulationMaintenanceSettings.Current.maximumGenomeGenes)throw new InvalidDataException("生态默认模板缺失或重复");
                var genes=new HashSet<string>();foreach(var gene in template.genes)if(gene==null||string.IsNullOrWhiteSpace(gene.id)||!genes.Add(gene.id)||!Finite(gene.value)||gene.value<0||gene.value>100||gene.tier<0)throw new InvalidDataException("生态默认模板基因无效");
            }
            if(!ids.Contains(value.producerTemplate)||!ids.Contains(value.mobileTemplate))throw new InvalidDataException("生态默认比例引用未知模板");return value;
        }
        private static bool Finite(float value)=>float.IsFinite(value);
        // ★ 新增：旧档缺配置时先校验并固定一次外部默认，不能清场后才读取一份可能无效的配置。
        public static ColonyWorldState PrepareSnapshot(ColonyWorldState state)
        {
            if(state==null||state.IsEmpty)state=new ColonyWorldState();
            if(string.IsNullOrWhiteSpace(state.ecologyDefaults))state.ecologyDefaults=ReadConfiguredJson();
            Parse(state.ecologyDefaults);return state;
        }
        public static void ValidateCatalog(string json,ICollection<string> catalog)
        {
            if(string.IsNullOrWhiteSpace(json))return;var settings=Parse(json);
            foreach(var template in settings.templates)foreach(var gene in template.genes)if(!catalog.Contains(gene.id))throw new InvalidDataException("生态默认模板引用未知基因："+gene.id);
        }
        public static void UseSnapshot(string json){json=string.IsNullOrWhiteSpace(json)?ReadConfiguredJson():json;var parsed=Parse(json);_current=parsed;_json=json;}
        public static RandomSpawnRecipe Template(string id)=>Current.templates.Single(t=>t.id==id).Recipe();
        public static PopulationMaintenanceState CreateRules(string json=null,bool force=false)
        {
            var settings=json==null?Current:Parse(json);var result=new PopulationMaintenanceState();
            if(!settings.enableNewWorldRules&&!force)return result;
            result.rules.Add(new PopulationMaintenanceRule{id=ProducerRuleId,category=PopulationRuleCategory.Producers,ratio=settings.producerRatio,minimumCount=Mathf.CeilToInt(settings.minimumPopulation*settings.producerRatio),source=settings.templates.Single(t=>t.id==settings.producerTemplate).Recipe()});
            result.rules.Add(new PopulationMaintenanceRule{id=MobileRuleId,category=PopulationRuleCategory.Mobile,ratio=settings.mobileRatio,minimumCount=settings.minimumPopulation-result.rules[0].minimumCount,source=settings.templates.Single(t=>t.id==settings.mobileTemplate).Recipe()});return result;
        }
        // ★ 新增：旧世界从界面显式应用；预先验证整组后原子替换默认两行，保留其他用户规则，失败时不部分写入。
        public static bool ApplyRules()
        {
            if(WorldAuthority.IsReplica||!WorldAuthority.CanEdit)return false;
            try
            {
                var raw=ReadConfiguredJson();var catalog=GeneDatabase.All.Select(d=>d.Id).ToArray();ValidateCatalog(raw,catalog);
                var next=PopulationMaintenance.Capture();var defaults=CreateRules(raw,true);
                next.rules.RemoveAll(r=>r.id==ProducerRuleId||r.id==MobileRuleId);next.rules.AddRange(defaults.rules);
                PopulationMaintenance.Validate(next,catalog);UseSnapshot(raw);PopulationMaintenance.Restore(next);return true;
            }
            catch(Exception ex){Debug.LogWarning("[生态默认项] 未应用："+ex.Message);return false;}
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] private static void Reset(){_current=null;_json=null;}
    }
}
