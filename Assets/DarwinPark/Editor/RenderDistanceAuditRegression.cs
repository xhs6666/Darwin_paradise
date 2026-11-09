using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DarwinPark.Core;
using DarwinPark.Entities;
using DarwinPark.Entities.Ecology;
using DarwinPark.Planet;
using DarwinPark.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;
namespace DarwinPark.EditorTools
{
    // ★ 新增：固定同一世界/分辨率/远近镜头，暂停模拟逐项隔离渲染，不能用实体计算耗时解释所有近景掉帧。
    [InitializeOnLoad]public static class RenderDistanceAuditRegression
    {
        private const string Key="DarwinPark.RenderDistance";private static bool _started;
        private static string Folder=>Path.GetFullPath("Artifacts/RulesRenderReview");
        static RenderDistanceAuditRegression(){EditorApplication.update+=()=>{if(!_started&&Application.isPlaying&&SessionState.GetBool(Key,false)&&LanSession.Instance!=null){_started=true;LanSession.Instance.StartCoroutine(Guard());}};}
        public static void RunBatch(){Application.runInBackground=true;SessionState.SetBool(Key,true);EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");var view=EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));view.position=new Rect(20,20,1920,1080);EditorApplication.EnterPlaymode();}
        private static IEnumerator Guard()
        {
            Directory.CreateDirectory(Folder);File.WriteAllText(Path.Combine(Folder,"result.txt"),"RUNNING");string error=null;var stack=new Stack<IEnumerator>();stack.Push(Run());
            while(stack.Count>0){bool next=false;object current=null;try{next=stack.Peek().MoveNext();if(next)current=stack.Peek().Current;}catch(Exception ex){error=ex.ToString();Debug.LogException(ex);break;}if(!next){stack.Pop();continue;}if(current is IEnumerator nested)stack.Push(nested);else yield return current;}
            File.WriteAllText(Path.Combine(Folder,"result.txt"),(error==null?"PASS":"FAIL")+"\n"+error);SessionState.SetBool(Key,false);EditorApplication.Exit(error==null?0:1);
        }
        private static IEnumerator Sample(string name,bool warm=true)
        {
            if(warm)yield return new WaitForSecondsRealtime(.7f);double start=EditorApplication.timeSinceStartup,previous=start;using var sample=new PerformanceSampleSession();var gpu=new List<double>();var timing=new FrameTiming[1];
            while(EditorApplication.timeSinceStartup-start<2){FrameTimingManager.CaptureFrameTimings();yield return null;double now=EditorApplication.timeSinceStartup;sample.RecordFrame((now-previous)*1000);previous=now;if(FrameTimingManager.GetLatestTimings(1,timing)>0&&timing[0].gpuFrameTime>0)gpu.Add(timing[0].gpuFrameTime);}
            var report=sample.Finish($"{name} screen={Screen.width}x{Screen.height} api={SystemInfo.graphicsDeviceType} gpu={SystemInfo.graphicsDeviceName} circles={PopulationRangeLayer.ActiveCircleCount} entities={EntityRegistry.All.Count} resources={ColonyResource.All.Count()} running={ServiceLocator.Get<SimulationClock>().IsRunning} proxy={SimulationOptimization.Current.weatherProxyMeshes}");File.WriteAllText(Path.Combine(Folder,name+".json"),JsonUtility.ToJson(report,true));File.WriteAllText(Path.Combine(Folder,name+"-gpu.txt"),gpu.Count>0?"samples="+gpu.Count+" mean="+gpu.Average()+" max="+gpu.Max():"GPU timing unavailable");
            if(name.Contains("baseline")){yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(Folder,name+".png"));yield return null;}
        }
        private static IEnumerator Run()
        {
            var world=JsonUtility.FromJson<WorldSaveData>(File.ReadAllText(Path.Combine(Folder,"input/world.json")));var session=Object.FindFirstObjectByType<WorldSessionManager>();session.autoSaveInterval=36000;session.Open(world);double began=EditorApplication.timeSinceStartup;while(session.Busy){if(EditorApplication.timeSinceStartup-began>300)throw new TimeoutException("load");yield return null;}
            var clock=ServiceLocator.Get<SimulationClock>();clock.Pause();Object.FindFirstObjectByType<RandomPlanetSpawner>().enabled=false;PopulationMaintenance.Restore(null);FramePacingSettings.SetLimit(0);
            var camera=Camera.main;camera.GetComponent<OrbitCamera>().enabled=false;var planet=EcologyQueries.Planet;
            // ★ 修复（防Bug回归）：云随真实时间旋转，必须固定云相位，避免两轮加载耗时不同被当作渲染收益。
            planet.GetComponent<PlanetWeatherVisuals>().cloudSpeed=0;planet.transform.Find("CloudVolume").localRotation=Quaternion.identity;
            var center=planet.transform.position;var target=EntityRegistry.All.Where(e=>!planet.IsSubmerged(e.transform.position)).OrderBy(e=>(e.transform.position-camera.transform.position).sqrMagnitude).First();var point=target.transform.position;var radial=(point-center).normalized;var rotation=Quaternion.LookRotation(-radial,Mathf.Abs(radial.y)>.99f?Vector3.right:Vector3.up);
            File.WriteAllText(Path.Combine(Folder,"context.txt"),$"saved={world.savedUtc} entities={EntityRegistry.All.Count} resources={ColonyResource.All.Count()} point={point} radius={planet.radius} meshTriangles={planet.Data.Triangles.Length/3}");
            PopulationRangeLayer.SetVisible(false);camera.transform.SetPositionAndRotation(center+radial*800,rotation);yield return Sample("far-baseline");camera.transform.SetPositionAndRotation(center+radial*(Vector3.Distance(point,center)+10),rotation);yield return Sample("near-baseline");
            PopulationRangeLayer.SetVisible(true);yield return Sample("near-population");PopulationRangeLayer.SetVisible(false);
            // ★ 新增：同一镜头恢复模拟，区分近景渲染与活体计算；报告同时记录样本末尾数量，避免误称完全静态样本。
            clock.Play();yield return Sample("near-running");clock.Pause();
            camera.transform.SetPositionAndRotation(center+radial*800,rotation);clock.Play();yield return Sample("far-running");clock.Pause();camera.transform.SetPositionAndRotation(center+radial*(Vector3.Distance(point,center)+10),rotation);
            var orbit=camera.GetComponent<OrbitCamera>();orbit.RestoreView(center+radial*800,rotation,800);orbit.enabled=true;clock.Play();orbit.FocusOn(point);yield return Sample("zoom-transition-running",false);clock.Pause();orbit.enabled=false;camera.transform.SetPositionAndRotation(center+radial*(Vector3.Distance(point,center)+10),rotation);
            var lights=planet.GetComponentsInChildren<Light>(true).Where(l=>l.type==LightType.Point).ToArray();var scheduler=Object.FindFirstObjectByType<ResourceVisualScheduler>();scheduler.enabled=false;var lightStates=lights.Select(l=>l.enabled).ToArray();foreach(var light in lights)light.enabled=false;yield return Sample("near-no-resource-lights");for(int i=0;i<lights.Length;i++)lights[i].enabled=lightStates[i];scheduler.enabled=true;
            var shells=planet.GetComponentsInChildren<Renderer>(true).Where(r=>r.name=="OceanSurface"||r.name=="CloudVolume"||r.name=="AtmosphericScattering").ToArray();var states=shells.Select(r=>r.enabled).ToArray();foreach(var r in shells)r.enabled=false;yield return Sample("near-no-weather");for(int i=0;i<shells.Length;i++)shells[i].enabled=states[i];
            var resources=ColonyResource.All.SelectMany(r=>r.GetComponentsInChildren<Renderer>()).ToArray();foreach(var r in resources)r.enabled=false;yield return Sample("near-no-resources");foreach(var r in resources)r.enabled=true;
            var entities=EntityRegistry.All.SelectMany(e=>e.GetComponentsInChildren<Renderer>()).ToArray();var visibility=entities.Select(r=>r.enabled).ToArray();foreach(var r in entities)r.enabled=false;yield return Sample("near-no-entities");for(int i=0;i<entities.Length;i++)entities[i].enabled=visibility[i];
            var surface=planet.GetComponent<MeshRenderer>();var original=surface.sharedMaterial;var simple=new Material(Shader.Find("Universal Render Pipeline/Unlit"));surface.sharedMaterial=simple;yield return Sample("near-simple-surface");surface.sharedMaterial=original;Object.Destroy(simple);
            var pipeline=GraphicsSettings.currentRenderPipeline;var scale=pipeline.GetType().GetProperty("renderScale");if(scale!=null){object old=scale.GetValue(pipeline);scale.SetValue(pipeline,.5f);yield return Sample("near-half-resolution");scale.SetValue(pipeline,old);}
        }
    }
}
