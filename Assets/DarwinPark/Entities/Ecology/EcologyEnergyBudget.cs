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
            GeneEvaluation gene=null;
            if(genes.Evaluation!=null)foreach(var candidate in genes.Evaluation.Genes)
                if(candidate.IsActive&&candidate.Definition.Id==settings.nutritionGene&&(gene==null||candidate.EffectiveStrength>gene.EffectiveStrength))gene=candidate;
            return gene?.State!=null&&System.Array.IndexOf(settings.producerStates,gene.State.StateId)>=0&&genes.GetAttribute(settings.movementAttribute)<=0;
        }
        public static float Competition(EntityGeneController genes)
        {
            var settings=EcologyBalanceSettings.Current;int count=0;Vector3 position=genes.transform.position;
            foreach(var entity in EntityRegistry.All)
                // ★ 修复（防Bug回归）：先用廉价距离筛选，避免远处每个实体都触发组件和基因查询，空间分散后仍能持续运行。
                if(entity!=null&&entity.gameObject!=genes.gameObject&&(entity.transform.position-position).sqrMagnitude<=settings.competitionRadius*settings.competitionRadius&&EcologyQueries.Alive(entity)&&IsProducer(entity.Genes))count++;
            return 1+count*settings.competitionPerNeighbor;
        }
        public static void Produce(EntityGeneController genes,GeneInstance source,float dt)
        {
            if(dt<=0||!GeneBehaviorAuthority.IsPrimary<NutritionGeneLogic>(genes,source))return;
            var stats=genes.GetComponent<EntityStats>();if(stats==null)return;
            var state=genes.Evaluation?.Genes.Find(g=>g.Gene==source&&g.IsActive)?.State;
            var settings=EcologyBalanceSettings.Current;float efficiency=genes.GetAttribute(settings.absorptionAttribute);
            if(efficiency<=0||state==null)return;
            if(state.StateId==settings.photoState)stats.Feed(efficiency*settings.photoProductionRate*dt/(IsProducer(genes)?Competition(genes):1));
            else if(state.StateId==settings.chemicalState&&PlanetOreSystem.Instance!=null)
                stats.Feed(PlanetOreSystem.Instance.ConsumeOre(genes.transform.position,efficiency*settings.mineralExtractionRate*dt));
        }
        public static float Consumption(EntityGeneController genes,float baseRate,float movementRate)
        {
            var settings=EcologyBalanceSettings.Current;float speed=0;
            var body=genes.GetComponent<Rigidbody>();float scale=ServiceLocator.TryGet<SimulationClock>(out var clock)?clock.TimeScale:1;
            if(body!=null&&scale>0&&genes.GetAttribute(settings.movementAttribute)>0)
                speed=Mathf.Min(genes.GetAttribute(settings.movementAttribute),Vector3.ProjectOnPlane(body.linearVelocity,EcologyQueries.Up(genes.transform.position)).magnitude/scale);
            return baseRate*settings.baseMetabolismScale+movementRate*settings.movementMetabolismScale*speed;
        }
    }
}
