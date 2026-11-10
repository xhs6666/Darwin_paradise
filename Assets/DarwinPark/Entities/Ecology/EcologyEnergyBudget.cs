using DarwinPark.Entities.Simulation; // ★ 适配：同步模拟批内复用组件，不缓存实时状态数值。
using DarwinPark.Core;
using DarwinPark.Entities.Logic;
using DarwinPark.Entities.Stats;
using DarwinPark.Genetics;
using DarwinPark.Planet;
using UnityEngine;

// ★ 适配：降低获取速度时只扣实际转移量，避免营养凭空损失。
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
        // ★ 修复（防Bug回归）：比例规则和空间竞争共用已存在的表达分类；不能在每个繁殖检查重复遍历并走零速度修饰链。
        public static bool IsProducer(EntityGeneController genes)=>ProducerClassification.IsProducer(genes);
        public static float Competition(EntityGeneController genes)
        {
            // ★ 适配：按真实距离统计同一批生产者，帧内子步共用竞争结果，消除逐实体逐子步全图扫描。
            return EntityNeighborhood.Competition(genes);
        }
        public static void Produce(EntityGeneController genes,GeneInstance source,float dt)
        {
            if(dt<=0||!GeneBehaviorAuthority.IsPrimary<NutritionGeneLogic>(genes,source))return;
            // ★ 适配：遗传食性资格按表达版本复用，实际吸收和容量仍每次实时核算。
            if(Simulation.ActiveNutritionContext.Blocked(genes))return;
            var stats=ActiveSimulationBatch.Read<EntityStats>(genes);if(stats==null)return;
            // ★ 修复（防Bug回归）：使用重算时维护的实例索引，生产小步不再创建捕获闭包并扫描全部基因。
            var evaluation=genes.GetEvaluation(source);var state=evaluation?.IsActive==true?evaluation.State:null;
            var settings=EcologyBalanceSettings.Current;float efficiency=genes.GetAttribute(settings.absorptionAttribute);
            if(efficiency<=0||state==null)return;
            // ★ 适配：生产者分类已由帧共享快照计算，不能在每个子步再次遍历表达并读取同一组移动修饰组件。
            if(state.StateId==settings.photoState)stats.Feed(efficiency*settings.photoProductionRate*dt*GameplayTuning.Current.nutritionGainMultiplier/EntityNeighborhood.ProductionCompetition(genes));
            else if(state.StateId==settings.chemicalState&&PlanetOreSystem.Instance!=null)
                stats.Feed(PlanetOreSystem.Instance.ConsumeOre(genes.transform.position,efficiency*settings.mineralExtractionRate*dt*GameplayTuning.Current.nutritionGainMultiplier));
        }
        public static float Consumption(EntityGeneController genes,float baseRate,float movementRate)
        {
            var settings=EcologyBalanceSettings.Current;float speed=0;
            var body=ActiveSimulationBatch.Read<Rigidbody>(genes);/* ★ 适配：与实际交付模拟时间一致。 */ float scale=ServiceLocator.TryGet<SimulationClock>(out var clock)?clock.MotionTimeScale:1;
            float movement=body!=null&&scale>0?genes.GetAttribute(settings.movementAttribute):0;
            if(movement>0)speed=Mathf.Min(movement,Vector3.ProjectOnPlane(body.linearVelocity,EcologyQueries.Up(genes.transform.position)).magnitude/scale);
            // ★ 适配：全局可编辑倍率在最终收支入口应用，AI、手动和不同模拟倍率共用同一计算。
            var tuning=GameplayTuning.Current;
            return baseRate*settings.baseMetabolismScale*tuning.baseMetabolismMultiplier+movementRate*settings.movementMetabolismScale*speed*tuning.movementMetabolismMultiplier;
        }
    }
}
