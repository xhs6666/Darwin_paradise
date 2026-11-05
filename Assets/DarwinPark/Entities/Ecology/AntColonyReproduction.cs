using System.Linq;
using DarwinPark.Entities.Logic;
using DarwinPark.Entities.Stats;
using DarwinPark.Genetics;
using UnityEngine;

namespace DarwinPark.Entities.Ecology
{
    // ★ 新增：王后独占繁殖流程，有性蚁群只维持一名不可自主繁殖的配偶，孕育进度随分工状态保存。
    public static class AntColonyReproduction
    {
        public static bool BlocksOrdinary(EntityGeneController genes)=>AntColonyMember.IsManaged(genes);
        private static Genome Copy(Genome genome)
        {
            var result=new Genome();
            foreach(var gene in genome.Genes)
            {var copy=GeneInstance.Create(gene.DefinitionId,gene.Value,gene.Tier);copy.Enabled=gene.Enabled;copy.CopyStrength=gene.CopyStrength;copy.MissingStateIds=new System.Collections.Generic.List<string>(gene.MissingStateIds);copy.OriginInstanceId=gene.InstanceId;result.Add(copy);}
            return result;
        }
        private static DarwinEntity Spawn(Genome genome,Vector3 point)
        {
            if(!EntityPopulationLimit.CanSpawn)return null;
            var before=EntityRegistry.All.ToArray();DarwinEntity.Spawner?.Invoke(genome,point,"");return EntityRegistry.All.FirstOrDefault(e=>!before.Contains(e));
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
                var mate=Spawn(Copy(genes.Genome),EcologyQueries.Surface(queen.transform.position+Vector3.ProjectOnPlane(Vector3.right,EcologyQueries.Up(queen.transform.position))*settings.arrivalDistance));
                if(mate!=null)
                {
                    AntColonyMember.Ensure(mate.GetComponent<PopulationBehavior>()).Assign(AntRole.Mate,queen.GroupId);
                    mate.GetComponent<EntityLifeCycle>().RestoreProgress(LifeCycleSettings.Load().adultProgress);
                    int mask=(1<<(int)AgeState.Infant)|(1<<(int)AgeState.Adult);mate.Genes.RestoreStageState(AgeState.Adult,mask,2);mate.Genes.Recalculate();
                    stats.Modify("storedNutrient",-settings.mateCost);mates=new[]{mate.GetComponent<AntColonyMember>()};
                }
            }
            if(genes.IsGestating)
            {
                state.mateWait=0;
                state.reproductionTimer=Mathf.Max(0,state.reproductionTimer-dt);
                if(state.reproductionTimer>0||DarwinEntity.Spawner==null)return;
                if(!OffspringPlacement.TryGetPosition(queen.transform.position,false,out var point))return;
                var child=Copy(genes.Genome);
                if(state.partnerGenome!=null)
                    foreach(var gene in child.Genes){var partner=state.partnerGenome.Genes.FirstOrDefault(g=>g.DefinitionId==gene.DefinitionId);if(partner!=null)gene.Value=(gene.Value+partner.Value)*.5f;}
                // 使用原出生入口，卵生/孵化和分工继承仍由同一个入口处理。
                if(!EcologyReproduction.Spawn(genes,child,point))return;genes.IsGestating=false;state.partnerGenome=null;state.parthenogenetic=false;state.reproductionCooldown=settings.queenCooldownSeconds;genes.Recalculate();return;
            }
            if(state.reproductionCooldown>0){state.mateWait=0;return;}
            var eligibleMate=mates.FirstOrDefault(m=>EcologyQueries.Distance(queen.transform.position,m.transform.position)<=settings.feedRadius);
            bool parthenogenetic=ParthenogenesisCondition.Advance(genes,ref state.mateWait,!sexual||eligibleMate!=null,dt);
            if(!EntityPopulationLimit.CanSpawn||sexual&&eligibleMate==null&&!parthenogenetic)return;
            if(!stats.TryGet("storedNutrient",out float reserve)||reserve<settings.queenBirthCost)return;
            stats.Modify("storedNutrient",-settings.queenBirthCost);state.partnerGenome=sexual&&!parthenogenetic?Copy(eligibleMate.Genes.Genome):null;state.parthenogenetic=parthenogenetic;state.mateWait=0;state.reproductionTimer=settings.queenGestationSeconds;genes.IsGestating=true;genes.Recalculate();
        }
    }
}
