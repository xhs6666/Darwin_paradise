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
    // ★ 新增：同一世界实测远景、近景及真实操控眼位，逐项隔离地表、天气、灯光与活动区；记录GPU而非只看基因计时。
    [InitializeOnLoad]public static class NearViewPerformanceAudit
    {
        private const string Key="DarwinPark.NearViewPerformanceAudit";private static bool _started,_protected;private static string Folder=>Path.GetFullPath("Artifacts/NearViewReview");
        static NearViewPerformanceAudit(){EditorApplication.update+=()=>{if(!_started&&Application.isPlaying&&SessionState.GetBool(Key,false)&&LanSession.Instance!=null){_started=true;LanSession.Instance.StartCoroutine(Guard());}};}
        public static void RunBatch(){Application.runInBackground=true;SessionState.SetBool(Key,true);EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");var view=EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));if(Array.IndexOf(System.Environment.GetCommandLineArgs(),"-auditMatchView")>=0)MatchView(view);EditorApplication.EnterPlaymode();}
        // ★ 新增：用已记录的用户Game视口尺寸对照DX12，不能把不同视野/渲染量混为同一帧数。
        private static void MatchView(EditorWindow view)
        {
            var assembly=typeof(UnityEditor.Editor).Assembly;var sizesType=assembly.GetType("UnityEditor.GameViewSizes");var sizes=sizesType.GetProperty("instance",BindingFlags.Public|BindingFlags.Static|BindingFlags.FlattenHierarchy).GetValue(null);var get=sizesType.GetMethod("GetGroup");var group=get.Invoke(sizes,new[]{Enum.Parse(get.GetParameters()[0].ParameterType,"Standalone")});var sizeType=assembly.GetType("UnityEditor.GameViewSize");var mode=assembly.GetType("UnityEditor.GameViewSizeType");var size=Activator.CreateInstance(sizeType,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance,null,new[]{Enum.Parse(mode,"FixedResolution"),(object)(File.Exists(Path.Combine(Folder,"user-report.json"))?2560:1237),(File.Exists(Path.Combine(Folder,"user-report.json"))?1385:1382),"PerformanceAudit"},null);group.GetType().GetMethod("AddCustomSize").Invoke(group,new[]{size});int count=(int)group.GetType().GetMethod("GetTotalCount").Invoke(group,null);view.GetType().GetProperty("selectedSizeIndex",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).SetValue(view,count-1);
        }
        private static IEnumerator Guard()
        {
            Directory.CreateDirectory(Folder);File.WriteAllText(Path.Combine(Folder,"result.txt"),"RUNNING");string error=null;var stack=new Stack<IEnumerator>();stack.Push(Run());
            while(stack.Count>0){bool next=false;object current=null;try{next=stack.Peek().MoveNext();if(next)current=stack.Peek().Current;}catch(Exception ex){error=ex.ToString();Debug.LogException(ex);break;}if(!next){stack.Pop();continue;}if(current is IEnumerator nested)stack.Push(nested);else yield return current;}
            File.WriteAllText(Path.Combine(Folder,"result.txt"),(error==null?"PASS":"FAIL")+"\n"+error);SessionState.SetBool(Key,false);EditorApplication.Exit(error==null?0:1);
        }
        private static IEnumerator Sample(string name,bool warm=true,Action transition=null)
        {
            if(warm)yield return new WaitForSecondsRealtime(.8f);int initialPopulation=EntityRegistry.All.Count;double began=EditorApplication.timeSinceStartup,previous=began;using var sample=new PerformanceSampleSession();
            transition?.Invoke();
            while(EditorApplication.timeSinceStartup-began<3){yield return null;double now=EditorApplication.timeSinceStartup;sample.RecordFrame((now-previous)*1000);previous=now;}
            var camera=Camera.main;var report=sample.Finish($"{name} api={SystemInfo.graphicsDeviceType} size={Screen.width}x{Screen.height} initialEntities={initialPopulation} spawnAttempts={Object.FindFirstObjectByType<RandomPlanetSpawner>().PlacementAttempts} globalFallbacks={Object.FindFirstObjectByType<RandomPlanetSpawner>().GlobalFallbacks} slices={Object.FindFirstObjectByType<RandomPlanetSpawner>().YieldedSlices} entities={EntityRegistry.All.Count} resources={ColonyResource.All.Count()} running={ServiceLocator.Get<SimulationClock>().IsRunning} speed={ServiceLocator.Get<SimulationClock>().ConfiguredTimeScale} controlledProtection={(_protected?1:0)} circles={PopulationRangeLayer.ActiveCircleCount} eye={camera.transform.position} near={camera.nearClipPlane}");File.WriteAllText(Path.Combine(Folder,name+".json"),JsonUtility.ToJson(report,true));File.WriteAllText(Path.Combine(Folder,name+"-gpu.txt"),report.metrics.First(m=>m.name=="GPU Frame Time") is var gpu&&gpu.available?$"samples={gpu.samples} mean={gpu.mean:R} max={gpu.maximum:R}":"GPU unavailable");
            if(name.Contains("baseline")){yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(Folder,name+".png"));yield return null;}
        }
        private static IEnumerator Run()
        {
            var world=WorldSaveStore.Read(Path.Combine(Folder,"input/world.json"));
            // ★ 新增：数量阈值审计保留王后优先的嵌套子集，停用自动投放与自然繁殖仅用于独立测试，不写回原存档。
            var args=System.Environment.GetCommandLineArgs();int countArg=Array.IndexOf(args,"-populationCount");if(countArg>=0){int keep=int.Parse(args[countArg+1]);world.entities=world.entities.OrderByDescending(e=>e.ecology?.ant?.role=="Queen").Take(keep).ToList();world.running=false;}
            var session=Object.FindFirstObjectByType<WorldSessionManager>();session.autoSaveInterval=36000;session.Open(world);while(session.Busy)yield return null;
            var clock=ServiceLocator.Get<SimulationClock>();clock.Pause();FramePacingSettings.SetLimit(0);var camera=Camera.main;var orbit=camera.GetComponent<OrbitCamera>();orbit.enabled=false;var planet=EcologyQueries.Planet;var weather=planet.GetComponent<PlanetWeatherVisuals>();weather.cloudSpeed=0;PopulationRangeLayer.SetVisible(false);
            // ★ 新增：用户现场报告复查使用原镜头与实际视口，保留完整生态，不用先前413实体世界替代新495实体现场。
            if(Array.IndexOf(System.Environment.GetCommandLineArgs(),"-userReportOnly")>=0)
            {
                var saved=JsonUtility.FromJson<PerformanceReport>(File.ReadAllText(Path.Combine(Folder,"user-report.json")));var capturedView=saved.viewWorst??saved.viewStart;camera.transform.SetPositionAndRotation(capturedView.position,capturedView.rotation);clock.SetTimeScale(1);
                File.WriteAllText(Path.Combine(Folder,"profiler-state.txt"),"enabled="+UnityEngine.Profiling.Profiler.enabled+" deep="+UnityEditorInternal.ProfilerDriver.deepProfiling);
                // ★ 新增：阈值本身不保证该三秒窗口有投放，显式保留12个生产者缺口的压力对照，所有变更仅存在测试副本内存。
                if(Array.IndexOf(args,"-forceDeficit")>=0)
                {
                    var automatic=Object.FindFirstObjectByType<RandomPlanetSpawner>();automatic.Cancel();automatic.enabled=false;if(Array.IndexOf(args,"-rejectLanding")>=0)automatic.landClearance=100000;var rules=PopulationMaintenance.Capture();var producerRule=rules.rules.First(r=>r.category==PopulationRuleCategory.Producers);producerRule.enabled=true;producerRule.minimumCount=100000;PopulationMaintenance.Restore(rules);producerRule=PopulationMaintenance.Rules.First(r=>r.id==producerRule.id);
                    bool started=(bool)typeof(RandomPlanetSpawner).GetMethod("StartInternal",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(automatic,new object[]{producerRule.source,12,producerRule.landOnly,producerRule});if(!started)throw new InvalidOperationException("强制投放缺口未启动");
                }
                if(Array.IndexOf(args,"-noAutoSpawn")>=0){var automatic=Object.FindFirstObjectByType<RandomPlanetSpawner>();automatic.Cancel();automatic.enabled=false;}
                if(Array.IndexOf(args,"-noNaturalBirth")>=0)
                {
                    foreach(var actor in EntityRegistry.All)
                    {
                        foreach(var gene in actor.Genes.Genome.Genes)if(actor.Genes.GetLogic(gene) is DarwinPark.Entities.Logic.ReproductionGeneLogic logic)typeof(DarwinPark.Entities.Logic.ReproductionGeneLogic).GetField("_cooldownTimer",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(logic,100000f);
                        var member=actor.GetComponent<AntColonyMember>();if(member!=null){member.State.reproductionTimer=100000;member.State.reproductionCooldown=100000;}
                    }
                }
                bool trace=Array.IndexOf(System.Environment.GetCommandLineArgs(),"-noHierarchy")<0;if(trace)UserFrameHierarchyAudit.Start();clock.Play();yield return Sample("user-running",Array.IndexOf(args,"-forceDeficit")<0);if(trace)UserFrameHierarchyAudit.Finish(Folder);clock.Pause();yield return Sample("user-paused");yield break;
            }
            var target=EntityRegistry.All.Where(e=>EcologyQueries.Alive(e)&&e.Genes.GetAttribute("moveSpeed")>0&&!planet.IsSubmerged(e.transform.position)).OrderBy(e=>(e.transform.position-camera.transform.position).sqrMagnitude).First();Vector3 point=target.transform.position,center=planet.transform.position,up=(point-center).normalized;var rotation=Quaternion.LookRotation(-up,Mathf.Abs(up.y)>.99f?Vector3.right:Vector3.up);
            // ★ 新增：在切换动作前启动计时，测首次镜头切换，不能用预热后的稳态掩盖进入控制的尖峰。
            if(Array.IndexOf(System.Environment.GetCommandLineArgs(),"-coldOnly")>=0)
            {
                _protected=true;target.GetComponent<EntityLifeCycle>().RestoreProgress(40);var health=target.GetComponent<DarwinPark.Entities.Stats.EntityStats>();health.SetMax("health",1000000);health.Modify("health",1000000,false);clock.SetTimeScale(1);camera.transform.SetPositionAndRotation(center+up*800,rotation);clock.Play();yield return Sample("first-far-running");
                yield return Sample("first-near-transition",false,()=>camera.transform.SetPositionAndRotation(point+up*10,rotation));var control=Object.FindFirstObjectByType<EntityViewController>();yield return Sample("first-control-transition",false,()=>{if(!control.EnterView(target))throw new InvalidOperationException("first control rejected");});clock.Pause();control.ExitView();yield break;
            }
            camera.transform.SetPositionAndRotation(center+up*800,rotation);yield return Sample("far-paused-baseline");camera.transform.SetPositionAndRotation(point+up*10,rotation);yield return Sample("near-paused-baseline");clock.Play();yield return Sample("near-running");clock.Pause();
            // ★ 新增：输入压力样本只保护被控的一个实体，防止自然死亡切镜头污染控制结果；其余实体仍正常生态。
            _protected=true;target.GetComponent<EntityLifeCycle>().RestoreProgress(40);var protectedStats=target.GetComponent<DarwinPark.Entities.Stats.EntityStats>();protectedStats.SetMax("health",1000000);protectedStats.Modify("health",1000000,false);
            // ★ 新增：仅Editor原型对比补算预算的交互/吞吐权衡，绝不默认改变正式游戏倍率或丢弃欠账。
            if(Array.IndexOf(System.Environment.GetCommandLineArgs(),"-budgetOnly")>=0)
            {
                camera.transform.SetPositionAndRotation(point+up*10,rotation);clock.SetTimeScale(16);clock.Play();yield return Sample("budget-05");clock.Pause();clock.maximumGameDelta=.1f;clock.Play();yield return Sample("budget-01");clock.Pause();yield break;
            }
            var view=Object.FindFirstObjectByType<EntityViewController>();if(!view.EnterView(target))throw new InvalidOperationException("control rejected");var input=view.GetComponent<EntityPossessionInput>();var pitch=typeof(EntityPossessionInput).GetField("_pitch",BindingFlags.NonPublic|BindingFlags.Instance);pitch.SetValue(input,0f);yield return Sample("control-horizon-paused-baseline");clock.Play();yield return Sample("control-horizon-running");clock.Pause();pitch.SetValue(input,65f);yield return Sample("control-ground-paused-baseline");pitch.SetValue(input,-45f);yield return Sample("control-sky-paused-baseline");pitch.SetValue(input,0f);
            var fixture=view.gameObject.AddComponent<NearViewInputFixture>();fixture.view=view;fixture.move=true;clock.Play();yield return Sample("control-moving-running");clock.Pause();fixture.move=false;fixture.attack=true;clock.Play();yield return Sample("control-attacking-running");clock.Pause();fixture.attack=false;Object.DestroyImmediate(fixture);if(!orbit.EntityViewActive)throw new InvalidOperationException("控制目标在输入样本中死亡，不能把退出镜头当控制结果");
            Object.FindFirstObjectByType<GeneInspectorUI>().ShowEntityInfo(target);yield return Sample("control-inspector-paused");Object.FindFirstObjectByType<GeneInspectorUI>().HideEntityInfo();
            var scheduler=Object.FindFirstObjectByType<ResourceVisualScheduler>();var lights=ColonyResource.All.SelectMany(r=>r.GetComponents<Light>()).ToArray();var lightStates=lights.Select(l=>l.enabled).ToArray();scheduler.enabled=false;foreach(var l in lights)l.enabled=false;yield return Sample("control-no-resource-lights");for(int i=0;i<lights.Length;i++)lights[i].enabled=lightStates[i];scheduler.enabled=true;
            var shells=planet.GetComponentsInChildren<Renderer>(true).Where(r=>r.name=="AtmosphericScattering"||r.name=="OceanSurface"||r.name=="CloudVolume").ToArray();var shellStates=shells.Select(r=>r.enabled).ToArray();foreach(var r in shells)r.enabled=false;yield return Sample("control-no-weather");for(int i=0;i<shells.Length;i++)shells[i].enabled=shellStates[i];
            var surface=planet.GetComponent<MeshRenderer>();var original=surface.sharedMaterial;var simple=new Material(Shader.Find("Universal Render Pipeline/Unlit"));surface.sharedMaterial=simple;yield return Sample("control-simple-terrain");surface.sharedMaterial=original;Object.Destroy(simple);
            PopulationRangeLayer.SetVisible(true);yield return Sample("control-population");PopulationRangeLayer.SetVisible(false);
            var pipeline=UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;var scale=pipeline.GetType().GetProperty("renderScale");if(scale!=null){object old=scale.GetValue(pipeline);scale.SetValue(pipeline,.5f);yield return Sample("control-half-resolution");scale.SetValue(pipeline,old);}view.ExitView();
            orbit.RestoreView(center+up*800,rotation,800);orbit.enabled=true;clock.Play();orbit.FocusOn(point);yield return Sample("zoom-transition-running");clock.Pause();orbit.enabled=false;camera.transform.SetPositionAndRotation(point+up*10,rotation);clock.SetTimeScale(16);clock.Play();yield return Sample("near-running-16x");clock.Pause();
        }
    }
    // ★ 新增：只在Editor测试中模拟持续移动/攻击，使用与真实输入相同的手动接口，不修改游戏输入或实体基因。
    [DefaultExecutionOrder(-150)]public sealed class NearViewInputFixture:MonoBehaviour
    {
        public EntityViewController view;public bool move,attack;
        private void Apply(){if(view==null||view.Target==null||!ServiceLocator.Get<SimulationClock>().IsRunning)return;var manual=view.Target.GetComponent<EntityManualControl>();var camera=Camera.main;manual.SetAim(new Ray(camera.transform.position,camera.transform.forward));manual.SetInput(move?camera.transform.forward:Vector3.zero,attack);}
        private void Update()=>Apply();private void FixedUpdate()=>Apply();
    }
}
