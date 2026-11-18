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
            // ★ 修复（防Bug回归）：个体显式任务不能共享代表的进度或完成状态。
            if(DarwinPark.Core.OptionalComponent.Read<EntityActivityPlan>(genes)?.Active==true)return false;
            foreach(var evaluation in genes.Evaluation.Genes)
            {
                if(!evaluation.IsActive)continue;
                var logic=genes.GetLogic(evaluation.Gene);
                // ★ 修复（防Bug回归）：没有行动表达时中枢入口必定空返，不应仅因持有该基因禁用整株植物的批结算。
                if(logic is CentralGeneLogic&&GeneCapabilities.Active(genes,BiologySettings.Current.locomotionGene)==null)continue;
                // ★ 修复（防Bug回归）：营养拆分后旧白名单把所有植物退回逐帧模拟；基础代谢已无生产副作用。
                if(logic is AutotrophyGeneLogic)
                {if(evaluation.State?.StateId!=EcologyBalanceSettings.Current.photoState)return false;}
                else if(logic!=null&&logic is not NutritionGeneLogic&&logic is not GrowthGeneLogic&&logic is not RepairGeneLogic&&logic is not StorageGeneLogic&&logic is not EnvironmentAdaptationGeneLogic&&logic is not ReproductionGeneLogic&&logic is not ObservationGeneLogic)return false;
            }
            return true;
        }
        public static float LifeSpeed(EntityGeneController genes)
        {
            return NaturalPopulationPace.LifeSpeed(genes);
        }
        public static Vector3 Sample(EntityGeneController genes,float dt)
            =>Sample(genes,genes.GetComponent<EntityStats>(),genes.GetComponent<EntityEnvironmentHazard>(),genes.GetComponent<EntityVitals>(),dt);
        // ★ 适配：调度已有生命周期内组件引用，代表和成员的小步不能再重复查询同一组件。
        internal static Vector3 Sample(EntityGeneController genes,EntityStats stats,EntityEnvironmentHazard environment,EntityVitals vitals,float dt)
        {
            var before=Values(stats);
            // ★ 适配：冻结无意义的被动Update/感知，保留真正的生产与环境/代谢结算。
            foreach(var gene in genes.Genome.Genes)if(genes.GetLogic(gene) is AutotrophyGeneLogic nutrition)nutrition.OnUpdate(dt);
            if(environment!=null&&environment.isActiveAndEnabled)environment.AdvanceShared(dt);
            if(vitals!=null&&vitals.isActiveAndEnabled)vitals.AdvanceShared(dt);
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
            =>Apply(representative.GetComponent<EntityEnvironmentHazard>(),genes,genes.GetComponent<EntityStats>(),genes.GetComponent<EntityEnvironmentHazard>(),genes.GetComponent<DarwinEntity>(),delta);
        internal static void Apply(EntityEnvironmentHazard source,EntityGeneController genes,EntityStats stats,EntityEnvironmentHazard environment,DarwinEntity entity,Vector3 delta)
        {
            stats.Modify("health",delta.x,false);stats.Modify("nutrient",delta.y,false);stats.Modify("storedNutrient",delta.z,false);
            if(source!=null&&source.isActiveAndEnabled)environment?.CopySharedReadings(source);
            if(stats.TryGet("health",out var health)&&health<=0)EntityDeathSettlement.Die(entity);
        }
        public static void Reproduce(EntityGeneController genes,float dt)=>PrimaryReproduction(genes)?.OnUpdate(dt);
        internal static ReproductionGeneLogic PrimaryReproduction(EntityGeneController genes)
        {
            // ★ 修复（防Bug回归）：复制逻辑可能在孕育/出生回调中重算基因，先保留当前逻辑引用再调用。
            ReproductionGeneLogic primary=null;
            foreach(var gene in genes.Genome.Genes)
                if(genes.GetLogic(gene) is ReproductionGeneLogic logic&&GeneBehaviorAuthority.IsPrimary<ReproductionGeneLogic>(genes,gene)){primary=logic;break;}
            return primary;
        }
    }
}
