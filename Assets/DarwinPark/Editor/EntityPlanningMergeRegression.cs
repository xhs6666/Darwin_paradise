using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DarwinPark.Core;
using DarwinPark.Entities;
using DarwinPark.Entities.Ecology;
using DarwinPark.Entities.Logic;
using DarwinPark.Entities.Simulation;
using DarwinPark.Entities.Stats;
using DarwinPark.Entities.Vitals;
using DarwinPark.Genetics;
using DarwinPark.Planet;
using DarwinPark.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace DarwinPark.EditorTools
{
    // ★ 新增：真实实体/能量/出生/磁盘快照回归，验证共享的计算量与不能共享的个体身份。
    [InitializeOnLoad] public static class EntityPlanningMergeRegression
    {
        private const string Key="DarwinPark.PlanningMergeReview";private static bool _started;
        private static readonly List<string> Passed=new List<string>();
        private static readonly BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
        private static string Folder=>Path.GetFullPath("Artifacts/EntityPlanningMergeReview");
        static EntityPlanningMergeRegression(){EditorApplication.update+=()=>{if(!_started&&Application.isPlaying&&SessionState.GetBool(Key,false)&&LanSession.Instance!=null){_started=true;LanSession.Instance.StartCoroutine(Guard());}};}
        public static void RunBatch(){Application.runInBackground=true;SessionState.SetBool(Key,true);EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");EditorApplication.EnterPlaymode();}
        private static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);Passed.Add(message);File.WriteAllLines(Path.Combine(Folder,"progress.txt"),Passed);}
        private static IEnumerator Guard()
        {
            Directory.CreateDirectory(Folder);Passed.Clear();File.WriteAllText(Path.Combine(Folder,"result.txt"),"RUNNING");string error=null;var stack=new Stack<IEnumerator>();stack.Push(Run());
            while(stack.Count>0){bool next=false;object current=null;try{next=stack.Peek().MoveNext();if(next)current=stack.Peek().Current;}catch(Exception ex){error=ex.ToString();Debug.LogException(ex);break;}if(!next){stack.Pop();continue;}if(current is IEnumerator nested)stack.Push(nested);else yield return current;}
            File.WriteAllText(Path.Combine(Folder,"result.txt"),(error==null?"PASS":"FAIL")+"\n"+error+"\n"+string.Join("\n",Passed));SessionState.SetBool(Key,false);EditorApplication.Exit(error==null?0:1);
        }
        private static IEnumerator Wait(Func<bool> done,string name)
        {double start=EditorApplication.timeSinceStartup;while(!done()){if(EditorApplication.timeSinceStartup-start>60)throw new TimeoutException(name);yield return null;}}
        private static void Clear(){foreach(var entity in EntityRegistry.All.ToArray())Object.DestroyImmediate(entity.gameObject);ColonyResource.ClearWorld();}
        private static DarwinEntity Spawn(Genome genome,Vector3 point,AgeState age=AgeState.Adult)
        {
            DarwinEntity child=null;void Registered(DarwinEntity entity)=>child=entity;EntityRegistry.OnRegistered+=Registered;
            try{DarwinEntity.Spawner(RandomSpawnRecipe.Clone(genome),point,"共享回归");}finally{EntityRegistry.OnRegistered-=Registered;}
            if(child==null)throw new InvalidOperationException("实体生成失败");child.Genes.mutationChance=0;
            child.GetComponent<EntityLifeCycle>().RestoreProgress(age==AgeState.Adult?40:0);child.Genes.RestoreStageState(age,age==AgeState.Adult?3:1,age==AgeState.Adult?2:1);child.Genes.Recalculate();
            foreach(var behavior in child.GetComponents<MonoBehaviour>())if(behavior is PlanetGravity||behavior is EntitySurfaceAnchor||behavior is PopulationBehavior||behavior is TerritoryBehavior||behavior is EntityEnvironmentHazard||behavior.GetType().Name=="EntityAIController")behavior.enabled=false;
            child.GetComponent<Rigidbody>().isKinematic=true;return child;
        }
        private static Vector3 Land(PlanetTerrainMeshBuilder planet)
        {
            for(int i=0;i<256;i++){var direction=Quaternion.Euler(0,i*137.5f,0)*new Vector3(1,(i%10-5)*.1f,0).normalized;var point=EcologyQueries.Surface(planet.transform.position+direction*(planet.radius+planet.heightScale));if((point-planet.transform.position).magnitude>planet.WaterRadius+3)return point;}throw new InvalidOperationException("没有测试陆地");
        }
        private static ReproductionGeneLogic Reproduction(DarwinEntity entity)=>entity.Genes.Genome.Genes.Select(entity.Genes.GetLogic).OfType<ReproductionGeneLogic>().First();
        private static void Food(DarwinEntity entity,float food,float reserve)
        {var stats=entity.GetComponent<EntityStats>();stats.TryGet("nutrient",out var f);stats.TryGet("storedNutrient",out var r);stats.Modify("nutrient",food-f,false);stats.Modify("storedNutrient",reserve-r,false);}
        private static IEnumerator Run()
        {
            var session=Object.FindFirstObjectByType<WorldSessionManager>();session.autoSaveInterval=36000;var planet=Object.FindFirstObjectByType<PlanetTerrainMeshBuilder>();planet.subdivisions=24;typeof(WorldSessionManager).GetField("_initialTerrain",Private).SetValue(session,JsonUtility.ToJson(planet));PlanetClimateMap.Instance.mapResolution=64;PlanetOreSystem.Instance.mapResolution=32;
            session.Create("实体计划与合并回归",29375,.5f,20,44);yield return Wait(()=>!session.Busy,"创建世界");var clock=ServiceLocator.Get<SimulationClock>();clock.Pause();Object.FindFirstObjectByType<RandomPlanetSpawner>().enabled=false;PopulationMaintenance.Restore(null);Clear();GameInputGate.Blocked=false;
            var tuning=GameplayTuning.Current;Check(tuning.baseMetabolismMultiplier==.5f&&tuning.movementMetabolismMultiplier==.5f&&tuning.nutritionGainMultiplier==.5f,"基础、行动与获取默认同时降至一半");
            Check(GeneDatabase.Get(MultipleBirthEvolutionUpgrade.GeneId)?.LogicScriptId=="MultipleBirthGeneLogic"&&GeneTreeStorage.Active.edges.Any(e=>e.from==MultipleBirthEvolutionUpgrade.SourceGeneId&&e.to==MultipleBirthEvolutionUpgrade.GeneId&&e.kind=="error"),"多胎基因注册并接入繁殖出错树");
            var home=Land(planet);var producer=EcologyDefaults.Template("producer").CreateGenome();producer.Genes.RemoveAll(g=>g.DefinitionId=="gene_reproduction");
            var scheduler=Object.FindFirstObjectByType<EntityWorkScheduler>();Check(scheduler!=null,"游戏根节点实际安装全局调度");tuning.mergeQuietSeconds=0;
            var clones=new List<DarwinEntity>();for(int i=0;i<24;i++)clones.Add(Spawn(producer,home+Vector3.right*((i%6)*.35f)+Vector3.forward*(i/6*.35f)));
            var far=Spawn(producer,home+Vector3.right*tuning.mergeRadius*4);var changed=RandomSpawnRecipe.Clone(producer);changed.Genes.First(g=>g.DefinitionId=="gene_repair").Value=61;var different=Spawn(changed,home+Vector3.forward*2);
            foreach(var entity in EntityRegistry.All)Food(entity,10,30);
            var baseline=clones.Select(EntitySaveState.Capture).ToArray();
            float beforeLife=clones[0].GetComponent<EntityLifeCycle>().Progress;long representatives=scheduler.RepresentativeSteps,shared=scheduler.SharedMemberSteps;
            scheduler.Advance(.5f);
            Check(scheduler.SharedMemberSteps-shared>=23*5&&scheduler.RepresentativeSteps-representatives<=3*5,"24个近邻共享代表基础计算，远处或不同基因分组");
            Check(clones.All(e=>Mathf.Abs(e.GetComponent<EntityLifeCycle>().Progress-clones[0].GetComponent<EntityLifeCycle>().Progress)<.0001f)&&clones[0].GetComponent<EntityLifeCycle>().Progress>beforeLife,"合并成员各自保留寿命且继续成长");
            Check(clones.Select(e=>EcologyIdentity.Of(e)).Distinct().Count()==24&&clones.Select(e=>e.Genes.Genome.Genes[0].InstanceId).Distinct().Count()==24,"合并计算不合并实体或基因实例身份");
            Check(clones.All(e=>(StationaryCohortKernel.Values(e.GetComponent<EntityStats>())-StationaryCohortKernel.Values(clones[0].GetComponent<EntityStats>())).sqrMagnitude<.00001f),"代表营养变化应用给全部兼容近邻");
            var merged=StationaryCohortKernel.Values(clones[0].GetComponent<EntityStats>());float mergedLife=clones[0].GetComponent<EntityLifeCycle>().Progress;
            for(int index=0;index<clones.Count;index++)baseline[index].Restore(clones[index]);
            for(int step=0;step<5;step++)foreach(var entity in clones){entity.Genes.AdvanceSimulation(.1f);entity.GetComponent<EntityVitals>().AdvanceShared(.1f);}
            Check((merged-StationaryCohortKernel.Values(clones[0].GetComponent<EntityStats>())).sqrMagnitude<.000001f&&Mathf.Abs(mergedLife-clones[0].GetComponent<EntityLifeCycle>().Progress)<.00001f,"相同状态下共享结算与逐体计算的营养/寿命数值一致");
            clones[1].GetComponent<EntityStats>().Modify("health",-5,false);scheduler.Advance(.1f);
            Check(!EntityWorkScheduler.Dormant(clones[1].Genes)&&clones.Skip(2).All(e=>e.GetComponent<EntityStats>().CapacityRemaining("health")==0),"受伤实体立即拆组且伤害不传播给其他成员");
            var debt=EntitySaveState.Capture(clones[2]);Check(debt.pendingSimulationTime>0&&JsonUtility.FromJson<EntitySaveState>(JsonUtility.ToJson(debt)).pendingSimulationTime==debt.pendingSimulationTime,"欠算时间进入序列化快照");
            session.SaveNow(false);var pendingDisk=WorldSaveStore.Read(WorldSaveStore.FileFor(session.Current.id));Check(pendingDisk.entities.Any(e=>e.pendingSimulationTime>0),"欠算时间实际落盘并通过完整世界校验");
            scheduler.enabled=false;float old=clones[2].GetComponent<EntityLifeCycle>().Progress;EntityWorkScheduler.AdvanceUnscheduled(clones[2].Genes,.1f);Check(clones[2].Genes.PendingSimulationTime==0&&clones[2].GetComponent<EntityLifeCycle>().Progress>old,"停用合并后补清欠下时间");scheduler.enabled=true;
            clones[2].Genes.PendingSimulationTime=12;EntityWorkScheduler.AdvanceUnscheduled(clones[2].Genes,.1f);Check(Mathf.Abs(clones[2].Genes.PendingSimulationTime-7)<.0001f,"大额欠算按每次补算上限分段处理，剩余时间继续保留");
            EntityWorkScheduler.AdvanceUnscheduled(clones[2].Genes,.1f);EntityWorkScheduler.AdvanceUnscheduled(clones[2].Genes,.1f);Check(clones[2].Genes.PendingSimulationTime==0,"后续补算可以真正追平累计时间");
            WorldAuthority.IsReplica=true;old=clones[0].GetComponent<EntityLifeCycle>().Progress;scheduler.Advance(1);WorldAuthority.IsReplica=false;Check(clones[0].GetComponent<EntityLifeCycle>().Progress==old,"联机副本不独立推进共享生命");
            // 验证阶段抽样只改变代表，不把同一个基因变异复制给整个组。
            foreach(var entity in clones){entity.GetComponent<EntityLifeCycle>().RestoreProgress(LifeCycleSettings.Load().adultProgress-.001f);entity.Genes.RestoreStageState(AgeState.Infant,1,1);entity.Genes.Recalculate();}
            tuning.mutationAttemptsPerFrame=100;tuning.mergeQuietSeconds=0;long skipped=GlobalMutationManager.MergedSkipped;yield return null;scheduler.Advance(.5f);
            Check(GlobalMutationManager.MergedSkipped>skipped&&clones.All(e=>e.Genes.CurrentAge==AgeState.Adult),"近邻阶段变异只抽部分代表，但所有成员照常成年");
            Check(clones.All(e=>e.GetComponent<EntityEnvironmentHazard>().CurrentTemperature==0),"共享、拆组和欠账补算尊重被禁用的环境组件");
            // 满预算时只延期，不能把其他组的时间直接丢掉。
            tuning.mergeGroupsPerFrame=1;scheduler.Advance(.5f);Check(EntityRegistry.All.Any(e=>e.Genes.PendingSimulationTime>=.49f),"全局共享预算不足时保留其他组的模拟时间");tuning.mergeGroupsPerFrame=128;
            Clear();yield return null;
            var killer=Spawn(producer,home,AgeState.Infant);killer.GetComponent<EntityLifeCycle>().RestoreProgress(LifeCycleSettings.Load().adultProgress-.001f);var manual=killer.gameObject.AddComponent<EntityManualControl>();manual.BeginControl(planet.transform);
            var ghost=Spawn(EcologyDefaults.Template("producer").CreateGenome(),home+Vector3.right*5);ghost.Genes.IsGestating=true;ghost.Genes.Recalculate();var ready=Reproduction(ghost).CaptureState();ready.gestation=0;Reproduction(ghost).RestoreState(ready);ghost.GetComponent<EntityStats>().Modify("health",-5,false);
            killer.Genes.OnEvaluationChanged+=_=>{if(EcologyQueries.Alive(ghost))EntityDeathSettlement.Die(ghost);};scheduler.Advance(.1f);
            Check(EntityRegistry.All.Count==1&&!ghost.gameObject.activeInHierarchy,"高优先级先处理后，已死亡的后续快照成员不能继续生产后代");
            var replicaBefore=StationaryCohortKernel.Values(killer.GetComponent<EntityStats>());WorldAuthority.IsReplica=true;clock.Play();typeof(EntityVitals).GetMethod("Update",Private).Invoke(killer.GetComponent<EntityVitals>(),null);clock.Pause();WorldAuthority.IsReplica=false;
            Check(StationaryCohortKernel.Values(killer.GetComponent<EntityStats>())==replicaBefore,"联机副本的原Vitals.Update也不重复消耗营养");
            Clear();yield return null;
            var hunter=Spawn(EcologyDefaults.Template("mobile").CreateGenome(),home);hunter.Genes.Genome.Genes.First(g=>g.DefinitionId=="gene_heterotrophy").Value=100;hunter.Genes.Recalculate();Food(hunter,0,0);
            float absorbed=NutritionTransfer.Digest(hunter,10);Check(Mathf.Abs(absorbed-5)<.001f,"完全消化的猎食者在半速获取下只吸收一半预算");
            var victim=Spawn(producer,home+Vector3.right*3);Food(hunter,0,0);float biomass=ColonyResource.Biomass(victim);var corpse=EntityDeathSettlement.Die(victim,hunter);var accepted=StationaryCohortKernel.Values(hunter.GetComponent<EntityStats>());
            Check(corpse!=null&&Mathf.Abs(accepted.y+accepted.z+corpse.State.energy-biomass)<.001f,"降低捕猎获取后实际吸收加剩余尸体仍严格守恒");
            Clear();yield return null;
            var grades=new List<ColonyResource>();foreach(float energy in new[]{10f,30,90,270,810})grades.Add(ColonyResource.Create(new ColonyResourceSave{id=Guid.NewGuid().ToString("N"),kind=ColonyResourceKind.Energy,group=Guid.NewGuid().ToString("N"),energy=energy,local=EcologyQueries.ToLocal(home+Vector3.right*grades.Count*2),scale=Vector3.one}));
            for(int i=0;i<5;i++){var block=new MaterialPropertyBlock();grades[i].GetComponent<Renderer>().GetPropertyBlock(block);ColorUtility.TryParseHtmlString(EnergyBlockGrades.Table.levels[i].color,out var color);Check(EnergyBlockGrades.Level(grades[i].State.energy)==i&&block.GetColor("_BaseColor")==color,"五级能量块实际颜色与能量范围 "+(i+1));}
            var screenshotCamera=Camera.main.GetComponent<OrbitCamera>();screenshotCamera.ReturnToEntity(grades[2].transform.position);yield return null;yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(Folder,"energy-levels.png"));yield return null;
            grades[4].State.energy=10;grades[4].GetComponent<EnergyBlockGradeVisual>().Refresh();Check(grades[4].name.Contains("Lv.1")&&grades[4].Capture().energy==10,"能量耗尽降级且存档不凭等级补能量");ColonyResource.ClearWorld();
            var parentGenome=EcologyDefaults.Template("producer").CreateGenome();parentGenome.Genes.RemoveAll(g=>g.DefinitionId=="gene_reproduction");parentGenome.Add(GeneInstance.Create(MultipleBirthEvolutionUpgrade.GeneId,100,1));var parent=Spawn(parentGenome,home);Food(parent,10,100);var mate=Spawn(parentGenome,home+Vector3.right*3);Food(mate,10,100);Physics.SyncTransforms();
            Check(parent.Genes.GetAttribute("growthSpeed")>0,"多胎分支未孕育时不常驻抑制成长");
            var reproduction=Reproduction(parent);reproduction.OnUpdate(.1f);var pregnancy=reproduction.CaptureState();Check(parent.Genes.IsGestating&&pregnancy.litter?.planned==5,"真实多胎孕育固定五胎并支付相应预算");pregnancy.gestation=0;reproduction.RestoreState(pregnancy);
            EntityPopulationLimit.UseSnapshot(new WorldGameSettings{entityLimit=4});reproduction.OnUpdate(.1f);pregnancy=reproduction.CaptureState();Check(EntityRegistry.All.Count==4&&parent.Genes.IsGestating&&pregnancy.litter.remaining==3,"数量上限只交付两胎并保留剩余三胎");
            session.SaveNow(false);var saved=WorldSaveStore.Read(WorldSaveStore.FileFor(session.Current.id));string parentId=EcologyIdentity.Of(parent);Check(saved.entities.Single(e=>e.ecology.id==parentId).reproduction.Any(r=>r.litter?.remaining==3),"待出生胎数实际写入JSON磁盘存档");
            var bad=JsonUtility.FromJson<WorldSaveData>(JsonUtility.ToJson(saved));bad.entities.Single(e=>e.ecology.id==parentId).reproduction.First(r=>r.litter!=null).litter.remaining=99;bool rejected=false;try{WorldSaveStore.Validate(bad);}catch(InvalidDataException){rejected=true;}Check(rejected&&EcologyQueries.Alive(parent),"损坏胎数在清场前拒绝");
            session.Open(saved);yield return Wait(()=>!session.Busy,"读回多胎");clock.Pause();parent=EcologyIdentity.Find(parentId);reproduction=Reproduction(parent);Check(reproduction.CaptureState().litter.remaining==3,"读档恢复三胎且不重置到五胎");
            EntityPopulationLimit.UseSnapshot(new WorldGameSettings{entityLimit=20});for(int i=0;i<5&&parent.Genes.IsGestating;i++)reproduction.OnUpdate(.1f);Check(EntityRegistry.All.Count==7&&!parent.Genes.IsGestating,"释放空位后只出生剩余三胎，总共五个后代");
            Clear();yield return null;
            var queenGenome=EcologyDefaults.Template("ant_founder").CreateGenome();queenGenome.Genes.RemoveAll(g=>g.DefinitionId=="gene_reproduction");queenGenome.Add(GeneInstance.Create(MultipleBirthEvolutionUpgrade.GeneId,100,1));var queenEntity=Spawn(queenGenome,home);var queen=AntColonyMember.Ensure(queenEntity.GetComponent<PopulationBehavior>());queen.Assign(AntRole.Queen,Guid.NewGuid().ToString("N"));queen.Population.EnsureMembership();Food(queenEntity,10,100);queen.Advance(.1f);
            Check(queen.Genes.IsGestating&&queen.State.litter?.planned==5,"蚁群王后接入相同多胎策略");queen.State.reproductionTimer=0;queen.Advance(.1f);Check(EntityRegistry.All.Count>1&&queen.Capture().litter?.remaining<5,"王后实际交付多胎并保留进度");
            var workerEntity=Spawn(queenGenome,home+Vector3.right*15);var worker=AntColonyMember.Ensure(workerEntity.GetComponent<PopulationBehavior>());worker.Assign(AntRole.Worker,queen.GroupId);worker.Population.EnsureMembership();Food(workerEntity,6.9f,0);
            int capBefore=EntityPopulationLimit.Maximum;EntityPopulationLimit.UseSnapshot(new WorldGameSettings{entityLimit=EntityRegistry.All.Count+1});
            Check(EntityPopulationLimit.CanSpawn&&!ColonyContinuityPriority.CanSpawn(worker.Genes)&&ColonyContinuityPriority.CanSpawn(queen.Genes),"接近全局上限时普通繁殖让出真实空位，王后可以使用且不突破上限");EntityPopulationLimit.UseSnapshot(new WorldGameSettings{entityLimit=capBefore});
            var successorEntity=Spawn(queenGenome,home+Vector3.forward*20);var successorQueen=AntColonyMember.Ensure(successorEntity.GetComponent<PopulationBehavior>());successorQueen.Assign(AntRole.Queen,Guid.NewGuid().ToString("N"));
            for(int i=0;i<EcologyDefaults.Current.minimumWorkers;i++){var helper=Spawn(queenGenome,home+Vector3.forward*(22+i*3));AntColonyMember.Ensure(helper.GetComponent<PopulationBehavior>()).Assign(AntRole.Worker,successorQueen.GroupId);}
            Check(successorQueen.ChooseBirthRole()==AntRole.Reproductive,"首批工匠到位的小群优先培养后继繁殖个体");
            var supply=ColonyResource.Convert(queen.GroupId,worker.transform.position,100,false);worker.State.patrolRemaining=20;ColonyFeeding.Find(worker,false);string locked=worker.State.plan.supply;
            var competitor=ColonyResource.Convert(queen.GroupId,worker.transform.position+Vector3.Cross(EcologyQueries.Up(worker.transform.position),Vector3.up).normalized*4,100,false);worker.transform.position=competitor.transform.position;ColonyFeeding.Find(worker,false);Check(worker.State.plan.supply==locked,"出现更近可用补给时不抢占尚有效的原计划");
            Food(workerEntity,7.1f,0);Check(ColonyFeeding.WantsFood(worker)&&ColonyFeeding.Find(worker,false)?.State.id==locked,"越过开始补给线后仍保留本次补给和固定目标");
            for(int i=0;i<30&&ColonyFeeding.WantsFood(worker);i++)ColonyResource.Feed(worker,.1f);
            worker.GetComponent<EntityStats>().TryGet("nutrient",out float fed);Check(fed>=tuning.memberFeedResume&&!ColonyFeeding.WantsFood(worker),"吃到恢复线才释放补给任务");
            Food(workerEntity,6,0);ColonyFeeding.Find(worker,false);var depleted=ColonyResource.Find(worker.State.plan.supply);string depletedId=depleted.State.id;depleted.Remove();Check(ColonyFeeding.Find(worker,false)!=null&&worker.State.plan.supply!=depletedId,"补给耗尽失效后才选择新资源");
            Check(worker.Capture().plan!=null&&EntityTaskPlan.Priority(ColonyActivity.Feed)>EntityTaskPlan.Priority(ColonyActivity.Transport),"计划成对保存且补给优先于运输/巡逻");
            yield return null;yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(Folder,"runtime.png"));yield return null;
        }
    }
}
