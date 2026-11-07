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
    // ★ 新增：同一场景对比逐体与合并的调度成本、帧峰值和分组对象复用，避免只用代表计算次数判断性能。
    [InitializeOnLoad] public static class SchedulerPerformanceRegression
    {
        private const string Key="DarwinPark.SchedulerPerformance";private static bool _started;
        private static readonly List<string> Results=new();
        private static string Folder=>Path.GetFullPath("Artifacts/SchedulerPerformanceReview");
        static SchedulerPerformanceRegression(){EditorApplication.update+=()=>{if(!_started&&Application.isPlaying&&SessionState.GetBool(Key,false)&&LanSession.Instance!=null){_started=true;LanSession.Instance.StartCoroutine(Guard());}};}
        public static void RunBatch(){Application.runInBackground=true;SessionState.SetBool(Key,true);EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");EditorApplication.EnterPlaymode();}
        private static IEnumerator Guard()
        {
            Directory.CreateDirectory(Folder);Results.Clear();File.WriteAllText(Path.Combine(Folder,"result.txt"),"RUNNING");string error=null;var stack=new Stack<IEnumerator>();stack.Push(Run());
            while(stack.Count>0){bool next=false;object current=null;try{next=stack.Peek().MoveNext();if(next)current=stack.Peek().Current;}catch(Exception ex){error=ex.ToString();UnityEngine.Debug.LogException(ex);break;}if(!next){stack.Pop();continue;}if(current is IEnumerator nested)stack.Push(nested);else yield return current;}
            File.WriteAllText(Path.Combine(Folder,"result.txt"),(error==null?"PASS":"FAIL")+"\n"+error+"\n"+string.Join("\n",Results));SessionState.SetBool(Key,false);EditorApplication.Exit(error==null?0:1);
        }
        private static void Clear(){foreach(var entity in EntityRegistry.All.ToArray())Object.DestroyImmediate(entity.gameObject);ColonyResource.ClearWorld();}
        private static DarwinEntity Spawn(Genome genome,Vector3 position)
        {
            DarwinEntity result=null;void Registered(DarwinEntity entity)=>result=entity;EntityRegistry.OnRegistered+=Registered;
            try{DarwinEntity.Spawner(RandomSpawnRecipe.Clone(genome),position,"调度测量");}finally{EntityRegistry.OnRegistered-=Registered;}
            result.Genes.mutationChance=0;result.GetComponent<EntityLifeCycle>().RestoreProgress(40);result.Genes.RestoreStageState(AgeState.Adult,3,2);result.Genes.Recalculate();
            foreach(var behavior in result.GetComponents<MonoBehaviour>())if(behavior is PlanetGravity||behavior is EntitySurfaceAnchor||behavior is PopulationBehavior||behavior is TerritoryBehavior||behavior is EntityEnvironmentHazard||behavior.GetType().Name=="EntityAIController")behavior.enabled=false;
            result.GetComponent<Rigidbody>().isKinematic=true;return result;
        }
        private static void Measure(string name,EntityWorkScheduler scheduler,DarwinEntity[] entities,bool merged,float dt)
        {
            var tuning=GameplayTuning.Current;tuning.mergeStationaryEntities=merged;
            UnityEngine.Random.InitState(31987);
            var vitals=entities.Select(e=>e.GetComponent<EntityVitals>()).ToArray();var environments=entities.Select(e=>e.GetComponent<EntityEnvironmentHazard>()).ToArray();
            void Step(){scheduler.Advance(dt);for(int e=0;e<entities.Length;e++)if(entities[e]!=null&&entities[e].isActiveAndEnabled&&!EntityWorkScheduler.Dormant(entities[e].Genes)){if(environments[e]!=null&&environments[e].isActiveAndEnabled)environments[e].AdvanceShared(dt);if(vitals[e]!=null&&vitals[e].isActiveAndEnabled)vitals[e].AdvanceShared(dt);}}
            for(int i=0;i<60;i++)Step();
            long representatives=scheduler.RepresentativeSteps,shared=scheduler.SharedMemberSteps;var property=scheduler.GetType().GetProperty("CohortPoolSize");int pool=property!=null?(int)property.GetValue(scheduler):-1;
            var samples=new double[180];var watch=new Stopwatch();
            for(int i=0;i<samples.Length;i++){watch.Restart();Step();watch.Stop();samples[i]=watch.Elapsed.TotalMilliseconds;}
            Array.Sort(samples);
            string report=string.Format(CultureInfo.InvariantCulture,"{0} merged={1} dt={2:F4} entities={3} average_ms={4:F4} p95_ms={5:F4} max_ms={6:F4} new_cohort_objects={7} shared={8} representatives={9} dormant={10}",name,merged,dt,entities.Length,samples.Average(),samples[170],samples[179],property!=null?(int)property.GetValue(scheduler)-pool:-1,scheduler.SharedMemberSteps-shared,scheduler.RepresentativeSteps-representatives,scheduler.DormantMembers);
            Results.Add(report);File.WriteAllLines(Path.Combine(Folder,"samples.txt"),Results);
        }
        private static IEnumerator Run()
        {
            var session=Object.FindFirstObjectByType<WorldSessionManager>();session.autoSaveInterval=36000;var planet=Object.FindFirstObjectByType<PlanetTerrainMeshBuilder>();planet.subdivisions=24;typeof(WorldSessionManager).GetField("_initialTerrain",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(session,JsonUtility.ToJson(planet));PlanetClimateMap.Instance.mapResolution=64;PlanetOreSystem.Instance.mapResolution=32;
            session.Create("调度性能隔离测量",29375,.5f,20,44);double start=EditorApplication.timeSinceStartup;while(session.Busy){if(EditorApplication.timeSinceStartup-start>90)throw new TimeoutException("创建测试世界");yield return null;}
            var clock=ServiceLocator.Get<SimulationClock>();clock.Pause();Object.FindFirstObjectByType<RandomPlanetSpawner>().enabled=false;PopulationMaintenance.Restore(null);Clear();
            var scheduler=Object.FindFirstObjectByType<EntityWorkScheduler>();var tuning=GameplayTuning.Current;tuning.mergeQuietSeconds=0;
            var producer=EcologyDefaults.Template("producer").CreateGenome();producer.Genes.RemoveAll(g=>g.DefinitionId=="gene_reproduction");
            foreach(string name in new[]{"dense400","spread400","mixed400"})
            {
                Clear();UnityEngine.Random.InitState(4382);
                for(int i=0;i<400;i++)
                {
                    var genome=RandomSpawnRecipe.Clone(producer);if(name=="mixed400"&&i%10>=7)genome.Add(GeneInstance.Create("gene_locomotion",50,0));
                    var point=name=="spread400"?new Vector3(i%20*12,350,i/20*12):new Vector3(i%20*.2f,350,i/20*.2f);
                    var entity=Spawn(genome,point);var stats=entity.GetComponent<EntityStats>();stats.TryGet("nutrient",out var food);stats.Modify("nutrient",10-food,false);stats.TryGet("storedNutrient",out var reserve);stats.Modify("storedNutrient",30-reserve,false);
                    if(i%40==39)yield return null;
                }
                yield return null;var entities=EntityRegistry.All.ToArray();var saved=entities.Select(EntitySaveState.Capture).ToArray();
                foreach(float dt in new[]{1f/60,.25f})foreach(bool merged in new[]{false,true})
                {
                    for(int i=0;i<entities.Length;i++)saved[i].Restore(entities[i]);
                    Measure(name,scheduler,entities,merged,dt);yield return null;
                }
            }
            Clear();
            string input=Path.Combine(Folder,"input/world.json");
            if(File.Exists(input))foreach(bool merged in new[]{false,true})
            {
                var world=WorldSaveStore.Read(input);JsonUtility.FromJsonOverwrite(world.terrainParameters,planet);planet.subdivisions=24;world.terrainParameters=JsonUtility.ToJson(planet);session.Open(world);
                while(session.Busy)yield return null;
                Object.FindFirstObjectByType<RandomPlanetSpawner>().enabled=false;PopulationMaintenance.Restore(null);GameplayTuning.Current.mergeStationaryEntities=merged;
                clock.RestoreState(world.gameTime,1);clock.Play();yield return new WaitForSecondsRealtime(1);
                int count=EntityRegistry.All.Count;double began=EditorApplication.timeSinceStartup,previous=began;var frames=new List<double>();
                while(EditorApplication.timeSinceStartup-began<4){yield return null;double now=EditorApplication.timeSinceStartup;frames.Add((now-previous)*1000);previous=now;}
                clock.Pause();frames.Sort();Results.Add(string.Format(CultureInfo.InvariantCulture,"saved-world merged={0} entities_start={1} entities_end={2} mean_frame_ms={3:F3} p95_frame_ms={4:F3} dormant={5}",merged,count,EntityRegistry.All.Count,frames.Average(),frames[Math.Min(frames.Count-1,(int)(frames.Count*.95))],scheduler.DormantMembers));File.WriteAllLines(Path.Combine(Folder,"samples.txt"),Results);
            }
            Results.Add("Microbenchmarks include genes and normal/shared vitals on the same simulated time. Saved-world samples include native frame/physics/UI with reduced test terrain; Editor observations are not release FPS guarantees.");
        }
    }
}
