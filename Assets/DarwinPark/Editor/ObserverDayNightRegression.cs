using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DarwinPark.Core;
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
    // ★ 新增：隔离项目同一世界、同一镜头交替渲染旧/新Shader；同步读回计时，不以此冒充游戏FPS或纯GPU耗时。
    [InitializeOnLoad] public static class ObserverDayNightRegression
    {
        private const string Key="DarwinPark.ObserverDayNight";
        private static bool started;private static readonly List<string> Passed=new();
        private static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private static string Folder=>Path.GetFullPath("Artifacts/ObserverDayNightReview");
        private static readonly List<(Material material,Shader current,Shader baseline)> Materials=new();
        private static Color waterColor;
        static ObserverDayNightRegression(){EditorApplication.update+=()=>{if(!SessionState.GetBool(Key,false))return;EditorApplication.isPaused=false;if(!started&&Application.isPlaying&&LanSession.Instance!=null){started=true;LanSession.Instance.StartCoroutine(Guard());}};}
        public static void RunBatch(){SessionState.SetBool(Key,true);Application.runInBackground=true;EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");EditorApplication.EnterPlaymode();}
        private static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);Passed.Add(message);Debug.Log("[ObserverDayNight] "+message);}
        private static IEnumerator Guard()
        {
            Directory.CreateDirectory(Folder);string error=null;var stack=new Stack<IEnumerator>();stack.Push(Run());
            while(stack.Count>0){bool next=false;object value=null;try{next=stack.Peek().MoveNext();if(next)value=stack.Peek().Current;}catch(Exception ex){error=ex.ToString();Debug.LogException(ex);break;}if(!next){stack.Pop();continue;}if(value is IEnumerator nested)stack.Push(nested);else yield return value;}
            Baseline(false);File.WriteAllText(Path.Combine(Folder,"result.txt"),(error==null?"PASS":"FAIL")+"\n"+error+"\n"+string.Join("\n",Passed));SessionState.SetBool(Key,false);EditorApplication.Exit(error==null?0:1);
        }
        private static void Bind(Renderer renderer,string name)
        {
            var material=renderer.sharedMaterial;var baseline=AssetDatabase.LoadAssetAtPath<Shader>("Assets/DarwinPark/Planet/ReviewBaseline"+name+".shader");
            if(baseline==null||!baseline.isSupported)throw new InvalidOperationException("缺少实际修改前Shader："+name);
            Materials.Add((material,material.shader,baseline));
        }
        private static void Baseline(bool before){foreach(var item in Materials)if(item.material!=null){item.material.shader=before?item.baseline:item.current;if(before&&item.material.name=="FreshWater")item.material.SetColor("_BaseColor",waterColor);}}
        private static Texture2D Render(Camera camera,RenderTexture target,Texture2D image)
        {
            var request=new RenderPipeline.StandardRequest{destination=target};camera.aspect=target.width/(float)target.height;
            RenderPipeline.SubmitRenderRequest(camera,request);var old=RenderTexture.active;RenderTexture.active=target;image.ReadPixels(new Rect(0,0,target.width,target.height),0,0,false);RenderTexture.active=old;return image;
        }
        private static Texture2D Capture(Camera camera,string name)
        {
            var target=new RenderTexture(768,432,24,RenderTextureFormat.ARGB32);target.Create();var image=new Texture2D(768,432,TextureFormat.RGB24,false);Render(camera,target,image);image.Apply();File.WriteAllBytes(Path.Combine(Folder,name+".png"),image.EncodeToPNG());target.Release();Object.Destroy(target);return image;
        }
        private static double Mean(Texture2D image,float x0=.43f,float x1=.57f,float y0=.4f,float y1=.6f)
        {double sum=0;int count=0;for(int y=(int)(image.height*y0);y<image.height*y1;y+=2)for(int x=(int)(image.width*x0);x<image.width*x1;x+=2){var c=image.GetPixel(x,y).linear;sum+=c.r*.2126+c.g*.7152+c.b*.0722;count++;}return sum/count;}
        private static double Difference(Texture2D a,Texture2D b)
        {double sum=0;int count=0;for(int y=a.height/3;y<a.height*2/3;y+=3)for(int x=a.width/3;x<a.width*2/3;x+=3){var p=a.GetPixel(x,y);var q=b.GetPixel(x,y);sum+=Math.Abs(p.r-q.r)+Math.Abs(p.g-q.g)+Math.Abs(p.b-q.b);count++;}return sum/count;}
        private static double Median(List<double> values){values.Sort();return values[values.Count/2];}
        private static IEnumerator Benchmark(Camera camera,int width,int height)
        {
            var target=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32);target.Create();var image=new Texture2D(width,height,TextureFormat.RGB24,false);var before=new List<double>();var after=new List<double>();
            // 单次同一目标读回包含GPU同步；交替顺序减少首次编译、缓存和机器背景负载的偏差。
            for(int round=0;round<23;round++)
            {
                for(int index=0;index<2;index++)
                {
                    bool baseline=(round+index)%2==0;Baseline(baseline);long start=System.Diagnostics.Stopwatch.GetTimestamp();Render(camera,target,image);
                    double ms=(System.Diagnostics.Stopwatch.GetTimestamp()-start)*1000d/System.Diagnostics.Stopwatch.Frequency;if(round>=6)(baseline?before:after).Add(ms);
                }
                yield return null;
            }
            double old=Median(before),current=Median(after);File.AppendAllText(Path.Combine(Folder,"performance.txt"),$"{width}x{height}: baselineMedianMs={old:F4} currentMedianMs={current:F4} deltaMs={current-old:F4} ratio={current/old:F4} samples={before.Count} (RenderRequest + synchronized ReadPixels)\n");
            Check(current<=old*1.2+1,$"{width}×{height} 同场景交替渲染未发现大量新增开销");Baseline(false);target.Release();Object.Destroy(target);Object.Destroy(image);
        }
        private static IEnumerator Run()
        {
            var world=Object.FindFirstObjectByType<WorldSessionManager>();var planet=Object.FindFirstObjectByType<PlanetTerrainMeshBuilder>();typeof(WorldSessionManager).GetMethod("EnsureInitialized",Private).Invoke(world,null);
            planet.subdivisions=96;typeof(WorldSessionManager).GetField("_initialTerrain",Private).SetValue(world,JsonUtility.ToJson(planet));PlanetClimateMap.Instance.mapResolution=64;PlanetOreSystem.Instance.mapResolution=32;NaturalPopulationPace.Current.enabled=false;
            world.Create("Observer day night fixture",77441,.5f,20,44);double deadline=EditorApplication.timeSinceStartup+180;while(world.Busy){if(EditorApplication.timeSinceStartup>deadline)throw new TimeoutException("world generation");yield return null;}
            ServiceLocator.Get<SimulationClock>().Pause();yield return null;
            var camera=Camera.main;camera.GetComponent<OrbitCamera>().enabled=false;camera.transform.SetPositionAndRotation(planet.transform.position+Vector3.forward*850,Quaternion.LookRotation(Vector3.back,Vector3.up));
            var sun=Object.FindFirstObjectByType<SunSystem>();sun.enabled=false;var light=sun.GetComponent<Light>();
            var clouds=planet.transform.Find("CloudVolume").GetComponent<Renderer>();var atmosphere=planet.transform.Find("AtmosphericScattering").GetComponent<Renderer>();var ocean=planet.transform.Find("OceanSurface").GetComponent<Renderer>();var water=planet.transform.Find("RiversAndLakes").GetComponent<Renderer>();var terrain=planet.GetComponent<Renderer>();
            Bind(terrain,"PlanetURP");Bind(ocean,"PlanetOcean");Bind(clouds,"PlanetCloudVolume");Bind(atmosphere,"PlanetScattering");
            waterColor=planet.Data.Hydrology.Settings.waterColor;Materials.Add((water.sharedMaterial,water.sharedMaterial.shader,Shader.Find("Universal Render Pipeline/Unlit")));
            int cameras=Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Length,lights=Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Length,renderers=Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Length;
            var originalMesh=terrain.GetComponent<MeshFilter>().sharedMesh;clouds.enabled=false;atmosphere.enabled=false;
            light.transform.rotation=Quaternion.LookRotation(Vector3.back,Vector3.up);yield return null;Baseline(true);var oldDay=Capture(camera,"baseline-day");Baseline(false);var day=Capture(camera,"observer-day");Check(Difference(oldDay,day)<.01,"日面保留原有地表和海面颜色");
            light.transform.rotation=Quaternion.LookRotation(Vector3.forward,Vector3.up);yield return null;Baseline(true);var oldNight=Capture(camera,"baseline-night");Baseline(false);var night=Capture(camera,"observer-night");
            File.WriteAllText(Path.Combine(Folder,"pixels.txt"),$"linear luminance: oldDay={Mean(oldDay):F6} day={Mean(day):F6} oldNight={Mean(oldNight):F6} night={Mean(night):F6}\n");
            Check(Mean(night)<Mean(day)*.35&&Mean(night)<Mean(oldNight)*.3,"相同观察镜头夜半球线性亮度明显降低，保留轮廓");Object.Destroy(oldDay);Object.Destroy(oldNight);Object.Destroy(day);Object.Destroy(night);
            var block=new MaterialPropertyBlock();terrain.GetPropertyBlock(block);block.SetFloat("_LayerMode",1);terrain.SetPropertyBlock(block);yield return null;Baseline(true);var oldMap=Capture(camera,"baseline-map");Baseline(false);var map=Capture(camera,"observer-map");Check(Difference(oldMap,map)<.001,"夜间数据图层与原显示一致，不降低读图亮度");Object.Destroy(oldMap);Object.Destroy(map);block.SetFloat("_LayerMode",0);terrain.SetPropertyBlock(block);
            light.transform.rotation=Quaternion.LookRotation(Vector3.left,Vector3.up);clouds.enabled=true;atmosphere.enabled=true;yield return null;Baseline(true);var oldOrbit=Capture(camera,"baseline-terminator");Baseline(false);var orbit=Capture(camera,"observer-terminator");
            // 不直接比较蓝色深海与白色云/陆地的绝对亮度；各自对比原图以消除反照率差异。
            double dayRatio=Mean(orbit,.36f,.45f,.4f,.6f)/Mean(oldOrbit,.36f,.45f,.4f,.6f),nightRatio=Mean(orbit,.55f,.64f,.4f,.6f)/Mean(oldOrbit,.55f,.64f,.4f,.6f);
            File.AppendAllText(Path.Combine(Folder,"pixels.txt"),$"splitDayAttenuation={dayRatio:F6} splitNightAttenuation={nightRatio:F6}\n");
            Check(nightRatio<dayRatio*.6,"同屏观察中夜面相对自身原图衰减，明暗交界跟随太阳方向");Object.Destroy(oldOrbit);Object.Destroy(orbit);
            // 直接渲染同一无深度遮挡气壳；视点处于晨昏，但日侧/夜侧边缘应有不同颜色。
            clouds.enabled=false;terrain.enabled=false;ocean.enabled=false;water.enabled=false;var oldRing=Capture(camera,"atmosphere-spatial");
            double left=Mean(oldRing,.36f,.43f,.4f,.6f),right=Mean(oldRing,.57f,.64f,.4f,.6f);Check(Math.Abs(left-right)>.005,"气壳颜色按每条视线对应位置变化，不再整球套用相机晨昏色");Object.Destroy(oldRing);terrain.enabled=true;ocean.enabled=true;water.enabled=true;clouds.enabled=true;
            File.WriteAllText(Path.Combine(Folder,"performance.txt"),"Fixed world seed77441 subdivisions96; no entities; orbit camera; cloudSteps="+planet.GetComponent<PlanetWeatherVisuals>().cloudSteps+"; scatteringSteps=8\n");yield return Benchmark(camera,768,432);yield return Benchmark(camera,1920,1080);
            Check(Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Length==cameras&&Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Length==lights&&Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Length==renderers&&terrain.GetComponent<MeshFilter>().sharedMesh==originalMesh,"昼夜显示不新增相机/灯光/绘制对象，也不重建世界网格");
            File.WriteAllText(Path.Combine(Folder,"scene.txt"),$"cameras={cameras} lights={lights} renderers={renderers}; no entity scans; no new per-frame jobs\n");
        }
    }
}
