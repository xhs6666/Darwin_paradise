using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DarwinPark.Core;
using DarwinPark.Entities;
using DarwinPark.Entities.Ecology;
using DarwinPark.Entities.Vitals;
using DarwinPark.Entities.Simulation;
using DarwinPark.Planet;
using DarwinPark.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;
namespace DarwinPark.EditorTools
{
    // ★ 新增：同一最新世界运行正常生命、环境、战斗和出生；记录真实交付模拟时间、数量及重算，不再关闭主要负载。
    [InitializeOnLoad]public static class LiveSimulationAudit
    {
        private const string Key="DarwinPark.LiveSimulationAudit";private static bool _started;private static string Folder=>Path.GetFullPath("Artifacts/LiveSimulationReview");
        static LiveSimulationAudit(){EditorApplication.update+=()=>{if(!_started&&Application.isPlaying&&SessionState.GetBool(Key,false)&&LanSession.Instance!=null){_started=true;LanSession.Instance.StartCoroutine(Guard());}};}
        public static void RunBatch(){Application.runInBackground=true;SessionState.SetBool(Key,true);EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");var view=EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));if(Array.IndexOf(System.Environment.GetCommandLineArgs(),"-auditMatchView")>=0)MatchView(view);EditorApplication.EnterPlaymode();}
        // ★ 新增：用已记录的用户Game视口尺寸对照DX12，不能把不同视野/渲染量混为同一帧数。
        private static void MatchView(EditorWindow view)
        {
            var assembly=typeof(UnityEditor.Editor).Assembly;var sizesType=assembly.GetType("UnityEditor.GameViewSizes");var sizes=sizesType.GetProperty("instance",BindingFlags.Public|BindingFlags.Static|BindingFlags.FlattenHierarchy).GetValue(null);var get=sizesType.GetMethod("GetGroup");var group=get.Invoke(sizes,new[]{Enum.Parse(get.GetParameters()[0].ParameterType,"Standalone")});var sizeType=assembly.GetType("UnityEditor.GameViewSize");var mode=assembly.GetType("UnityEditor.GameViewSizeType");var size=Activator.CreateInstance(sizeType,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance,null,new[]{Enum.Parse(mode,"FixedResolution"),(object)1237,1382,"PerformanceAudit"},null);group.GetType().GetMethod("AddCustomSize").Invoke(group,new[]{size});int count=(int)group.GetType().GetMethod("GetTotalCount").Invoke(group,null);view.GetType().GetProperty("selectedSizeIndex",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).SetValue(view,count-1);
        }
        private static IEnumerator Guard()
        {
            Directory.CreateDirectory(Folder);File.WriteAllText(Path.Combine(Folder,"result.txt"),"RUNNING");string error=null;var stack=new Stack<IEnumerator>();stack.Push(Run());
            while(stack.Count>0){bool next=false;object current=null;try{next=stack.Peek().MoveNext();if(next)current=stack.Peek().Current;}catch(Exception ex){error=ex.ToString();Debug.LogException(ex);break;}if(!next){stack.Pop();continue;}if(current is IEnumerator nested)stack.Push(nested);else yield return current;}
            File.WriteAllText(Path.Combine(Folder,"result.txt"),(error==null?"PASS":"FAIL")+"\n"+error);SessionState.SetBool(Key,false);EditorApplication.Exit(error==null?0:1);
        }
        private static Dictionary<string,long> Counters()
        {var values=new Dictionary<string,long>();var type=typeof(ActiveSimulationPlan);if(type!=null)foreach(var f in type.GetFields(BindingFlags.Public|BindingFlags.Static))if(f.FieldType==typeof(long))values[f.Name]=(long)f.GetValue(null);return values;}
        private static IEnumerator Sample(string name)
        {
            yield return new WaitForSecondsRealtime(3);int population=EntityRegistry.All.Count;float simulationStart=ServiceLocator.Get<SimulationClock>().GameTime;var before=Counters();double began=EditorApplication.timeSinceStartup,previous=began;using var sample=new PerformanceSampleSession();
            while(EditorApplication.timeSinceStartup-began<8){yield return null;double now=EditorApplication.timeSinceStartup;sample.RecordFrame((now-previous)*1000);previous=now;}
            var report=sample.Finish(name+" population="+population+"->"+EntityRegistry.All.Count+" api="+SystemInfo.graphicsDeviceType+" resolution="+Screen.width+"x"+Screen.height+" live ecology enabled cap=0");File.WriteAllText(Path.Combine(Folder,name+".json"),JsonUtility.ToJson(report,true));
            var after=Counters();File.WriteAllLines(Path.Combine(Folder,name+"-counters.txt"),after.Select(p=>p.Key+"="+(p.Value-(before.TryGetValue(p.Key,out long value)?value:0))));var bodies=EntityRegistry.All.Select(e=>e.GetComponent<Rigidbody>()).Where(b=>b!=null).ToArray();File.WriteAllText(Path.Combine(Folder,name+"-bodies.txt"),"total="+bodies.Length+" sleeping="+bodies.Count(b=>b.IsSleeping())+" kinematic="+bodies.Count(b=>b.isKinematic));File.WriteAllText(Path.Combine(Folder,name+"-simulation.txt"),"simulated="+(ServiceLocator.Get<SimulationClock>().GameTime-simulationStart)+" population="+population+"->"+EntityRegistry.All.Count+" resources="+ColonyResource.All.Count());
        }
        private static IEnumerator Run()
        {
            var world=WorldSaveStore.Read(Path.Combine(Folder,"input/world.json"));var session=Object.FindFirstObjectByType<WorldSessionManager>();session.autoSaveInterval=36000;session.Open(world);double started=EditorApplication.timeSinceStartup;while(session.Busy){if(EditorApplication.timeSinceStartup-started>300)throw new TimeoutException("load");yield return null;}
            var clock=ServiceLocator.Get<SimulationClock>();clock.Pause();FramePacingSettings.SetLimit(0);UnityEngine.Random.InitState(45882);clock.SetTimeScale(1);if(Array.IndexOf(System.Environment.GetCommandLineArgs(),"-auditNoReuse")>=0)SimulationOptimization.Current.activeComponentReuse=false;clock.Play();yield return Sample("live");clock.Pause();
        }
    }
}
