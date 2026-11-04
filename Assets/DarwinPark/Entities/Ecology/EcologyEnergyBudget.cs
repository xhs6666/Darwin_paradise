using DarwinPark.Core;
using DarwinPark.Entities.Logic;
using DarwinPark.Entities.Stats;
using DarwinPark.Genetics;
using DarwinPark.Planet;
using UnityEngine;

namespace DarwinPark.Entities.Ecology
{
    // ★ 新增：生产只有一个入口，溢出统一入库；移动开销按实际切向速度与模拟倍率换算。
    public static class EcologyEnergyBudget
    {
        // ★ 修复（防Bug回归）：繁殖预算不足时提前储粮；储备充足后只在饥饿时觅食，避免吃光植物却堆满无用仓储。
        public static bool WantsFood(EntityGeneController genes,EntityStats stats,float hungerThreshold)
        {
            var settings=EcologyBalanceSettings.Current;
            if(!stats.TryGet(settings.nutrientAttribute,out float food))return false;
            if(food<hungerThreshold)return true;
            if(genes.CurrentAge==AgeState.Elder||food>=settings.reserveFeedingThreshold||!stats.TryGet(settings.reserveAttribute,out float reserve))return false;
            if(!stats.TryGet(settings.sizeAttribute,out float size))return false;
            if(reserve>=LifeCycleSettings.Load().nutrientCost*Mathf.Max(settings.minimumReproductionSize,size))return false;
            return genes.Evaluation.Genes.Exists(g=>g.IsActive&&genes.GetLogic(g.Gene) is ReproductionGeneLogic);
        }
        public static bool IsProducer(EntityGeneController genes)
        {
            var settings=EcologyBalanceSettings.Current;
            // ★ 修复（防Bug回归）：密集生态每步大量查询角色，只线性选主表达，避免为每个邻居创建 LINQ 排序集合。
            if(genes==null||genes.GetComponent<EggIncubation>()?.Incubating==true)return false;
            // ★ 适配：异养后代/食腐分支接管营养方式，不能同时借父基因的光合状态凭空生产。
            if(EcologyQueries.Active(genes,NutritionEcologySettings.Current.heterotrophyGene)!=null||EcologyQueries.Active(genes,NutritionEcologySettings.Current.scavengerGene)!=null)return false;
            GeneEvaluation gene=null;
            if(genes.Evaluation!=null)foreach(var candidate in genes.Evaluation.Genes)
                if(candidate.IsActive&&candidate.Definition.Id==settings.nutritionGene&&(gene==null||candidate.EffectiveStrength>gene.EffectiveStrength))gene=candidate;
            return gene?.State!=null&&System.Array.IndexOf(settings.producerStates,gene.State.StateId)>=0&&genes.GetAttribute(settings.movementAttribute)<=0;
        }
        public static float Competition(EntityGeneController genes)
        {
            // ★ 适配：按真实距离统计同一批生产者，帧内子步共用竞争结果，消除逐实体逐子步全图扫描。
            return EntityNeighborhood.Competition(genes);
        }
        public static void Produce(EntityGeneController genes,GeneInstance source,float dt)
        {
            if(dt<=0||!GeneBehaviorAuthority.IsPrimary<NutritionGeneLogic>(genes,source))return;
            if(EcologyQueries.Active(genes,NutritionEcologySettings.Current.heterotrophyGene)!=null||EcologyQueries.Active(genes,NutritionEcologySettings.Current.scavengerGene)!=null)return;
            var stats=genes.GetComponent<EntityStats>();if(stats==null)return;
            var state=genes.Evaluation?.Genes.Find(g=>g.Gene==source&&g.IsActive)?.State;
            var settings=EcologyBalanceSettings.Current;float efficiency=genes.GetAttribute(settings.absorptionAttribute);
            if(efficiency<=0||state==null)return;
            // ★ 适配：生产者分类已由帧共享快照计算，不能在每个子步再次遍历表达并读取同一组移动修饰组件。
            if(state.StateId==settings.photoState)stats.Feed(efficiency*settings.photoProductionRate*dt/EntityNeighborhood.ProductionCompetition(genes));
            else if(state.StateId==settings.chemicalState&&PlanetOreSystem.Instance!=null)
                stats.Feed(PlanetOreSystem.Instance.ConsumeOre(genes.transform.position,efficiency*settings.mineralExtractionRate*dt));
        }
        public static float Consumption(EntityGeneController genes,float baseRate,float movementRate)
        {
            var settings=EcologyBalanceSettings.Current;float speed=0;
            var body=genes.GetComponent<Rigidbody>();/* ★ 适配：与实际交付模拟时间一致。 */ float scale=ServiceLocator.TryGet<SimulationClock>(out var clock)?clock.MotionTimeScale:1;
            if(body!=null&&scale>0&&genes.GetAttribute(settings.movementAttribute)>0)
                speed=Mathf.Min(genes.GetAttribute(settings.movementAttribute),Vector3.ProjectOnPlane(body.linearVelocity,EcologyQueries.Up(genes.transform.position)).magnitude/scale);
            return baseRate*settings.baseMetabolismScale+movementRate*settings.movementMetabolismScale*speed;
        }
    }
}
