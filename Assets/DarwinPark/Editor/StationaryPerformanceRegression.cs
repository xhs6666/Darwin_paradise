using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using DarwinPark.Core;
using DarwinPark.Entities;
using DarwinPark.Entities.Ecology;
using DarwinPark.Entities.Stats;
using DarwinPark.Genetics;
using DarwinPark.Planet;
using DarwinPark.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Debug=UnityEngine.Debug;
using Object=UnityEngine.Object;

namespace DarwinPark.EditorTools
{
    // ★ 新增：验证生产路径与旧公式一致，并运行真实地形、物理、环境、UI下的400/800个体，避免只测空菜单FPS。
    [InitializeOnLoad] public static class StationaryPerformanceRegression
    {
        private const string Key="DarwinPark.StationaryPerformance";private static bool _started;
        private static readonly List<string> Results=new List<string>();private static readonly List<DarwinEntity> Entities=new List<DarwinEntity>();
        private static string Folder=>Path.GetFullPath("Artifacts/StationaryPerformanceReview");
        static StationaryPerformanceRegression(){EditorApplication.update+=()=>{if(!_started&&Application.isPlaying&&SessionState.GetBool(Key,false)&&LanSession.Instance!=null){_started=true;LanSession.Instance.StartCoroutine(Guard());}};}
        public static void RunBatch(){Application.runInBackground=true;SessionState.SetBool(Key,true);EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");EditorApplication.EnterPlaymode();}
        private static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);Results.Add("PASS: "+message);}
        private static IEnumerator Guard()
        {
            Directory.CreateDirectory(Folder);File.WriteAllText(Path.Combine(Folder,"result.txt"),"RUNNING");Results.Clear();yield return null;
            string error=null;var stack=new Stack<IEnumerator>();stack.Push(Run());
            while(stack.Count>0)
            {
                bool next=false;object value=null;try{next=stack.Peek().MoveNext();if(next)value=stack.Peek().Current;}catch(Exception ex){error=ex.ToString();Debug.LogException(ex);break;}
                if(!next){stack.Pop();continue;}if(value is IEnumerator nested)stack.Push(nested);else yield return value;
            }
            File.WriteAllText(Path.Combine(Folder,"result.txt"),(error==null?"PASS":"FAIL")+"\n"+error+"\n"+string.Join("\n",Results));SessionState.SetBool(Key,false);Debug.Log("[StationaryPerformance] "+(error==null?"PASS":"FAIL"));EditorApplication.Exit(error==null?0:1);
        }
        private static DarwinEntity Spawn(Vector3 point,bool movement=false)
        {
            var genome=new Genome();foreach(string id in new[]{"gene_growth","gene_nutrition","gene_reproduction","gene_environment"})genome.Add(GeneInstance.Create(id,50,GeneDatabase.Get(id).Tier));
            if(movement)genome.Add(GeneInstance.Create("gene_locomotion",50,0));
            DarwinEntity created=null;void Registered(DarwinEntity e)=>created=e;
            EntityRegistry.OnRegistered+=Registered;try{DarwinEntity.Spawner(genome,point,"静止规模验证");}finally{EntityRegistry.OnRegistered-=Registered;}
            created.Genes.mutationChance=0;return created;
        }
        private static void Clean(){foreach(var entity in EntityRegistry.All.ToArray())Object.DestroyImmediate(entity.gameObject);Entities.Clear();}
        private static Vector3 Spread(int i,int count)
        {float y=1-2f*(i+.5f)/count,a=i*2.39996323f,r=Mathf.Sqrt(1-y*y);return new Vector3(r*Mathf.Cos(a),y,r*Mathf.Sin(a))*350;}
        private static float Legacy(EntityGeneController genes)
        {
            var config=EcologyBalanceSettings.Current;int count=0;var p=genes.transform.position;
            foreach(var e in EntityRegistry.All)if(e!=null&&e.gameObject!=genes.gameObject&&(e.transform.position-p).sqrMagnitude<=config.competitionRadius*config.competitionRadius&&EcologyQueries.Alive(e)&&EcologyEnergyBudget.IsProducer(e.Genes))count++;
            return 1+count*config.competitionPerNeighbor;
        }
        private static void Compare(string distribution)
        {
            var watch=Stopwatch.StartNew();var expected=Entities.Select(e=>Legacy(e.Genes)).ToArray();watch.Stop();double baseline=watch.Elapsed.TotalMilliseconds;
            EntityNeighborhood.Clear();watch.Restart();var actual=Entities.Select(e=>EcologyEnergyBudget.Competition(e.Genes)).ToArray();watch.Stop();double optimized=watch.Elapsed.TotalMilliseconds;
            Check(expected.Zip(actual,(a,b)=>Mathf.Abs(a-b)<.0001f).All(x=>x),Entities.Count+" "+distribution+" 逐实体竞争结果与旧公式一致");
            int builds=EntityNeighborhood.BuildCount;foreach(var e in Entities)for(int i=0;i<5;i++)EcologyEnergyBudget.Competition(e.Genes);
            Check(EntityNeighborhood.BuildCount==builds,"重复子步未重建全图索引 "+Entities.Count+" "+distribution);
            Results.Add(string.Format(CultureInfo.InvariantCulture,"Competition {0} {1}: legacy={2:F3}ms, production-index+cold-build={3:F3}ms",Entities.Count,distribution,baseline,optimized));
        }
        private static IEnumerator Run()
        {
            var clock=ServiceLocator.Get<SimulationClock>();clock.Pause();
            var budget=new SimulationStepBudget();float first=budget.Consume(10,16,.5f);double delivered=first;
            Check(first==.5f&&Math.Abs(budget.Pending-159.5)<.0001,"10秒长帧在16倍下只交付0.5秒，其余159.5秒排队");
            for(int i=0;i<319;i++)delivered+=budget.Consume(0,1,.5f);
            Check(Math.Abs(delivered-160)<.0001&&budget.Pending==0,"排队模拟时间完整交付，无丢弃和额外时间");
            Check(Mathf.Abs(budget.Consume(1f/60,16,.5f)-16f/60)<.00001f,"正常帧保留16倍模拟步长");budget.Clear();
            foreach(int count in new[]{400,800,1600})
            {
                Clean();for(int i=0;i<count;i++){var e=Spawn(Spread(i,count));Entities.Add(e);e.Genes.enabled=false;e.GetComponent<Rigidbody>().isKinematic=true;if(i%32==31)yield return null;}
                yield return null;Compare("spread");yield return null;
                if(count==400){for(int i=0;i<count;i++)Entities[i].transform.position=new Vector3(i%20*.2f,350,i/20*.2f);EntityNeighborhood.Clear();Compare("dense");yield return null;}
            }
            Clean();var a=Spawn(new Vector3(0,350,0));var b=Spawn(new Vector3(1,350,0));a.Genes.enabled=b.Genes.enabled=false;
            float together=Legacy(a.Genes);Check(EcologyEnergyBudget.Competition(a.Genes)==together,"出生后立即计入竞争");
            var original=a.Genes.Genome.Genes.Single(g=>g.DefinitionId=="gene_nutrition");var copy=GeneInstance.Create("gene_nutrition",50,0);copy.InstanceId=new string('0',32);
            a.Genes.OnEvaluationChanged+=_=>DarwinPark.Entities.Logic.GeneBehaviorAuthority.IsPrimary<DarwinPark.Entities.Logic.NutritionGeneLogic>(a.Genes,copy);
            a.Genes.Genome.Add(copy);a.Genes.Recalculate();Check(DarwinPark.Entities.Logic.GeneBehaviorAuthority.IsPrimary<DarwinPark.Entities.Logic.NutritionGeneLogic>(a.Genes,copy),"表达事件先于挂载时，挂载版本仍使主行为缓存正确失效");
            copy.Enabled=false;a.Genes.Recalculate();Check(DarwinPark.Entities.Logic.GeneBehaviorAuthority.IsPrimary<DarwinPark.Entities.Logic.NutritionGeneLogic>(a.Genes,original),"同帧禁用复制基因后恢复原主行为");
            b.GetComponent<EntityStats>().Modify("health",-100,false);Check(EcologyEnergyBudget.Competition(a.Genes)==Legacy(a.Genes),"同帧生命归零立即退出竞争");
            b.GetComponent<EntityStats>().Modify("health",100,false);Check(EcologyEnergyBudget.Competition(a.Genes)==together,"同帧恢复生命重新计入竞争");
            b.gameObject.SetActive(false);Check(EcologyEnergyBudget.Competition(a.Genes)==1,"停用立即清除成员");b.gameObject.SetActive(true);
            var nutrition=b.Genes.Genome.Genes.Single(g=>g.DefinitionId=="gene_nutrition");nutrition.Enabled=false;b.Genes.Recalculate();Check(EcologyEnergyBudget.Competition(a.Genes)==1,"同帧禁用营养基因更新生产者分类");
            nutrition.Enabled=true;b.Genes.Recalculate();Check(EcologyEnergyBudget.Competition(a.Genes)==together,"同帧恢复营养表达更新生产者分类");
            b.transform.position+=Vector3.right*100;EntityNeighborhood.ObservePosition(b.Genes);Check(EcologyEnergyBudget.Competition(a.Genes)==Legacy(a.Genes),"跨网格移动同步后不存在幽灵邻居");
            var hits=new List<DarwinEntity>();EntityNeighborhood.Query(a.transform.position,2,hits);Check(hits.Count==1&&hits[0]==a,"局部查询使用真实半径且排除远处实体");
            var snapshot=EntitySaveState.Capture(b);snapshot.position=a.transform.position+Vector3.right;snapshot.Restore(b);Check(EcologyEnergyBudget.Competition(a.Genes)==Legacy(a.Genes),"存档恢复位置和营养后同帧缓存正确");
            EntityRegistry.Clear();EntityNeighborhood.Query(a.transform.position,200,hits);Check(hits.Count==0,"清场后索引不保留上一局对象");Object.DestroyImmediate(a.gameObject);Object.DestroyImmediate(b.gameObject);
            var session=Object.FindFirstObjectByType<WorldSessionManager>();session.autoSaveInterval=36000;session.Create("静止性能隔离验证",742031,.5f,20,44);
            while(session.Busy)yield return null;Check(WorldRuntimeGate.Ready&&session.Current!=null,"真实星球生成并进入游戏");clock.Pause();
            var builder=Object.FindFirstObjectByType<PlanetTerrainMeshBuilder>();var surface=builder.GetComponent<MeshCollider>();
            for(int i=0;i<800;i++)
            {
                RaycastHit hit;Vector3 direction;int tries=0;
                do{direction=UnityEngine.Random.onUnitSphere;surface.Raycast(new Ray(builder.transform.position+direction*1000,-direction),out hit,2000);tries++;}while(tries<100&&(hit.collider==null||(hit.point-builder.transform.position).magnitude<builder.WaterRadius+1));
                Check(hit.collider!=null,"落点命中真实地形 "+i);Entities.Add(Spawn(hit.point+direction*.65f));if(i%16==15)yield return null;
                if(i==399)yield return RuntimeSample(clock,400);
            }
            yield return RuntimeSample(clock,800);
            clock.Play();typeof(SimulationClock).GetField("<GameDeltaTime>k__BackingField",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(clock,.1f);
            var watch=Stopwatch.StartNew();foreach(var e in EntityRegistry.All.ToArray())e.Genes.AdvanceSimulation(.1f);watch.Stop();Results.Add("800 genes 0.1s all: "+watch.Elapsed.TotalMilliseconds.ToString("F3")+"ms");
            var vitals=typeof(DarwinPark.Entities.Vitals.EntityVitals).GetMethod("AdvanceVitals",BindingFlags.NonPublic|BindingFlags.Instance);watch.Restart();foreach(var e in EntityRegistry.All.ToArray())vitals.Invoke(e.GetComponent<DarwinPark.Entities.Vitals.EntityVitals>(),new object[]{.1f});watch.Stop();Results.Add("800 vitals 0.1s all: "+watch.Elapsed.TotalMilliseconds.ToString("F3")+"ms");
            var environment=typeof(DarwinPark.Entities.Vitals.EntityEnvironmentHazard).GetMethod("Update",BindingFlags.NonPublic|BindingFlags.Instance);watch.Restart();foreach(var e in EntityRegistry.All.ToArray())environment.Invoke(e.GetComponent<DarwinPark.Entities.Vitals.EntityEnvironmentHazard>(),null);watch.Stop();Results.Add("800 environment all: "+watch.Elapsed.TotalMilliseconds.ToString("F3")+"ms");clock.Pause();
            var dormant=Entities.FirstOrDefault(e=>e!=null&&e.GetComponent<Rigidbody>().IsSleeping());Check(dormant!=null,"真实地表的无行动实体进入休眠");
            Check(StationaryBodyRest.IsResting(dormant.GetComponent<Rigidbody>()),"只有经过地面接触验证的休眠才可跳过地表约束");
            float height=builder.heightScale;builder.heightScale+=1;Check(!StationaryBodyRest.IsResting(dormant.GetComponent<Rigidbody>()),"星球地形参数变化使休眠接触缓存失效");builder.heightScale=height;
            var body=dormant.GetComponent<Rigidbody>();body.AddForce(Vector3.up,ForceMode.VelocityChange);Check(!body.IsSleeping(),"碰撞/外力仍能唤醒休眠动态刚体");
            body.Sleep();dormant.Genes.Genome.Add(GeneInstance.Create("gene_locomotion",50,0));dormant.Genes.Recalculate();Check(!StationaryBodyRest.RemainsAsleep(body,dormant.Genes)&&!body.IsSleeping(),"获得有效行动基因后恢复物理推进");
            var rooted=Entities.First(e=>e!=null&&e!=dormant&&StationaryBodyRest.IsResting(e.GetComponent<Rigidbody>()));var rootedBody=rooted.GetComponent<Rigidbody>();var origin=rootedBody.position;
            rootedBody.position+=EcologyQueries.Up(origin)*60;Physics.SyncTransforms();Check(!StationaryBodyRest.RemainsAsleep(rootedBody,rooted.Genes)&&!rootedBody.IsSleeping(),"休眠实体被传送到高空时恢复地表安全网");
            Check(Entities.Where(e=>e!=null).All(e=>e.GetComponentsInChildren<Light>(true).Length==0),"普通无行动实体不创建闲置泳光");
            Results.Add("Runtime samples include actual terrain, environment, physics and UI; Editor observations are not release-build FPS guarantees.");
        }
        private static IEnumerator RuntimeSample(SimulationClock clock,int nominal)
        {
            foreach(int scale in new[]{1,16})
            {
                // ★ 修复（防Bug回归）：各倍率独立测量，上一轮16倍的积压不能污染下一轮1倍基线。
                clock.RestoreState(clock.GameTime,scale);clock.Play();yield return new WaitForSecondsRealtime(1);var times=new List<double>();double start=EditorApplication.timeSinceStartup,previous=start;float gameTime=clock.GameTime;
                while(EditorApplication.timeSinceStartup-start<4){yield return null;double now=EditorApplication.timeSinceStartup;times.Add((now-previous)*1000);previous=now;}
                clock.Pause();times.Sort();double seconds=EditorApplication.timeSinceStartup-start;
                Results.Add(string.Format(CultureInfo.InvariantCulture,"Actual planet {0} nominal / {1} alive, {2}x: frames={3}, elapsed={4:F2}s, avg={5:F2}ms, p95={6:F2}ms, max={7:F2}ms, game={8:F2}s, pending={9:F2}s, asleep={10}",nominal,EntityRegistry.All.Count,scale,times.Count,seconds,seconds*1000/times.Count,times[(int)(times.Count*.95)],times[times.Count-1],clock.GameTime-gameTime,clock.PendingGameTime,EntityRegistry.All.Count(e=>e.GetComponent<Rigidbody>().IsSleeping())));
                Check(times.Count>=5&&clock.GameTime>gameTime,"真实星球持续推进，无补算锁死 "+nominal+" / "+scale+"x");File.WriteAllLines(Path.Combine(Folder,"progress.txt"),Results);
            }
        }
    }
}
