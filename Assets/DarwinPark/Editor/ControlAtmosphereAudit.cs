using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DarwinPark.Core;
using DarwinPark.Entities;
using DarwinPark.Entities.Ecology;
using DarwinPark.Genetics;
using DarwinPark.Planet;
using DarwinPark.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;
namespace DarwinPark.EditorTools
{
    // ★ 新增：真实第一人称天空的有/无大气像素差和截图，不能只凭对象enabled判定大气可见。
    [InitializeOnLoad]public static class ControlAtmosphereAudit
    {
        private const string Key="DarwinPark.ControlAtmosphere";private static bool _started;private static string Folder=>Path.GetFullPath("Artifacts/ActiveControlsReview");
        static ControlAtmosphereAudit(){EditorApplication.update+=()=>{if(!_started&&Application.isPlaying&&SessionState.GetBool(Key,false)&&LanSession.Instance!=null){_started=true;LanSession.Instance.StartCoroutine(Guard());}};}
        public static void RunBatch(){Application.runInBackground=true;SessionState.SetBool(Key,true);EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));EditorApplication.EnterPlaymode();}
        private static IEnumerator Guard()
        {
            Directory.CreateDirectory(Folder);File.WriteAllText(Path.Combine(Folder,"result.txt"),"RUNNING");string error=null;var stack=new Stack<IEnumerator>();stack.Push(Run());
            while(stack.Count>0){bool next=false;object current=null;try{next=stack.Peek().MoveNext();if(next)current=stack.Peek().Current;}catch(Exception ex){error=ex.ToString();Debug.LogException(ex);break;}if(!next){stack.Pop();continue;}if(current is IEnumerator nested)stack.Push(nested);else yield return current;}
            File.WriteAllText(Path.Combine(Folder,"result.txt"),(error==null?"PASS":"FAIL")+"\n"+error);SessionState.SetBool(Key,false);EditorApplication.Exit(error==null?0:1);
        }
        private static IEnumerator Run()
        {
            var session=Object.FindFirstObjectByType<WorldSessionManager>();session.autoSaveInterval=36000;var planet=Object.FindFirstObjectByType<PlanetTerrainMeshBuilder>();planet.subdivisions=32;typeof(WorldSessionManager).GetField("_initialTerrain",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(session,JsonUtility.ToJson(planet));PlanetClimateMap.Instance.mapResolution=32;PlanetOreSystem.Instance.mapResolution=16;session.Create("第一人称大气验证",77441,.5f,20,44);while(session.Busy)yield return null;
            var clock=ServiceLocator.Get<SimulationClock>();clock.Pause();Object.FindFirstObjectByType<RandomPlanetSpawner>().enabled=false;PopulationMaintenance.Restore(null);GameplayTuning.Current.mutationRoundsEnabled=false;foreach(var e in EntityRegistry.All.ToArray())Object.DestroyImmediate(e.gameObject);
            var light=Object.FindFirstObjectByType<SunSystem>().GetComponent<Light>();var direction=-light.transform.forward;var land=planet.Data.Vertices.Where(v=>v.magnitude>planet.WaterRadius+3&&Vector3.Dot(v.normalized,direction)>.25f).OrderByDescending(v=>v.magnitude).First();var up=land.normalized;planet.GetComponent<MeshCollider>().Raycast(new Ray(up*1000,-up),out var hit,2000);
            var genome=new Genome();genome.Add(GeneInstance.Create("gene_locomotion",70,0));DarwinEntity actor=null;void Added(DarwinEntity e)=>actor=e;EntityRegistry.OnRegistered+=Added;try{DarwinEntity.Spawner(genome,hit.point+up,"控制模式大气验证");}finally{EntityRegistry.OnRegistered-=Added;}yield return null;actor.GetComponent<Rigidbody>().isKinematic=true;
            var view=Object.FindFirstObjectByType<EntityViewController>();if(!view.EnterView(actor))throw new InvalidOperationException("cannot enter view");var input=view.GetComponent<EntityPossessionInput>();typeof(EntityPossessionInput).GetField("_pitch",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(input,-25f);
            var weather=planet.GetComponent<PlanetWeatherVisuals>();weather.cloudSpeed=0;var atmosphere=planet.transform.Find("AtmosphericScattering").GetComponent<Renderer>();
            yield return new WaitForSecondsRealtime(.3f);yield return new WaitForEndOfFrame();var on=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Path.Combine(Folder,"control-atmosphere-on.png"),on.EncodeToPNG());atmosphere.enabled=false;
            yield return null;yield return new WaitForEndOfFrame();var off=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Path.Combine(Folder,"control-atmosphere-off.png"),off.EncodeToPNG());
            double difference=0;int count=0;for(int y=on.height/2;y<on.height*9/10;y+=4)for(int x=on.width/4;x<on.width*3/4;x+=4){var a=on.GetPixel(x,y);var b=off.GetPixel(x,y);difference+=Math.Abs(a.r-b.r)+Math.Abs(a.g-b.g)+Math.Abs(a.b-b.b);count++;}
            File.WriteAllText(Path.Combine(Folder,"atmosphere-pixels.txt"),$"difference={difference/count:R} altitude={Vector3.Distance(Camera.main.transform.position,planet.transform.position)-planet.WaterRadius:R} outer={atmosphere.sharedMaterial.GetFloat("_OuterRadius"):R} camera={Camera.main.transform.position} clouds={planet.transform.Find("CloudVolume").gameObject.activeSelf}");double day=Brightness(on);Object.Destroy(on);Object.Destroy(off);atmosphere.enabled=true;
            var density=typeof(SimulationOptimization).GetField("entitySkyDensityMultiplier");
            if(density!=null)
            {
                if(atmosphere.sharedMaterial.GetFloat("_EntitySkyDensity")!=SimulationOptimization.Current.entitySkyDensityMultiplier)throw new InvalidOperationException("实体天空增强未应用");
                var sun=light.GetComponent<SunSystem>();sun.enabled=false;light.transform.rotation=Quaternion.LookRotation(-light.transform.forward,light.transform.up);
                yield return null;yield return new WaitForEndOfFrame();var night=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Path.Combine(Folder,"control-atmosphere-night.png"),night.EncodeToPNG());double dark=Brightness(night);Object.Destroy(night);if(dark>=day)throw new InvalidOperationException("夜晚天空应保持比白天暗");sun.enabled=true;
                // ★ 新增：同一近景地面分别使用1/8天空倍率，检查只增强天空而不洗白地形。
                typeof(EntityPossessionInput).GetField("_pitch",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(input,65f);yield return null;yield return new WaitForEndOfFrame();var boosted=ScreenCapture.CaptureScreenshotAsTexture();float original=SimulationOptimization.Current.entitySkyDensityMultiplier;density.SetValue(SimulationOptimization.Current,1f);yield return null;yield return new WaitForEndOfFrame();var normal=ScreenCapture.CaptureScreenshotAsTexture();double terrain=Difference(boosted,normal,.35f,.65f,.35f,.65f);File.WriteAllBytes(Path.Combine(Folder,"control-atmosphere-ground.png"),boosted.EncodeToPNG());Object.Destroy(boosted);Object.Destroy(normal);density.SetValue(SimulationOptimization.Current,original);if(terrain>.01)throw new InvalidOperationException("天空倍率改变了近处地形："+terrain);
                view.ExitView();yield return null;yield return new WaitForEndOfFrame();if(atmosphere.sharedMaterial.GetFloat("_EntitySkyDensity")!=1)throw new InvalidOperationException("退出实体视角未恢复原天空");File.WriteAllText(Path.Combine(Folder,"atmosphere-boundaries.txt"),$"PASS\nday={day:R} night={dark:R} terrainDelta={terrain:R} exitDensity=1");
            }
            else view.ExitView();
        }
        private static double Brightness(Texture2D image)
        {double total=0;int count=0;for(int y=image.height/2;y<image.height*9/10;y+=4)for(int x=image.width/4;x<image.width*3/4;x+=4){var p=image.GetPixel(x,y);total+=(p.r+p.g+p.b)/3;count++;}return total/count;}
        private static double Difference(Texture2D a,Texture2D b,float x0,float x1,float y0,float y1)
        {double total=0;int count=0;for(int y=(int)(a.height*y0);y<a.height*y1;y+=4)for(int x=(int)(a.width*x0);x<a.width*x1;x+=4){var p=a.GetPixel(x,y);var q=b.GetPixel(x,y);total+=Math.Abs(p.r-q.r)+Math.Abs(p.g-q.g)+Math.Abs(p.b-q.b);count++;}return total/count;}
    }
}
