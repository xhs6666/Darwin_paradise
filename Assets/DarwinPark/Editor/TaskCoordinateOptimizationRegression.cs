using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DarwinPark.Core;
using DarwinPark.Entities;
using DarwinPark.Entities.Ecology;
using DarwinPark.Entities.Simulation;
using DarwinPark.Entities.Stats;
using DarwinPark.Genetics;
using DarwinPark.Planet;
using DarwinPark.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace DarwinPark.EditorTools
{
    // ★ 新增：验证真实工匠计划、补给插队、磁盘读档和坐标复杂度，避免只检查队列容器本身。
    [InitializeOnLoad]public static class TaskCoordinateOptimizationRegression
    {
        private const string Key="DarwinPark.TaskCoordinates";private static bool _started;private static string _optimization;private static readonly List<string> Passed=new();
        private static string Folder=>Path.GetFullPath("Artifacts/TaskCoordinateReview");
        static TaskCoordinateOptimizationRegression(){EditorApplication.update+=()=>{if(!_started&&Application.isPlaying&&SessionState.GetBool(Key,false)&&LanSession.Instance!=null){_started=true;LanSession.Instance.StartCoroutine(Guard());}};}
        public static void RunBatch(){Application.runInBackground=true;SessionState.SetBool(Key,true);EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");EditorApplication.EnterPlaymode();}
        private static void Check(bool value,string text){if(!value)throw new InvalidOperationException(text);Passed.Add(text);File.WriteAllLines(Path.Combine(Folder,"progress.txt"),Passed);}
        private static IEnumerator Guard()
        {
            Directory.CreateDirectory(Folder);File.WriteAllText(Path.Combine(Folder,"result.txt"),"RUNNING");string error=null;var stack=new Stack<IEnumerator>();stack.Push(Run());
            while(stack.Count>0){bool next=false;object current=null;try{next=stack.Peek().MoveNext();if(next)current=stack.Peek().Current;}catch(Exception ex){error=ex.ToString();Debug.LogException(ex);break;}if(!next){stack.Pop();continue;}if(current is IEnumerator nested)stack.Push(nested);else yield return current;}
            if(_optimization!=null){File.WriteAllText(SimulationOptimization.FilePath,_optimization);SimulationOptimization.Reload();}
            File.WriteAllText(Path.Combine(Folder,"result.txt"),(error==null?"PASS":"FAIL")+"\n"+error+"\n"+string.Join("\n",Passed));SessionState.SetBool(Key,false);EditorApplication.Exit(error==null?0:1);
        }
        private static IEnumerator Wait(Func<bool> ready){double start=EditorApplication.timeSinceStartup;while(!ready()){if(EditorApplication.timeSinceStartup-start>120)throw new TimeoutException("world");yield return null;}}
        private static void Clear(){foreach(var entity in EntityRegistry.All.ToArray())Object.DestroyImmediate(entity.gameObject);ColonyResource.ClearWorld();EntityPerceptionHub.Clear();}
        private static DarwinEntity Spawn(Vector3 point,params (string,float)[] genes)
        {
            var genome=new Genome();foreach(var gene in genes)genome.Add(GeneInstance.Create(gene.Item1,gene.Item2,GeneDatabase.Get(gene.Item1).Tier));
            // ★ 适配：当前种群基因需要中枢，补齐测试夹具使分工组件由真实表达安装。
            if(genome.HasDefinition("gene_population")&&!genome.HasDefinition("gene_nervous_system"))
                genome.Add(GeneInstance.Create("gene_nervous_system",50,GeneDatabase.Get("gene_nervous_system").Tier));
            DarwinEntity child=null;void Added(DarwinEntity value)=>child=value;EntityRegistry.OnRegistered+=Added;
            try{DarwinEntity.Spawner(genome,point,"任务坐标回归");}finally{EntityRegistry.OnRegistered-=Added;}if(child==null)throw new InvalidOperationException("spawn");
            child.Genes.RestoreStageState(AgeState.Adult,3,2);child.GetComponent<EntityLifeCycle>().RestoreProgress(40);child.Genes.Recalculate();Freeze(child);return child;
        }
        private static void Freeze(DarwinEntity entity)
        {
            foreach(var component in entity.GetComponents<MonoBehaviour>())if(component is PlanetGravity||component is EntitySurfaceAnchor||component is PopulationBehavior||component is TerritoryBehavior||component is DarwinPark.Entities.Vitals.EntityEnvironmentHazard||component is DarwinPark.Entities.Vitals.EntityVitals||component.GetType().Name=="EntityAIController")component.enabled=false;
            entity.GetComponent<Rigidbody>().isKinematic=true;entity.Genes.enabled=false;
            // ★ 适配：测试冻结Update不等于退群；读档后的王后也必须保留原种群登记。
            entity.GetComponent<PopulationBehavior>()?.EnsureMembership();
        }
        private static void Food(DarwinEntity entity,float food,float reserve=100){var stats=entity.GetComponent<EntityStats>();stats.TryGet("nutrient",out var f);stats.TryGet("storedNutrient",out var r);stats.Modify("nutrient",food-f,false);stats.Modify("storedNutrient",reserve-r,false);}
        private static ColonyResource Resource(Vector3 point,ColonyResourceKind kind=ColonyResourceKind.Corpse,string group=null,float energy=20)=>ColonyResource.Create(new ColonyResourceSave{id=Guid.NewGuid().ToString("N"),kind=kind,group=group,energy=energy,local=EcologyQueries.ToLocal(point),scale=Vector3.one});
        private static IEnumerator Run()
        {
            var session=Object.FindFirstObjectByType<WorldSessionManager>();session.autoSaveInterval=36000;var planet=Object.FindFirstObjectByType<PlanetTerrainMeshBuilder>();planet.subdivisions=16;typeof(WorldSessionManager).GetField("_initialTerrain",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(session,JsonUtility.ToJson(planet));PlanetClimateMap.Instance.mapResolution=32;PlanetOreSystem.Instance.mapResolution=16;
            session.Create("任务队列与计算优化回归",74119,.5f,20,44);yield return Wait(()=>!session.Busy);var clock=ServiceLocator.Get<SimulationClock>();clock.Pause();Object.FindFirstObjectByType<RandomPlanetSpawner>().enabled=false;PopulationMaintenance.Restore(null);GameplayTuning.Current.mutationRoundsEnabled=false;Clear();
            _optimization=File.ReadAllText(SimulationOptimization.FilePath);
            Check(SimulationOptimization.Parse(_optimization).workerQueueLimit==12,"外部带注释优化配置实际生成并可解析");
            var here=EcologyQueries.Surface(planet.transform.position+Vector3.up*400);string group=Guid.NewGuid().ToString("N");
            yield return CadenceAndSave(session,here);
            Clear();
            var queen=Spawn(here,("gene_population",20),("gene_locomotion",50));queen.GetComponent<AntColonyMember>().Assign(AntRole.Queen,group);Food(queen,10);
            var entity=Spawn(here,("gene_population",20),("gene_locomotion",50));var worker=entity.GetComponent<AntColonyMember>();worker.Assign(AntRole.Worker,group);Food(entity,10);
            var first=Resource(here+Vector3.right*8);var second=Resource(here+Vector3.right*12);string firstId=first.State.id,secondId=second.State.id;
            AntColonyWorker.Advance(worker,.1f);var tasks=WorkerTaskQueue.Tasks(worker);
            Check(tasks.Count==2&&tasks[0].target==firstId&&tasks[1].target==secondId&&worker.State.resourceTarget==firstId,"工匠按全局周围坐标生成顺序任务，首先执行近处尸体 count="+tasks.Count+" radius="+worker.Radius+" target="+worker.State.resourceTarget);
            Check(string.IsNullOrEmpty(second.State.claim),"只独占当前目标，后续计划不锁死其他工匠资源");
            Resource(here,ColonyResourceKind.Supply,group,100);Food(entity,1);AntColonyWorker.Advance(worker,.1f);
            Check(tasks.Count==3&&tasks[0].kind==PlannedTaskKind.Feed&&tasks[1].target==firstId,"饥饿将补给插入首位，原采集计划顺延");
            for(int i=0;i<8;i++)AntColonyWorker.Advance(worker,.1f);Check(tasks.Count==3,"连续饥饿检查不重复插入补给任务");
            ColonyResource.Feed(worker,4);Check(entity.GetComponent<EntityStats>().TryGet("nutrient",out var eaten)&&eaten>1,"补给插队确实转移能量块营养");
            Food(entity,10);AntColonyWorker.Advance(worker,.1f);Check(tasks.Count==2&&tasks[0].target==firstId&&worker.State.resourceTarget==firstId,"吃饱恢复原目标而不是重新随机选任务");
            entity.transform.position=first.transform.position;EntityNeighborhood.ObservePosition(entity.Genes);AntColonyWorker.Advance(worker,.1f);
            Check(worker.State.carriedResource==firstId&&tasks[0].kind==PlannedTaskKind.Deliver,"取货后同一任务进入运输阶段");
            Food(entity,1);AntColonyWorker.Advance(worker,.1f);Check(tasks[0].kind==PlannedTaskKind.Feed&&tasks[1].kind==PlannedTaskKind.Deliver&&worker.State.carriedResource==firstId,"运输中饥饿保留独占货物和交付任务");
            string workerId=worker.Id;session.SaveNow(false);var disk=WorldSaveStore.Read(WorldSaveStore.FileFor(session.Current.id));
            Check(disk!=null,"真实世界与队列写入磁盘");session.Open(disk);yield return Wait(()=>!session.Busy);clock.Pause();entity=EcologyIdentity.Find(workerId);Freeze(entity);worker=entity.GetComponent<AntColonyMember>();tasks=WorkerTaskQueue.Tasks(worker);queen=worker.Queen.Entity;Freeze(queen);
            Check(tasks.Count==3&&tasks[0].kind==PlannedTaskKind.Feed&&tasks[1].kind==PlannedTaskKind.Deliver&&tasks[2].target==secondId&&worker.State.carriedResource==firstId,"真实读档恢复补给、原运输和下一目标顺序");
            Food(entity,10);entity.transform.position=worker.State.deliveryNursery?queen.transform.position:EcologyQueries.ToWorld(worker.State.deliveryPoint);EntityNeighborhood.ObservePosition(entity.Genes);AntColonyWorker.Advance(worker,.1f);
            Check(ColonyResource.Find(firstId)==null&&string.IsNullOrEmpty(worker.State.carriedResource)&&tasks.Count==1&&tasks[0].target==secondId,"送达只转换一次，并移除已完成任务");
            worker.State.scan=99;AntColonyWorker.Advance(worker,.1f);Check(worker.State.resourceTarget==secondId,"完成后立即执行下一任务，不等待旧扫描倒计时");
            ColonyResource.Find(secondId).Remove();AntColonyWorker.Advance(worker,.1f);Check(tasks.Count==0&&worker.State.resourceTarget==null,"目标失效自动跳过并释放当前任务");
            bool rejected=false;try{WorkerTaskQueue.Validate(new List<PlannedEntityTask>{new(){kind=PlannedTaskKind.CollectCorpse,target=firstId},new(){kind=PlannedTaskKind.CollectCorpse,target=firstId}});}catch(InvalidDataException){rejected=true;}Check(rejected,"重复任务标识在清场前拒绝");
            Clear();var ordinary=Spawn(here,("gene_nutrient_utilization",50),("gene_storage",50));Food(ordinary,1,0);var blockA=Resource(here+Vector3.right*8,ColonyResourceKind.Energy,group);var blockB=Resource(here+Vector3.right*12,ColonyResourceKind.Energy,group);var metabolism=ordinary.GetComponent<EnergyBlockMetabolism>();
            Check(metabolism.Destination(out _)&&EntityResourcePlan.Capture(ordinary.Genes).energy.SequenceEqual(new[]{blockA.State.id,blockB.State.id}),"普通实体按全局坐标排列多个能量块任务");string remaining=blockB.State.id;blockA.Remove();Check(metabolism.Destination(out _)&&EntityResourcePlan.Capture(ordinary.Genes).energy[0]==remaining,"普通能量块任务完成后立即接续下一项");
            string ordinaryId=EcologyIdentity.Of(ordinary);session.SaveNow(false);var ordinaryDisk=WorldSaveStore.Read(WorldSaveStore.FileFor(session.Current.id));Check(ordinaryDisk.entities.Any(e=>e.ecology?.resourcePlan?.energy?.Contains(remaining)==true),"普通实体资源队列真实写入世界文件");session.Open(ordinaryDisk);yield return Wait(()=>!session.Busy);clock.Pause();ordinary=EcologyIdentity.Find(ordinaryId);Freeze(ordinary);Food(ordinary,1,0);Check(EntityResourcePlan.Capture(ordinary.Genes).energy[0]==remaining&&ordinary.GetComponent<EnergyBlockMetabolism>().Destination(out _),"普通实体读档后继续原能量块计划");
            // ★ 适配：食腐夹具需要表达异养基因，确保使用实际营养链安装的尸体消费组件。
            var corpseA=Resource(here+Vector3.forward*8);var corpseB=Resource(here+Vector3.forward*12);var scavenger=Spawn(here,("gene_nutrition",50),("gene_heterotrophy",50),("gene_scavenging",50),("gene_storage",50));Food(scavenger,1,0);var corpseLogic=scavenger.GetComponent<CorpseFeeding>();corpseLogic.Advance(.1f);Check(EntityResourcePlan.Capture(scavenger.Genes).corpses.SequenceEqual(new[]{corpseA.State.id,corpseB.State.id}),"普通食腐实体同样生成顺序尸体计划");corpseA.Remove();corpseLogic.Advance(.01f);Check(EntityResourcePlan.Capture(scavenger.Genes).corpses[0]==corpseB.State.id,"食腐当前目标耗尽无需等待扫描冷却即可接续");EntityResourcePlan.Restore(scavenger.Genes,null);Check(EntityResourcePlan.Capture(scavenger.Genes)==null,"恢复空旧快照清除之前的普通资源计划");
            Clear();var food=Resource(here,ColonyResourceKind.Energy,Guid.NewGuid().ToString("N"));for(int i=0;i<300;i++)Resource(here+Vector3.right*(1000+i),ColonyResourceKind.Corpse);var coordinates=new List<ResourceCoordinate>();long visits=ResourceCoordinates.CandidateVisits;ResourceCoordinates.Query(here,8,true,false,coordinates);
            Check(coordinates.Count==1&&coordinates[0].resource==food&&ResourceCoordinates.CandidateVisits-visits==1,"300具尸体不增加能量块近食搜索候选");
            food.CarryTo(here+Vector3.forward*40);ResourceCoordinates.Query(here,8,true,false,coordinates);Check(coordinates.Count==0,"搬运立即更新资源全局坐标");food.gameObject.SetActive(false);ResourceCoordinates.Food(coordinates);Check(coordinates.Count==0,"停用资源从全局索引删除");
            var high=Resource(here+Vector3.up*100,ColonyResourceKind.Supply,group);ResourceCoordinates.Query(here,1,true,false,coordinates);Check(coordinates.Any(c=>c.resource==high),"球面搜索忽略高度，投影坐标不漏同方向高处资源");
            Check(Object.FindFirstObjectByType<ResourceVisualScheduler>()!=null,"全局外观调度组件实际安装");
            // ★ 适配：竞争测试实体明确表达光合作用；基础营养基因不再直接生产。
            Clear();var a=Spawn(here,("gene_storage",50),("gene_nutrition",20),("gene_photosynthesis",50));var b=Spawn(here+Vector3.right,("gene_storage",50),("gene_nutrition",20),("gene_photosynthesis",50));GeneExpressionCache.Clear();a.Genes.Recalculate();b.Genes.Recalculate();
            Check(GeneExpressionCache.Hits==1&&GeneExpressionCache.Misses==1,"相同DNA纯表达共享一次计算");
            Check(!ReferenceEquals(a.Genes.Evaluation.Genes[0].Gene,b.Genes.Evaluation.Genes[0].Gene)&&!ReferenceEquals(a.Genes.Evaluation.Attributes,b.Genes.Evaluation.Attributes),"命中缓存仍保留各自基因身份与属性表");
            a.Genes.EnvironmentGeneSuppression=30;a.Genes.Recalculate();Check(GeneExpressionCache.Misses==2,"环境抑制不同不得复用相同表达");
            a.Genes.EnvironmentGeneSuppression=0;a.Genes.Recalculate();float competition=EntityNeighborhood.Competition(a.Genes);b.transform.position+=Vector3.right*100;EntityNeighborhood.ObservePosition(b.Genes);Check(competition>1&&Mathf.Abs(EntityNeighborhood.Competition(a.Genes)-1)<.0001f,"生产者离开后局部竞争立即失效");
            var observation=Spawn(here,("gene_locomotion",50),("gene_observation",75));EntityPerceptionHub.Read(observation.Genes);a.gameObject.SetActive(false);Check(!EntityPerceptionHub.TryLocate(observation.Genes,a,out _),"反向订阅注销死亡目标后不能继续追踪");
            var cadence=new ActionDecisionCadence();Check(cadence.Due(observation.Genes,.01f)&&!cadence.Due(observation.Genes,.01f),"决策间隔内复用意图，不重复完整搜索");observation.Genes.Recalculate();Check(cadence.Due(observation.Genes,.01f),"表达变化立即打断决策缓存");
            float elapsed=.1f;ActionMotionBatch.Begin(observation.Genes,.5f);ActionMotionBatch.Step(observation.Genes,false);Check(!ActionMotionBatch.Consume(observation.Genes,ref elapsed),"非末尾小步不重复写物理移动");ActionMotionBatch.Step(observation.Genes,true);Check(ActionMotionBatch.Consume(observation.Genes,ref elapsed)&&Mathf.Abs(elapsed-.5f)<.0001f,"末尾小步使用完整累计时间转向");ActionMotionBatch.End(observation.Genes);
            ActionMotionBatch.Begin(observation.Genes,.7f);ActionMotionBatch.Begin(b.Genes,.1f);ActionMotionBatch.End(b.Genes);ActionMotionBatch.Step(observation.Genes,true);elapsed=0;Check(ActionMotionBatch.Consume(observation.Genes,ref elapsed)&&Mathf.Abs(elapsed-.7f)<.0001f,"嵌套实体回调恢复原批次累计时间");ActionMotionBatch.End(observation.Genes);
            var body=observation.GetComponent<Rigidbody>();var probe=new GravityProbeCache();Vector3 dir=(planet.transform.position-body.position).normalized;probe.Probe(planet.transform,body,dir,out _);long rays=probe.Raycasts;probe.Probe(planet.transform,body,dir,out _);Check(probe.Raycasts==rays,"静止同地形接地探测在有效期内复用");body.position+=Vector3.right;probe.Probe(planet.transform,body,dir,out _);Check(probe.Raycasts==rays+1,"位置改变立即恢复接地射线");
            Clear();Check(ResourceCoordinates.Count==0,"清场释放全局资源坐标与旧世界引用");
            File.WriteAllText(SimulationOptimization.FilePath,_optimization.Replace("\"decisionSeconds\": 0.25","\"decisionSeconds\": 0.3"));File.SetLastWriteTimeUtc(SimulationOptimization.FilePath,DateTime.UtcNow.AddSeconds(2));yield return new WaitForSecondsRealtime(2.2f);SimulationOptimization.Poll();Check(Mathf.Abs(SimulationOptimization.Current.decisionSeconds-.3f)<.0001f,"外部优化配置通过单一全局入口自动热加载");
        }

        private static IEnumerator CadenceAndSave(WorldSessionManager session,Vector3 here)
        {
            var observation=Spawn(here,("gene_locomotion",50),("gene_nervous_system",50));
            var cadence=new ActionDecisionCadence();
            Check(cadence.Due(observation.Genes,.01f)&&!cadence.Due(observation.Genes,.01f),"普通目标在决策间隔内复用");
            // ★ 适配：普通决策已降频，验证真实显式命令和命令撤销均可在同帧打断等待。
            var order=RtsOrderController.Ensure(observation);
            Check(order.Issue(RtsOrderKind.Stop,here)&&cadence.Due(observation.Genes,.001f),"显式停止同帧打断普通决策节奏");
            order.State.active=false;
            Check(cadence.Due(observation.Genes,.001f)&&!cadence.Due(observation.Genes,.001f),"命令原地结束立即释放意图，普通小步继续复用");
            // ★ 适配：通过实际自动保存入口和磁盘文件核对后台快照，再验证显式保存可立即读到更新。
            string savePath=WorldSaveStore.FileFor(session.Current.id);
            var priorWrite=File.GetLastWriteTimeUtc(savePath);
            observation.DisplayName="后台自动存档检查";
            session.autoSaveInterval=.05f;
            yield return Wait(()=>File.GetLastWriteTimeUtc(savePath)>priorWrite);
            session.autoSaveInterval=36000;
            Check(WorldSaveStore.Read(savePath).entities.Any(e=>e.name=="后台自动存档检查"),"自动保存后台落盘并可读取完整实体");
            observation.DisplayName="显式保存检查";
            session.SaveNow(false);
            Check(WorldSaveStore.Read(savePath).entities.Any(e=>e.name=="显式保存检查")&&File.Exists(savePath+".bak"),"显式保存顺序落盘且保留上一代备份");
        }
    }
}
