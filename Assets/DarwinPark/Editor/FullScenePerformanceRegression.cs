using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DarwinPark.Core;
using DarwinPark.Entities;
using DarwinPark.Entities.Ecology;
using DarwinPark.Planet;
using DarwinPark.UI;
using Unity.Profiling;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;
namespace DarwinPark.EditorTools
{
    // ★ 新增：原始地形和固定数量下分离脚本、物理、资源渲染与镜头成本，不再用缩小地形的微基准代替现场。
    [InitializeOnLoad]public static class FullScenePerformanceRegression
    {
        private const string Key="DarwinPark.FullScenePerformance";private static bool _started;private static readonly List<string> Results=new();
        private static string Folder=>Path.GetFullPath("Artifacts/FullScenePerformanceReview");
        static FullScenePerformanceRegression(){EditorApplication.update+=()=>{if(!_started&&Application.isPlaying&&SessionState.GetBool(Key,false)&&LanSession.Instance!=null){_started=true;LanSession.Instance.StartCoroutine(Guard());}};}
        public static void RunBatch(){Application.runInBackground=true;SessionState.SetBool(Key,true);EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");EditorApplication.EnterPlaymode();}
        private static IEnumerator Guard()
        {
            Directory.CreateDirectory(Folder);File.WriteAllText(Path.Combine(Folder,"result.txt"),"RUNNING");string error=null;var stack=new Stack<IEnumerator>();stack.Push(Run());
            while(stack.Count>0){bool next=false;object current=null;try{next=stack.Peek().MoveNext();if(next)current=stack.Peek().Current;}catch(Exception ex){error=ex.ToString();Debug.LogException(ex);break;}if(!next){stack.Pop();continue;}if(current is IEnumerator nested)stack.Push(nested);else yield return current;}
            File.WriteAllText(Path.Combine(Folder,"result.txt"),(error==null?"PASS":"FAIL")+"\n"+error+"\n"+string.Join("\n",Results));SessionState.SetBool(Key,false);EditorApplication.Exit(error==null?0:1);
        }
        private static IEnumerator Sample(string name)
        {
            yield return new WaitForSecondsRealtime(1);int count=EntityRegistry.All.Count;using var sample=new PerformanceSampleSession();double began=EditorApplication.timeSinceStartup,previous=began;
            while(EditorApplication.timeSinceStartup-began<5){yield return null;double now=EditorApplication.timeSinceStartup;sample.RecordFrame((now-previous)*1000);previous=now;}
            var report=sample.Finish("entities="+count+"->"+EntityRegistry.All.Count+" resources="+ColonyResource.All.Count()+" resolution="+Screen.width+"x"+Screen.height+" editor full saved terrain");
            File.WriteAllText(Path.Combine(Folder,name+".json"),JsonUtility.ToJson(report,true));var frame=report.metrics[0];Results.Add(name+": mean="+frame.mean.ToString("F3")+" p95="+frame.p95.ToString("F3")+" "+report.context);File.WriteAllLines(Path.Combine(Folder,"progress.txt"),Results);
            if(EntityRegistry.All.Count!=count)throw new InvalidOperationException("性能采样实体数量发生改变");
        }
        private static string Allocations(string name,Action action)
        {
            using var recorder=new ProfilerRecorder(ProfilerCategory.Internal,"GC.Alloc",65536,ProfilerRecorderOptions.CollectOnlyOnCurrentThread);
            if(!recorder.Valid)return name+": unavailable";recorder.Start();action();recorder.Stop();long bytes=0;for(int i=0;i<recorder.Count;i++)bytes+=recorder.GetSample(i).Value;
            return name+": bytes="+bytes+" events="+recorder.Count+" overflow="+recorder.WrappedAround;
        }
        private static IEnumerator Run()
        {
            var session=Object.FindFirstObjectByType<WorldSessionManager>();session.autoSaveInterval=36000;var world=WorldSaveStore.Read(Path.Combine(Folder,"input/world.json"));session.Open(world);double began=EditorApplication.timeSinceStartup;while(session.Busy){if(EditorApplication.timeSinceStartup-began>300)throw new TimeoutException("完整地形加载超时");yield return null;}
            var clock=ServiceLocator.Get<SimulationClock>();clock.Pause();Object.FindFirstObjectByType<RandomPlanetSpawner>().enabled=false;PopulationMaintenance.Restore(null);EntityPopulationLimit.UseSnapshot(new WorldGameSettings{entityLimit=EntityRegistry.All.Count});
            FramePacingSettings.SetLimit(0);
            foreach(var e in EntityRegistry.All){e.Genes.mutationChance=0;var life=e.GetComponent<EntityLifeCycle>();life.RestoreProgress(Mathf.Min(life.Progress,LifeCycleSettings.Load().deathProgress-10));}
            var entities=EntityRegistry.All.ToArray();var states=entities.Select(EntitySaveState.Capture).ToArray();var resources=ColonyResource.All.SelectMany(r=>r.GetComponentsInChildren<Renderer>()).ToArray();
            void Restore(){clock.Pause();for(int i=0;i<entities.Length;i++)states[i].Restore(entities[i]);clock.RestoreState(world.gameTime,1);}
            var probes=new List<string>();var scheduler=Object.FindFirstObjectByType<DarwinPark.Entities.Simulation.EntityWorkScheduler>();
            probes.Add(Allocations("scheduler",()=>scheduler.Advance(1f/60)));
            probes.Add(Allocations("all-genes",()=>{foreach(var e in entities)e.Genes.AdvanceSimulation(1f/60);}));
            probes.Add(Allocations("nutrition",()=>{foreach(var e in entities)foreach(var gene in e.Genes.Genome.Genes)if(e.Genes.GetLogic(gene) is DarwinPark.Entities.Logic.NutritionGeneLogic n)n.OnUpdate(1f/60);}));
            probes.Add(Allocations("population",()=>{foreach(var e in entities)e.GetComponent<PopulationBehavior>()?.Advance(1f/60);}));
            var inspector=Object.FindFirstObjectByType<GeneInspectorUI>();var inspectUpdate=typeof(GeneInspectorUI).GetMethod("Update",BindingFlags.NonPublic|BindingFlags.Instance);
            probes.Add(Allocations("inspector",()=>inspectUpdate.Invoke(inspector,null)));File.WriteAllLines(Path.Combine(Folder,"allocations.txt"),probes);
            Restore();clock.Play();yield return Sample("01-full");if(File.Exists(Path.Combine(Folder,"quick.flag"))){FramePacingSettings.SetLimit(30);Restore();clock.Play();yield return Sample("08-lock30");yield break;}
            clock.Pause();yield return Sample("02-paused");
            var previous=Physics.simulationMode;Physics.simulationMode=SimulationMode.Script;yield return Sample("03-paused-no-physics");
            foreach(var renderer in resources)renderer.enabled=false;yield return Sample("04-paused-no-resource-render");
            Physics.simulationMode=previous;Restore();clock.Play();yield return Sample("05-running-no-resource-render");
            foreach(var renderer in resources)renderer.enabled=true;foreach(var renderer in resources)if(renderer.gameObject.name=="CorpseStatusMark")renderer.enabled=false;
            Restore();clock.Play();yield return Sample("06-running-no-corpse-cross");
            clock.Pause();var cameras=Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Where(c=>c.enabled).ToArray();foreach(var camera in cameras)camera.enabled=false;yield return Sample("07-paused-no-camera");
            foreach(var camera in cameras)camera.enabled=true;
        }
    }
}
