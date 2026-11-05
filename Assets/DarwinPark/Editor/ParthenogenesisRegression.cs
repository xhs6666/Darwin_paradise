using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DarwinPark.Core;
using DarwinPark.Entities;
using DarwinPark.Entities.Ecology;
using DarwinPark.Entities.Logic;
using DarwinPark.Entities.Stats;
using DarwinPark.Genetics;
using DarwinPark.Genetics.Mutation;
using DarwinPark.Planet;
using DarwinPark.UI;
using UnityEngine;
using Object=UnityEngine.Object;

namespace DarwinPark.EditorTools
{
    // ★ 新增：由种群档案专项运行，覆盖真实孕育/出生、王后分工、基因迁移和磁盘重载，而非只验证计时公式。
    public static class ParthenogenesisRegression
    {
        private static void Clear(){foreach(var e in EntityRegistry.All.ToArray())Object.DestroyImmediate(e.gameObject);ColonyResource.ClearWorld();}
        private static DarwinEntity Spawn(Vector3 position,params (string id,float value)[] definitions)
        {
            var genome=new Genome();foreach(var def in definitions)genome.Add(GeneInstance.Create(def.id,def.value,GeneDatabase.Get(def.id).Tier));
            var before=EntityRegistry.All.ToArray();DarwinEntity.Spawner(genome,position,"孤雌验证实体");var entity=EntityRegistry.All.Single(e=>!before.Contains(e));entity.Genes.mutationChance=0;
            entity.Genes.RestoreStageState(AgeState.Adult,3,2);entity.GetComponent<EntityLifeCycle>().RestoreProgress(LifeCycleSettings.Load().adultProgress);entity.Genes.Recalculate();
            foreach(var component in entity.GetComponents<MonoBehaviour>())if(component is EntityGeneController||component is DarwinPark.Entities.Vitals.EntityVitals||component is DarwinPark.Entities.Vitals.EntityEnvironmentHazard||component is PlanetGravity||component is EntitySurfaceAnchor||component is PopulationBehavior||component is TerritoryBehavior||component.GetType().Name=="EntityAIController")component.enabled=false;
            entity.GetComponent<Rigidbody>().isKinematic=true;Fill(entity,100);Physics.SyncTransforms();return entity;
        }
        private static ReproductionGeneLogic Logic(DarwinEntity entity)=>entity.Genes.Genome.Genes.Select(g=>entity.Genes.GetLogic(g)).OfType<ReproductionGeneLogic>().First();
        private static float Reserve(DarwinEntity entity){entity.GetComponent<EntityStats>().TryGet("storedNutrient",out float value);return value;}
        private static void Fill(DarwinEntity entity,float reserve){entity.GetComponent<EntityStats>().Modify("storedNutrient",reserve-Reserve(entity),false);}
        public static IEnumerator Verify(WorldSessionManager session,PlanetTerrainMeshBuilder builder,SimulationClock clock,Action<bool,string> check)
        {
            string geneId=ParthenogenesisEvolutionUpgrade.GeneId;float originalWait=GameplayTuning.Current.parthenogenesisWaitDays;var originalLimit=EntityPopulationLimit.Capture();
            try
            {
                GameplayTuning.Current.parthenogenesisWaitDays=1;EntityPopulationLimit.UseSnapshot(new WorldGameSettings{entityLimit=0});clock.Pause();Clear();
                var home=EcologyQueries.Surface(builder.transform.position+Vector3.up*(builder.radius+builder.heightScale));float wait=ParthenogenesisCondition.WaitSeconds;
                var catalog=GeneTreeStorage.ReadCatalog();var tree=JsonUtility.FromJson<GeneTreeDocument>(Resources.Load<TextAsset>("Genetics/default_tree").text);tree.parthenogenesisVersion=0;tree.nodes.RemoveAll(n=>n.id==geneId);tree.edges.RemoveAll(e=>e.to==geneId);catalog.Remove(geneId);GeneTreeStorage.Validate(tree,catalog);
                check(catalog.ContainsKey(geneId)&&tree.nodes.Any(n=>n.id==geneId&&n.error)&&tree.edges.Any(e=>e.from=="gene_sexual"&&e.to==geneId&&e.kind=="error"),"旧基因库与旧树补齐有性繁殖的孤雌出错分支");
                int edges=tree.edges.Count;GeneTreeStorage.Validate(tree,catalog);check(tree.edges.Count==edges,"孤雌分支迁移幂等");tree.edges.RemoveAll(e=>e.to==geneId);GeneTreeStorage.Validate(tree,catalog);check(tree.edges.All(e=>e.to!=geneId),"迁移后尊重用户删除孤雌分支连线");
                check(GeneEvolutionRules.Allows("gene_sexual",geneId,"error",80)&&!GeneEvolutionRules.Allows("gene_asexual",geneId,"error",20)&&!GeneEvolutionRules.Allows("gene_sexual",geneId,"evolution",80),"孤雌生殖仅从有性特化通过出错演化");
                var mutation=new Genome();mutation.Add(GeneInstance.Create("gene_sexual",80,1));GeneMutator.Mutate(mutation,new GeneMutationSettings{ErrorWeight=1,DuplicateWeight=0,DeletionWeight=0,MaxErrorDelta=0});check(mutation.Genes.Single().DefinitionId==geneId,"实际基因出错可以得到孤雌生殖基因");

                var ordinary=Spawn(home,("gene_growth",50),("gene_sexual",80));Logic(ordinary).OnUpdate(wait*2);check(!ordinary.Genes.IsGestating,"普通有性繁殖无配偶不会强制孕育");Clear();
                var parent=Spawn(home,("gene_growth",50),(geneId,80));var logic=Logic(parent);parent.Genes.CurrentAge=AgeState.Infant;logic.OnUpdate(wait*2);check(!parent.Genes.IsGestating&&logic.CaptureState().mateWait==0,"幼体不累计孤雌等待或孕育");parent.Genes.CurrentAge=AgeState.Adult;
                logic.OnUpdate(wait-1);check(!parent.Genes.IsGestating&&Mathf.Approximately(logic.CaptureState().mateWait,wait-1),"成年无配偶达到等待阈值前不孕育");
                var stateBefore=logic.CaptureState();logic.OnUpdate(0);WorldAuthority.IsReplica=true;logic.OnUpdate(wait*2);WorldAuthority.IsReplica=false;check(logic.CaptureState().mateWait==stateBefore.mateWait&&!parent.Genes.IsGestating,"暂停与联机副本不推进等待");
                session.SaveNow(false);var saved=WorldSaveStore.Read(WorldSaveStore.FileFor(session.Current.id));check(saved.entities.Single().reproduction.Single().mateWait==stateBefore.mateWait,"无配偶等待真实写盘读取");
                var invalid=JsonUtility.FromJson<WorldSaveData>(JsonUtility.ToJson(saved));invalid.entities[0].reproduction[0].mateWait=float.NaN;bool rejected=false;try{WorldSaveStore.Validate(invalid);}catch(InvalidDataException){rejected=true;}check(rejected,"坏档无配偶计时在清场前被拒绝");
                session.Open(saved);while(session.Busy)yield return null;clock.Pause();parent=EntityRegistry.All.Single();parent.Genes.mutationChance=0;logic=Logic(parent);check(logic.CaptureState().mateWait==stateBefore.mateWait,"实际重载继续原等待进度");
                float reserve=Reserve(parent);parent.GetComponent<EntityStats>().TryGet("size",out float size);float cost=LifeCycleSettings.Load().nutrientCost*Mathf.Max(EcologyBalanceSettings.Current.minimumReproductionSize,size);logic.OnUpdate(1.01f);
                check(parent.Genes.IsGestating&&logic.CaptureState().parthenogenetic&&logic.CaptureState().partner==null&&Mathf.Abs(Reserve(parent)-(reserve-cost))<.002f,"超时启动孤雌孕育并且只扣一次真实营养");check(logic.GetTooltipInfo().Contains("孤雌孕育"),"孕育状态明确显示孤雌生殖");
                session.SaveNow(false);var pregnancy=WorldSaveStore.Read(WorldSaveStore.FileFor(session.Current.id));check(pregnancy.entities.Single().reproduction.Single().parthenogenetic&&LanWorldSnapshot.Frame(1).actors.Single().state.reproduction.Single().parthenogenetic,"孤雌孕育标记写盘并进入联机快照");
                session.Open(pregnancy);while(session.Busy)yield return null;clock.Pause();parent=EntityRegistry.All.Single();logic=Logic(parent);check(parent.Genes.IsGestating&&logic.CaptureState().parthenogenetic&&logic.CaptureState().partner==null,"孕育中实际读档保持单亲而非空配偶基因组");
                int beforeBirth=EntityRegistry.All.Count;logic.OnUpdate(logic.CaptureState().gestation+1);check(!parent.Genes.IsGestating&&EntityRegistry.All.Count==beforeBirth+1,"孤雌孕育完成实际产下一个后代");var child=EntityRegistry.All.Single(e=>e!=parent);check(child.Genes.Genome.Genes.All(g=>parent.Genes.Genome.Genes.Any(p=>p.DefinitionId==g.DefinitionId&&p.Value==g.Value))&&child.Genes.Genome.Genes.Any(g=>g.DefinitionId==geneId),"孤雌后代遗传亲本基因且保留孤雌分支");logic.OnUpdate(.1f);check(!parent.Genes.IsGestating&&logic.CaptureState().mateWait==0,"产后冷却不重复强制孕育");Clear();

                parent=Spawn(home,("gene_growth",50),(geneId,80));logic=Logic(parent);Fill(parent,0);logic.OnUpdate(wait+1);check(!parent.Genes.IsGestating&&logic.CaptureState().mateWait>=wait,"储粮不足继续等待但保留超时资格");
                Fill(parent,100);EntityPopulationLimit.UseSnapshot(new WorldGameSettings{entityLimit=1});reserve=Reserve(parent);logic.OnUpdate(.1f);check(!parent.Genes.IsGestating&&Reserve(parent)==reserve,"孤雌生殖不绕过世界数量上限或预扣营养");EntityPopulationLimit.UseSnapshot(new WorldGameSettings{entityLimit=0});logic.OnUpdate(.1f);check(parent.Genes.IsGestating,"恢复储粮和数量空位后自动开始孤雌孕育");Clear();
                parent=Spawn(home,("gene_growth",50),(geneId,80));logic=Logic(parent);Fill(parent,0);logic.OnUpdate(wait*.6f);var mate=Spawn(home+Vector3.right*2,("gene_growth",50),("gene_sexual",80));logic.OnUpdate(.1f);check(logic.CaptureState().mateWait==0&&!parent.Genes.IsGestating,"找到合适配偶会清零孤雌等待");Fill(parent,100);logic.OnUpdate(.1f);check(parent.Genes.IsGestating&&!logic.CaptureState().parthenogenetic&&logic.CaptureState().partner!=null,"有配偶时优先正常有性孕育");Clear();
                parent=Spawn(home,("gene_growth",50),(geneId,80));logic=Logic(parent);logic.OnUpdate(wait*.6f);parent.Genes.CurrentAge=AgeState.Elder;logic.OnUpdate(wait*2);check(logic.CaptureState().mateWait==0&&!parent.Genes.IsGestating,"离开成年阶段清零孤雌等待");parent.Genes.CurrentAge=AgeState.Adult;parent.Genes.Genome.Genes.Single(g=>g.DefinitionId==geneId).Enabled=false;parent.Genes.Recalculate();logic.OnUpdate(wait*2);check(!parent.Genes.IsGestating,"禁用孤雌基因不会继续强制孕育");Clear();

                parent=Spawn(home,("gene_growth",50),("gene_population",20),(geneId,80));var queen=AntColonyMember.Ensure(parent.GetComponent<PopulationBehavior>());queen.Assign(AntRole.Queen,Guid.NewGuid().ToString("N"));AntColonyReproduction.Advance(queen,wait-1);check(!parent.Genes.IsGestating&&queen.State.mateWait==wait-1&&queen.Group.All(m=>m.Role!=AntRole.Mate),"孤雌王后等待真实配偶而非自动复制配偶");
                session.SaveNow(false);var queenSave=WorldSaveStore.Read(WorldSaveStore.FileFor(session.Current.id));check(queenSave.entities.Single().ecology.ant.mateWait==wait-1,"王后等待进度成对保存读取");
                session.Open(queenSave);while(session.Busy)yield return null;clock.Pause();parent=EntityRegistry.All.Single();queen=parent.GetComponent<AntColonyMember>();check(queen.State.mateWait==wait-1,"王后实际读档恢复原等待时长");
                AntColonyReproduction.Advance(queen,1.01f);check(parent.Genes.IsGestating&&queen.State.parthenogenetic&&queen.State.partnerGenome==null,"王后超时同样启动孤雌孕育");beforeBirth=EntityRegistry.All.Count;AntColonyReproduction.Advance(queen,queen.State.reproductionTimer+1);check(EntityRegistry.All.Count==beforeBirth+1&&EntityRegistry.All.Single(e=>e!=parent).GetComponent<PopulationBehavior>().GroupId==queen.GroupId,"王后孤雌后代继承同群分工");Clear();
                parent=Spawn(home,("gene_growth",50),("gene_population",20),(geneId,80));logic=Logic(parent);AntColonyMember.Ensure(parent.GetComponent<PopulationBehavior>()).Assign(AntRole.Worker,Guid.NewGuid().ToString("N"));logic.OnUpdate(wait*2);check(!parent.Genes.IsGestating&&parent.Genes.Evaluation.Genes.Where(g=>g.Definition.Id==geneId).All(g=>!g.IsActive),"工匠分工仍抑制孤雌分支且不能自主繁殖");Clear();
                parent=Spawn(home,("gene_growth",50),(geneId,80),(geneId,80));reserve=Reserve(parent);parent.GetComponent<EntityStats>().TryGet("size",out size);cost=LifeCycleSettings.Load().nutrientCost*Mathf.Max(EcologyBalanceSettings.Current.minimumReproductionSize,size);
                foreach(var item in parent.Genes.Genome.Genes.Select(g=>parent.Genes.GetLogic(g)).OfType<ReproductionGeneLogic>().ToArray())item.OnUpdate(wait+1);
                check(parent.Genes.IsGestating&&Mathf.Abs(Reserve(parent)-(reserve-cost))<.002f&&parent.Genes.Genome.Genes.Select(g=>parent.Genes.GetLogic(g)).OfType<ReproductionGeneLogic>().Count(r=>r.CaptureState().parthenogenetic)==1,"重复孤雌基因只有一个繁殖行为写入且只扣一次储粮");Clear();

                parent=Spawn(home,("gene_growth",50),(geneId,80),("gene_oviparous",80));var primary=parent.Genes.Genome.Genes.First(g=>parent.Genes.GetLogic(g) is ReproductionGeneLogic&&GeneBehaviorAuthority.IsPrimary<ReproductionGeneLogic>(parent.Genes,g));logic=(ReproductionGeneLogic)parent.Genes.GetLogic(primary);logic.OnUpdate(wait+1);logic.OnUpdate(logic.CaptureState().gestation+1);check(EntityRegistry.All.Count==2&&EntityRegistry.All.Single(e=>e!=parent).GetComponent<EggIncubation>()?.Incubating==true,"孤雌生殖与卵生共存时仍生成待孵化卵");
            }
            finally{WorldAuthority.IsReplica=false;GameplayTuning.Current.parthenogenesisWaitDays=originalWait;EntityPopulationLimit.UseSnapshot(originalLimit);}
        }
    }
}
