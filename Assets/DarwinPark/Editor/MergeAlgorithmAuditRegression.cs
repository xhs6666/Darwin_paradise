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
using DarwinPark.Entities.Vitals;
using DarwinPark.Planet;
using DarwinPark.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;
namespace DarwinPark.EditorTools
{
    // ★ 新增：冻结同一最新存档，分别控制移动活体、尸体渲染和资源数量，检查实际共享成功率、欠账与运行帧峰值。
    [InitializeOnLoad]public static class MergeAlgorithmAuditRegression
    {
        private const string Key="DarwinPark.MergeAlgorithmAudit";private static bool _started;private static readonly List<string> Results=new();
        private static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;private static string Folder=>Path.GetFullPath("Artifacts/MergeAlgorithmAudit");
        static MergeAlgorithmAuditRegression(){EditorApplication.update+=()=>{if(!_started&&Application.isPlaying&&SessionState.GetBool(Key,false)&&LanSession.Instance!=null){_started=true;LanSession.Instance.StartCoroutine(Guard());}};}
        public static void RunBatch(){Application.runInBackground=true;SessionState.SetBool(Key,true);EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");EditorApplication.EnterPlaymode();}
        private static IEnumerator Guard()
        {
            Directory.CreateDirectory(Folder);File.WriteAllText(Path.Combine(Folder,"result.txt"),"RUNNING");string error=null;var stack=new Stack<IEnumerator>();stack.Push(Run());
            while(stack.Count>0){bool next=false;object current=null;try{next=stack.Peek().MoveNext();if(next)current=stack.Peek().Current;}catch(Exception ex){error=ex.ToString();Debug.LogException(ex);break;}if(!next){stack.Pop();continue;}if(current is IEnumerator nested)stack.Push(nested);else yield return current;}
            File.WriteAllText(Path.Combine(Folder,"result.txt"),(error==null?"PASS":"FAIL")+"\n"+error+"\n"+string.Join("\n",Results));SessionState.SetBool(Key,false);EditorApplication.Exit(error==null?0:1);
        }
        private static string Audit(EntityWorkScheduler scheduler)
        {
            var reasons=new Dictionary<string,int>();void Add(string key){reasons.TryGetValue(key,out int count);reasons[key]=count+1;}
            var entries=(IDictionary)typeof(EntityWorkScheduler).GetField("_entries",Private).GetValue(scheduler);
            int count=0,signatures=0;var ids=new HashSet<int>();float quiet=0,maxPending=0;
            foreach(object entry in entries.Values)
            {
                var type=entry.GetType();object Field(string key)=>type.GetField(key).GetValue(entry);
                var genes=(EntityGeneController)Field("genes");if(genes==null||genes.GetAttribute("moveSpeed")>0)continue;count++;
                ids.Add((int)Field("signature"));quiet+=(float)Field("quiet");maxPending=Mathf.Max(maxPending,genes.PendingSimulationTime);
                if(!GameplayTuning.Current.mergeStationaryEntities){Add("merge-disabled");continue;}
                if((bool)Field("dormant")){Add("dormant");continue;}
                var stats=genes.GetComponent<EntityStats>();var hazard=genes.GetComponent<EntityEnvironmentHazard>();
                if(!(bool)Field("supported")){var types=genes.Genome.Genes.Select(genes.GetLogic).Where(l=>l!=null).Select(l=>l.GetType().Name).Distinct();Add("unsupported:"+string.Join(",",types));}
                else if(stats.CapacityRemaining("health")>0)Add("injured");else if(genes.EnvironmentGeneSuppression>0)Add("suppressed");else if(hazard.StressLevel>0||hazard.IsDrowning)Add("environment");else if((float)Field("quiet")<GameplayTuning.Current.mergeQuietSeconds)Add("quiet/moving");else Add("other");
            }
            signatures=ids.Count;return "static="+count+" signatures="+signatures+" quietMean="+(quiet/Math.Max(1,count)).ToString("F3")+" maxPending="+maxPending.ToString("F3")+" reasons="+string.Join(";",reasons.Select(p=>p.Key+":"+p.Value));
        }
        private static IEnumerator Run()
        {
            string original=File.ReadAllText(Path.Combine(Folder,"input/world.json"));
            foreach(string name in new[]{"all-on","static-on","static-off","static-render-off","static300-no-resources-on","static300-no-resources-off"})
            {
                var world=JsonUtility.FromJson<WorldSaveData>(original);bool onlyStatic=name!="all-on",merge=!name.EndsWith("-off")||name=="static-render-off";
                if(onlyStatic)world.entities=world.entities.Where(e=>!e.genome.Genes.Any(g=>g.DefinitionId=="gene_locomotion")).ToList();
                if(name.Contains("300")){world.entities=world.entities.Take(300).ToList();world.colony.resources.Clear();}
                var session=Object.FindFirstObjectByType<WorldSessionManager>();session.autoSaveInterval=36000;session.Open(world);double start=EditorApplication.timeSinceStartup;while(session.Busy){if(EditorApplication.timeSinceStartup-start>300)throw new TimeoutException("load "+name);yield return null;}
                var clock=ServiceLocator.Get<SimulationClock>();clock.Pause();Object.FindFirstObjectByType<RandomPlanetSpawner>().enabled=false;PopulationMaintenance.Restore(null);EntityPopulationLimit.UseSnapshot(new WorldGameSettings{entityLimit=EntityRegistry.All.Count});FramePacingSettings.SetLimit(0);GameplayTuning.Current.mergeStationaryEntities=merge;
                foreach(var entity in EntityRegistry.All){entity.Genes.mutationChance=0;var life=entity.GetComponent<EntityLifeCycle>();life.RestoreProgress(Mathf.Min(life.Progress,LifeCycleSettings.Load().deathProgress-10));}
                if(name=="static-render-off")foreach(var resource in ColonyResource.All)foreach(var renderer in resource.GetComponentsInChildren<Renderer>())renderer.enabled=false;
                clock.RestoreState(world.gameTime,1);clock.Play();yield return new WaitForSecondsRealtime(3);
                var scheduler=Object.FindFirstObjectByType<EntityWorkScheduler>();long shared=scheduler.SharedMemberSteps,reps=scheduler.RepresentativeSteps;int count=EntityRegistry.All.Count,frames=0;double dormant=0;start=EditorApplication.timeSinceStartup;double previous=start;using var sample=new PerformanceSampleSession();
                while(EditorApplication.timeSinceStartup-start<6){yield return null;double now=EditorApplication.timeSinceStartup;sample.RecordFrame((now-previous)*1000);previous=now;dormant+=scheduler.DormantMembers;frames++;}
                var audit=Audit(scheduler);var report=sample.Finish(name+" entities="+count+"->"+EntityRegistry.All.Count+" resources="+ColonyResource.All.Count()+" merge="+merge+" dormantMean="+(dormant/Math.Max(1,frames)).ToString("F1")+" reps="+(scheduler.RepresentativeSteps-reps)+" shared="+(scheduler.SharedMemberSteps-shared)+" "+audit);clock.Pause();
                File.WriteAllText(Path.Combine(Folder,name+".json"),JsonUtility.ToJson(report,true));Results.Add(report.context+" mean="+report.metrics[0].mean.ToString("F3")+" p95="+report.metrics[0].p95.ToString("F3"));File.WriteAllLines(Path.Combine(Folder,"progress.txt"),Results);
            }
        }
    }
}
