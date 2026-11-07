using DarwinPark.Entities.Ecology;
using DarwinPark.Entities.Logic;
using DarwinPark.Entities.Stats;
using DarwinPark.Entities.Vitals;
using UnityEngine;

namespace DarwinPark.Entities.Simulation
{
    // ★ 新增：代表只计算可共享的生命/营养变化；有限矿产、捕食、搬运等有外部副作用的实体禁止进入组。
    public static class StationaryCohortKernel
    {
        public static bool Supported(EntityGeneController genes)
        {
            if(genes.Evaluation==null)return false;
            foreach(var evaluation in genes.Evaluation.Genes)
            {
                if(!evaluation.IsActive)continue;
                var logic=genes.GetLogic(evaluation.Gene);
                if(logic is NutritionGeneLogic)
                {if(evaluation.State?.StateId!=EcologyBalanceSettings.Current.photoState)return false;}
                else if(logic!=null&&logic is not GrowthGeneLogic&&logic is not RepairGeneLogic&&logic is not StorageGeneLogic&&logic is not EnvironmentAdaptationGeneLogic&&logic is not ReproductionGeneLogic)return false;
            }
            return true;
        }
        public static float LifeSpeed(EntityGeneController genes)
        {
            var settings=LifeCycleSettings.Load();
            return genes.CurrentAge==AgeState.Elder?Mathf.Max(settings.minimumAgingSpeed,genes.GetAttribute("agingSpeed"))*settings.agingProgressScale:Mathf.Max(0,genes.GetAttribute("growthSpeed"))*settings.growthProgressScale;
        }
        public static Vector3 Sample(EntityGeneController genes,float dt)
        {
            var stats=genes.GetComponent<EntityStats>();var before=Values(stats);
            // ★ 适配：冻结无意义的被动Update/感知，保留真正的生产与环境/代谢结算。
            foreach(var gene in genes.Genome.Genes)if(genes.GetLogic(gene) is NutritionGeneLogic nutrition)nutrition.OnUpdate(dt);
            AdvanceCore(genes,dt);
            return Values(stats)-before;
        }
        // ★ 修复（防Bug回归）：共享、拆组补算均尊重组件启停，不能把已关闭的环境/代谢重新运行。
        public static void AdvanceCore(EntityGeneController genes,float dt)
        {
            var environment=genes.GetComponent<EntityEnvironmentHazard>();if(environment!=null&&environment.isActiveAndEnabled)environment.AdvanceShared(dt);
            var vitals=genes.GetComponent<EntityVitals>();if(vitals!=null&&vitals.isActiveAndEnabled)vitals.AdvanceShared(dt);
        }
        public static Vector3 Values(EntityStats stats)
        {stats.TryGet("health",out var health);stats.TryGet("nutrient",out var food);stats.TryGet("storedNutrient",out var reserve);return new Vector3(health,food,reserve);}
        public static void Apply(EntityGeneController representative,EntityGeneController genes,Vector3 delta)
        {
            var stats=genes.GetComponent<EntityStats>();stats.Modify("health",delta.x,false);stats.Modify("nutrient",delta.y,false);stats.Modify("storedNutrient",delta.z,false);
            var source=representative.GetComponent<EntityEnvironmentHazard>();if(source!=null&&source.isActiveAndEnabled)genes.GetComponent<EntityEnvironmentHazard>()?.CopySharedReadings(source);
            if(stats.TryGet("health",out var health)&&health<=0)EntityDeathSettlement.Die(genes.GetComponent<DarwinEntity>());
        }
        public static void Reproduce(EntityGeneController genes,float dt)
        {
            // ★ 修复（防Bug回归）：复制逻辑可能在孕育/出生回调中重算基因，先保留当前逻辑引用再调用。
            ReproductionGeneLogic primary=null;
            foreach(var gene in genes.Genome.Genes)
                if(genes.GetLogic(gene) is ReproductionGeneLogic logic&&GeneBehaviorAuthority.IsPrimary<ReproductionGeneLogic>(genes,gene)){primary=logic;break;}
            primary?.OnUpdate(dt);
        }
    }
}
