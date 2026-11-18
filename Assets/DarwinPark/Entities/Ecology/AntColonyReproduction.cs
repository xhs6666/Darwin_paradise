using System.Linq;
using DarwinPark.Entities.Logic;
using DarwinPark.Entities.Stats;
using DarwinPark.Genetics;
using UnityEngine;

// ★ 修复（防Bug回归）：可选组件允许正常缺失，使用即时TryGet，保留安装/销毁可见性并消除编辑器空查询分配。
namespace DarwinPark.Entities.Ecology
{
    // ★ 新增：王后独占繁殖流程，有性蚁群只维持一名不可自主繁殖的配偶，孕育进度随分工状态保存。
    public static class AntColonyReproduction
    {
        public static bool BlocksOrdinary(EntityGeneController genes)=>AntColonyMember.IsManaged(genes);
        private static Genome Copy(Genome genome)
        {
            var result=Genome.Copy(genome);for(int i=0;i<result.Genes.Count;i++)result.Genes[i].OriginInstanceId=genome.Genes[i].InstanceId;return result;
        }
        private static DarwinEntity Spawn(Genome genome,Vector3 point)
        {
            if(!EntityPopulationLimit.CanSpawn)return null;
            // ★ 适配：配偶同样通过注册事件取得身份，保留原生成委托。
            return EntitySpawnCapture.Create(genome,point);
        }
        public static void Advance(AntColonyMember queen,float dt)
        {
            if(queen.Role!=AntRole.Queen||queen.Young||queen.State.founding||queen.Genes.CurrentAge!=AgeState.Adult){queen.State.mateWait=0;return;}
            if(dt<=0||DarwinPark.Core.WorldAuthority.IsReplica)return;
            var genes=queen.Genes;var state=queen.State;var settings=AntColonySettings.Current;var stats=queen.GetComponent<EntityStats>();
            var reproduction=genes.Evaluation.Genes.Where(g=>g.IsActive&&genes.GetLogic(g.Gene) is ReproductionGeneLogic).OrderByDescending(g=>g.Definition.Tier).FirstOrDefault();
            if(reproduction==null)
            {state.mateWait=0;if(genes.IsGestating){genes.IsGestating=false;state.reproductionTimer=0;genes.Recalculate();}return;}
            bool sexual=reproduction.State?.StateId=="sexual";
            var mates=queen.Group.Where(m=>m.Role==AntRole.Mate).OrderBy(m=>m.Population.JoinOrder).ToArray();
            // 配偶不来自出生随机池；损失后由王后花费实际储备补充，并立即锁定为同群成年配偶。
            // ★ 适配：带孤雌分支的王后等待真实配偶，不再凭空复制一个配偶绕过等待机制；其他有性王后保持既有补配偶流程。
            if(EntityPopulationLimit.CanSpawn&&sexual&&!ParthenogenesisCondition.Enabled(genes)&&mates.Length==0&&stats.TryGet("storedNutrient",out var mateReserve)&&mateReserve>=settings.mateCost&&DarwinEntity.Spawner!=null)
            {
                var mate=ColonyFormation.TryBirthPosition(queen,out var matePoint)?Spawn(Copy(genes.Genome),matePoint):null;
                if(mate!=null)
                {
                    AntColonyMember.Ensure(DarwinPark.Core.OptionalComponent.Read<PopulationBehavior>(mate)).Assign(AntRole.Mate,queen.GroupId);
                    mate.GetComponent<EntityLifeCycle>().RestoreProgress(LifeCycleSettings.Load().adultProgress);
                    int mask=(1<<(int)AgeState.Infant)|(1<<(int)AgeState.Adult);mate.Genes.RestoreStageState(AgeState.Adult,mask,2);mate.Genes.Recalculate();
                    stats.Modify("storedNutrient",-settings.mateCost);mates=new[]{DarwinPark.Core.OptionalComponent.Read<AntColonyMember>(mate)};
                }
            }
            if(genes.IsGestating)
            {
                state.mateWait=0;
                state.reproductionTimer=Mathf.Max(0,state.reproductionTimer-dt);
                if(state.reproductionTimer>0||DarwinEntity.Spawner==null)return;
                // ★ 适配：共享多胎交付策略，每个已出生子代立即扣减待出生数，读档不重复。
                if(!MultipleBirthPolicy.Deliver(ref state.litter,()=>QueenBirthDelivery.TrySpawn(queen)))return;
                state.litter=null;genes.IsGestating=false;state.partnerGenome=null;state.parthenogenetic=false;state.reproductionCooldown=NaturalPopulationPace.Current.enabled?NaturalPopulationPace.Current.queenCooldownDays*LifeCycleSettings.Load().secondsPerDay:settings.queenCooldownSeconds;genes.Recalculate();return;
            }
            if(state.reproductionCooldown>0){state.mateWait=0;return;}
            var eligibleMate=mates.FirstOrDefault(m=>EcologyQueries.Distance(queen.transform.position,m.transform.position)<=settings.feedRadius);
            bool parthenogenetic=ParthenogenesisCondition.Advance(genes,ref state.mateWait,!sexual||eligibleMate!=null,dt);
            if(!EntityPopulationLimit.CanSpawn||sexual&&eligibleMate==null&&!parthenogenetic)return;
            if(!stats.TryGet("storedNutrient",out float reserve)||reserve<settings.queenBirthCost*MultipleBirthPolicy.Count(genes))return;
            // ★ 适配：维持比例超量时延长王后的新孕育间隔，不中断已开始的孕育。
            // ★ 适配：建群缺工匠时优先建立补给链，不能再被全局移动者超量的繁殖等待卡住；仍照常支付食物和孕育时间。
            if(!ColonyFoundingSupport.NeedsWorker(queen)&&!PopulationMaintenance.BreedingReady(genes,ref state.maintenanceWait,dt))return;state.maintenanceWait=0;
            // ★ 适配：受孕时固定总胎数和对应预算，不让后续变异/调参改变已经付款的这一胎。
            state.litter=MultipleBirthPolicy.Begin(genes);stats.Modify("storedNutrient",-settings.queenBirthCost*state.litter.planned);state.partnerGenome=sexual&&!parthenogenetic?Copy(eligibleMate.Genes.Genome):null;state.parthenogenetic=parthenogenetic;state.mateWait=0;state.reproductionTimer=NaturalPopulationPace.Current.enabled?Mathf.Clamp(NaturalPopulationPace.Current.queenGestationDays*EcologyBalanceSettings.Current.referenceReproductionSpeed/Mathf.Max(EcologyBalanceSettings.Current.minimumReproductionSpeed,genes.GetAttribute(EcologyBalanceSettings.Current.reproductionSpeedAttribute)),NaturalPopulationPace.Current.minimumGestationDays,NaturalPopulationPace.Current.maximumGestationDays)*LifeCycleSettings.Load().secondsPerDay:settings.queenGestationSeconds;genes.IsGestating=true;genes.Recalculate();
        }
    }
}
