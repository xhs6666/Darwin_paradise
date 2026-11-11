using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DarwinPark.Core;
using DarwinPark.Entities.Ecology;
using DarwinPark.Genetics;
using UnityEngine;

namespace DarwinPark.Entities
{
    public enum RandomSpawnMethod {Template,Genes}
    // ★ 新增：模板复制与基因约束随机是互斥来源；条件按携带判定，不把禁用/抑制误当成基因缺失。
    [Serializable] public sealed class RandomSpawnRecipe
    {
        public RandomSpawnMethod method;public string name="默认实体";public Genome template;
        public List<string> included=new(),excluded=new();
        public static Genome DefaultGenome()
        {
            var asset=Resources.Load<TextAsset>("Entities/entity_default");if(asset==null)throw new InvalidDataException("缺少 Entities/entity_default");
            var data=JsonUtility.FromJson<EntityGeneData>(asset.text);var result=new Genome();foreach(var gene in data.genes)result.Add(GeneInstance.Create(gene.definitionId,gene.value,gene.tier));return result;
        }
        public static Genome Clone(Genome source)
        {if(source==null)return DefaultGenome();var result=JsonUtility.FromJson<Genome>(JsonUtility.ToJson(source));foreach(var gene in result.Genes)gene.InstanceId=Guid.NewGuid().ToString("N");return result;}
        public Genome CreateGenome()
        {
            if(method==RandomSpawnMethod.Template)return Clone(template);
            var result=new Genome();var settings=PopulationMaintenanceSettings.Current;
            var candidates=GeneDatabase.All.Where(d=>!excluded.Contains(d.Id)&&!included.Contains(d.Id)).OrderBy(_=>UnityEngine.Random.value).ToList();
            foreach(string id in included)result.Add(GeneInstance.Create(id,UnityEngine.Random.Range(settings.minimumGeneValue,settings.maximumGeneValue),GeneDatabase.Get(id).Tier));
            int extra=UnityEngine.Random.Range(settings.minimumAdditionalGenes,settings.maximumAdditionalGenes+1);if(result.Genes.Count==0)extra=Mathf.Max(1,extra);
            foreach(var definition in candidates.Take(Mathf.Min(extra,settings.maximumGenomeGenes-result.Genes.Count)))result.Add(GeneInstance.Create(definition.Id,UnityEngine.Random.Range(settings.minimumGeneValue,settings.maximumGeneValue),definition.Tier));return result;
        }
        public bool Matches(Genome genome)
        {
            if(genome?.Genes==null)return false;
            // ★ 适配：优化重复计算，匹配是繁殖/数量扫描热路径；显式循环保持携带条件，避免每次All/Any产生闭包和枚举对象。
            if(method==RandomSpawnMethod.Genes)
            {
                for(int i=0;i<included.Count;i++)if(!Carries(genome,included[i]))return false;
                for(int i=0;i<excluded.Count;i++)if(Carries(genome,excluded[i]))return false;
                return true;
            }
            var expected=template??DefaultGenome();if(expected.Genes.Count!=genome.Genes.Count)return false;
            // 复制基因同样比较重数，模板实例ID不参与统计，出生生成的实例具有各自身份。
            foreach(var gene in expected.Genes)
            {
                int own=0,actual=0;foreach(var other in expected.Genes)if(Same(gene,other))own++;foreach(var other in genome.Genes)if(Same(gene,other))actual++;if(own!=actual)return false;
            }
            return true;
        }
        private static bool Carries(Genome genome,string id)
        {for(int i=0;i<genome.Genes.Count;i++)if(genome.Genes[i].DefinitionId==id)return true;return false;}
        // ★ 修复（防Bug回归）：数量扫描不为每个实体/基因拼接浮点字符串，避免维持模板带来持续GC。
        private static bool Same(GeneInstance a,GeneInstance b)=>a.DefinitionId==b.DefinitionId&&a.Value==b.Value&&a.Tier==b.Tier&&a.Enabled==b.Enabled&&a.CopyStrength==b.CopyStrength&&a.MissingStateIds.SequenceEqual(b.MissingStateIds);
        public void Validate(ICollection<string> catalog)
        {
            if(!Enum.IsDefined(typeof(RandomSpawnMethod),method)||string.IsNullOrWhiteSpace(name)||name.Length>128||included==null||excluded==null||included.Count!=included.Distinct().Count()||excluded.Count!=excluded.Distinct().Count()||included.Intersect(excluded).Any()||included.Concat(excluded).Any(id=>!catalog.Contains(id)))throw new InvalidDataException("随机生成来源或基因条件无效");
            if(method==RandomSpawnMethod.Template)
            {if(template==null)template=DefaultGenome();var genes=template.Genes;if(genes==null||genes.Count==0||genes.Count>PopulationMaintenanceSettings.Current.maximumGenomeGenes||genes.Any(g=>g==null||!catalog.Contains(g.DefinitionId)||!float.IsFinite(g.Value)||g.Value<0||g.Value>100||!float.IsFinite(g.CopyStrength)||g.CopyStrength<0||g.MissingStateIds==null))throw new InvalidDataException("随机生成模板基因无效");}
            else if(included.Count>PopulationMaintenanceSettings.Current.maximumGenomeGenes||catalog.All(excluded.Contains))throw new InvalidDataException("基因条件没有可生成的基因或必选基因过多");
        }
    }
    [Serializable] public sealed class PopulationMaintenanceRule
    {public string id;public bool enabled=true,landOnly=true;public float ratio=.3f;public RandomSpawnRecipe source=new();
        // ★ 适配：普通来源规则保留旧语义；默认生态规则按实际能力统计，并可设置保底数量。
        public PopulationRuleCategory category;public int minimumCount;}
    [Serializable] public sealed class PopulationMaintenanceState
    {public int version=1;public List<PopulationMaintenanceRule> rules=new();}
    [Serializable] public sealed class PopulationMaintenanceSettings
    {
        public int maximumRules=8,maximumRefillPerCycle=12,emptyWorldSeedCount=10,maximumGenomeGenes=32,minimumAdditionalGenes=1,maximumAdditionalGenes=5;
        public float pollGameSeconds=1,extraBreedingDelaySeconds=30,minimumGeneValue=1,maximumGeneValue=100;
        private static PopulationMaintenanceSettings _current;
        public static PopulationMaintenanceSettings Current
        {
            get
            {
                if(_current!=null)return _current;var asset=Resources.Load<TextAsset>("Entities/population_maintenance");if(asset==null)throw new InvalidDataException("缺少 Entities/population_maintenance");var value=JsonUtility.FromJson<PopulationMaintenanceSettings>(asset.text);
                if(value==null||value.maximumRules<1||value.maximumRules>16||value.maximumRefillPerCycle<1||value.emptyWorldSeedCount<1||value.maximumGenomeGenes<1||value.minimumAdditionalGenes<0||value.maximumAdditionalGenes<value.minimumAdditionalGenes||!float.IsFinite(value.pollGameSeconds)||value.pollGameSeconds<=0||!float.IsFinite(value.extraBreedingDelaySeconds)||value.extraBreedingDelaySeconds<=0||!float.IsFinite(value.minimumGeneValue)||!float.IsFinite(value.maximumGeneValue)||value.minimumGeneValue<0||value.maximumGeneValue>100||value.maximumGeneValue<value.minimumGeneValue)throw new InvalidDataException("数量维持配置无效");return _current=value;
            }
        }
    }
    // ★ 新增：比例是世界数据；总规则默认关闭，启用后只用额外繁殖间隔调节超额类别，不直接补员。
    public static class PopulationMaintenance
    {
        private static readonly List<PopulationMaintenanceRule> Items=new();private static readonly Dictionary<string,int> Counts=new();private static HashSet<string> RoundedPartitions=new();private static int _frame=-1;private static long _membership=-1;
        public static IReadOnlyList<PopulationMaintenanceRule> Rules=>Items;public static int Total {get;private set;}
        public static void Refresh()
        {
            // ★ 修复（防Bug回归）：同帧移除再新增可保持总数不变，必须按注册版本失效，而非只比较数量。
            if(_frame==Time.frameCount&&_membership==EntityRegistry.MembershipRevision)return;_frame=Time.frameCount;_membership=EntityRegistry.MembershipRevision;Total=0;EcologyMaintenancePolicy.ResetCounts();Counts.Clear();foreach(var rule in Items)Counts[rule.id]=0;
            // ★ 适配：普通来源规则仍走原轻量匹配，只有生态分类规则存在时才计算实际生产/移动能力。
            bool ecological=Items.Any(r=>r.category!=PopulationRuleCategory.Source);
            foreach(var entity in EntityRegistry.All)if(EcologyQueries.Alive(entity)){Total++;var category=ecological?EcologyMaintenancePolicy.Observe(entity.Genes):PopulationRuleCategory.Source;foreach(var rule in Items)if(EcologyMaintenancePolicy.Matches(rule,entity.Genes,category))Counts[rule.id]++;}
        }
        public static int Count(PopulationMaintenanceRule rule){Refresh();return Counts.TryGetValue(rule.id,out int count)?count:0;}
        // ★ 适配：生态70/30共享“生产者+移动者”分母，其他静止异养个体不导致两个分区无解。
        public static int Denominator(PopulationMaintenanceRule rule){Refresh();return EcologyMaintenancePolicy.Denominator(rule,Total);}
        public static int Needed(PopulationMaintenanceRule rule)
        {
            // ★ 适配：优化重复计算，停用/零目标规则无需先统计全世界。
            if(!rule.enabled||rule.ratio<=0||rule.ratio>=1)return 0;int count=Count(rule),total=Denominator(rule);
            if(count<rule.minimumCount)return Math.Min(rule.minimumCount-count,PopulationMaintenanceSettings.Current.maximumRefillPerCycle);
            if(total==0)return Math.Min(PopulationMaintenanceSettings.Current.emptyWorldSeedCount,PopulationMaintenanceSettings.Current.maximumRefillPerCycle);if(count>=total*rule.ratio)return 0;
            // 新实体同时增加分子和分母，不能用ratio*当前总数直接补差。
            return (int)Math.Min(PopulationMaintenanceSettings.Current.maximumRefillPerCycle,Math.Max(0,Math.Ceiling(((double)rule.ratio*total-count)/(1d-rule.ratio))));
        }
        private static bool Disjoint(RandomSpawnRecipe a,RandomSpawnRecipe b)
        {
            if(a.method==RandomSpawnMethod.Template)return !b.Matches(a.template);
            if(b.method==RandomSpawnMethod.Template)return !a.Matches(b.template);
            return a.included.Any(b.excluded.Contains)||b.included.Any(a.excluded.Contains);
        }
        public static bool RequiresRefill(PopulationMaintenanceRule rule)
        {
            if(Needed(rule)==0)return false;int count=Count(rule);if(count==0)return true;
            // ★ 修复（防Bug回归）：完整互斥分区按一个实体的精度维持，避免33.3%/66.7%为追逐分母而交替无限补充。
            return count<rule.minimumCount||!RoundedPartitions.Contains(rule.id)||rule.ratio*Denominator(rule)-count>=1;
        }
        public static float BreedingScale(EntityGeneController genes)
        {
            // ★ 适配：把比例规则检查从孕育总项细分出来，正常世界不可在测试中关闭。
            using var performance=PerformanceTimings.Measure(PerformanceTimings.Area.BreedingMaintenance);
            // ★ 适配：优化重复计算，保留停用目标供界面查看，总规则关闭时也不扫描全体实体。
            // ★ 适配：世界目标配置与本机规则开关分开，旧档不隐式授权繁殖干预。
            if(!GameplayTuning.Current.populationRatioControlEnabled)return 1;
            bool active=false;for(int i=0;i<Items.Count;i++)if(Items[i].enabled){active=true;break;}if(!active)return 1;Refresh();float factor=1;
            foreach(var rule in Items)if(rule.enabled&&EcologyMaintenancePolicy.Matches(rule,genes)&&Denominator(rule)>0){float share=Counts[rule.id]/(float)Denominator(rule);if(share>rule.ratio)factor=Mathf.Min(factor,rule.ratio/share);}return factor;
        }
        public static bool BreedingReady(EntityGeneController genes,ref float waited,float dt)
        {
            float scale=BreedingScale(genes);if(scale>=1){waited=0;return true;}if(scale<=0){waited=0;return false;}
            float interval=PopulationMaintenanceSettings.Current.extraBreedingDelaySeconds;
            // ★ 适配：自然年限下按代际间隔抑制超额类别，不能让几模拟秒的等待失去实际作用。
            if(NaturalPopulationPace.Current.enabled)interval=Mathf.Max(interval,NaturalPopulationPace.Current.postpartumYears*365*LifeCycleSettings.Load().secondsPerDay);
            float delay=interval*(1/scale-1);waited=Mathf.Min(delay,waited+Mathf.Max(0,dt));return waited>=delay;
        }
        public static bool Set(PopulationMaintenanceRule rule)
        {
            if(WorldAuthority.IsReplica||!WorldAuthority.CanEdit||rule==null)return false;
            var next=Capture();int index=next.rules.FindIndex(r=>r.id==rule.id);if(index<0)next.rules.Add(rule);else next.rules[index]=rule;
            try{Validate(next,GeneDatabase.All.Select(d=>d.Id).ToArray());}catch(InvalidDataException){return false;}Restore(next);return true;
        }
        public static bool Remove(string id){if(WorldAuthority.IsReplica||!WorldAuthority.CanEdit)return false;int removed=Items.RemoveAll(r=>r.id==id);RoundedPartitions=CheckPartitions(Items);_frame=-1;return removed>0;}
        public static void SuspendAll(){foreach(var rule in Items)rule.enabled=false;RoundedPartitions.Clear();_frame=-1;}
        public static PopulationMaintenanceState Capture()=>JsonUtility.FromJson<PopulationMaintenanceState>(JsonUtility.ToJson(new PopulationMaintenanceState{rules=Items.ToList()}));
        public static void Validate(PopulationMaintenanceState state,ICollection<string> catalog)
        {
            if(state==null||state.version==0&&(state.rules==null||state.rules.Count==0))return;
            if(state.version!=1||state.rules==null||state.rules.Count>PopulationMaintenanceSettings.Current.maximumRules)throw new InvalidDataException("数量维持规则版本或数量无效");var ids=new HashSet<string>();
            foreach(var rule in state.rules){if(rule==null||!Enum.IsDefined(typeof(PopulationRuleCategory),rule.category)||rule.minimumCount<0||rule.minimumCount>100000||!Guid.TryParseExact(rule.id,"N",out _)||!ids.Add(rule.id)||!float.IsFinite(rule.ratio)||rule.ratio<0||rule.ratio>1||rule.source==null)throw new InvalidDataException("数量维持比例或来源无效（比例须为0到100%）");rule.source.Validate(catalog);}
            // ★ 修复（防Bug回归）：互斥条件的目标总和超过100%无解，必须在写盘/启动生成前拒绝，而非一直补到世界上限。
            CheckPartitions(state.rules);
        }
        private static HashSet<string> CheckPartitions(IEnumerable<PopulationMaintenanceRule> rules)
        {
            var result=new HashSet<string>();var enabled=rules.Where(r=>r.enabled).ToArray();var apart=new bool[enabled.Length,enabled.Length];for(int i=0;i<enabled.Length;i++)for(int j=0;j<i;j++)apart[i,j]=EcologyMaintenancePolicy.Disjoint(enabled[i],enabled[j],Disjoint);
            for(int mask=1;mask<(1<<enabled.Length);mask++){double sum=0;bool clique=true;for(int i=0;i<enabled.Length&&clique;i++)if((mask&(1<<i))!=0){sum+=enabled[i].ratio;for(int j=0;j<i;j++)if((mask&(1<<j))!=0&&!apart[i,j]){clique=false;break;}}if(!clique)continue;if(sum>1.000001)throw new InvalidDataException("互斥维持规则的目标比例总和不能超过100%");if(Math.Abs(sum-1)<.000001)for(int i=0;i<enabled.Length;i++)if((mask&(1<<i))!=0)result.Add(enabled[i].id);}
            return result;
        }
        public static void Restore(PopulationMaintenanceState state)
        {var incoming=state?.rules!=null?JsonUtility.FromJson<PopulationMaintenanceState>(JsonUtility.ToJson(state)).rules:new List<PopulationMaintenanceRule>();var partitions=CheckPartitions(incoming);Items.Clear();Items.AddRange(incoming);RoundedPartitions=partitions;_frame=-1;Counts.Clear();Total=0;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] private static void Reset()=>Restore(null);
    }
}
