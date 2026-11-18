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
using DarwinPark.Genetics.Mutation;
using DarwinPark.Planet;
using DarwinPark.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace DarwinPark.EditorTools
{
    // ★ 新增：通过管线正式 RenderRequest 渲染天空和河流，不依赖批处理下可能不触发的帧末截图事件。
    [InitializeOnLoad]public static class WorldVisualRegression
    {
        private const string Key="DarwinPark.WorldVisual";private static bool started;private static readonly List<string> Passed=new();private static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private static string Folder=>Path.GetFullPath("Artifacts/WorldVisualReview");
        static WorldVisualRegression(){EditorApplication.update+=()=>{if(!SessionState.GetBool(Key,false))return;EditorApplication.isPaused=false;if(!started&&Application.isPlaying&&LanSession.Instance!=null){started=true;LanSession.Instance.StartCoroutine(Guard());}};}
        public static void RunBatch(){Application.runInBackground=true;SessionState.SetBool(Key,true);EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");EditorApplication.EnterPlaymode();}
        private static void Check(bool ok,string text){if(!ok)throw new InvalidOperationException(text);Passed.Add(text);Debug.Log("[WorldVisual] "+text);}
        private static IEnumerator Guard()
        {
            Directory.CreateDirectory(Folder);string error=null;var stack=new Stack<IEnumerator>();stack.Push(Run());
            while(stack.Count>0){bool next=false;object value=null;try{next=stack.Peek().MoveNext();if(next)value=stack.Peek().Current;}catch(Exception ex){error=ex.ToString();Debug.LogException(ex);break;}if(!next){stack.Pop();continue;}if(value is IEnumerator nested)stack.Push(nested);else yield return value;}
            File.WriteAllText(Path.Combine(Folder,"result.txt"),(error==null?"PASS":"FAIL")+"\n"+error+"\n"+string.Join("\n",Passed));SessionState.SetBool(Key,false);EditorApplication.Exit(error==null?0:1);
        }
        private static Texture2D Capture(string name,Camera camera)
        {
            var target=new RenderTexture(768,432,24,RenderTextureFormat.ARGB32);target.Create();float aspect=camera.aspect;camera.aspect=768f/432;
            var request=new RenderPipeline.StandardRequest{destination=target};if(!RenderPipeline.SupportsRenderRequest(camera,request))throw new InvalidOperationException("Active pipeline does not support render requests");
            RenderPipeline.SubmitRenderRequest(camera,request);var previous=RenderTexture.active;RenderTexture.active=target;
            var texture=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,target.width,target.height),0,0);texture.Apply();RenderTexture.active=previous;camera.aspect=aspect;target.Release();Object.Destroy(target);
            File.WriteAllBytes(Path.Combine(Folder,name+".png"),texture.EncodeToPNG());return texture;
        }
        private static double Brightness(Texture2D texture)
        {double sum=0;int count=0;for(int y=texture.height/2;y<texture.height*9/10;y+=4)for(int x=texture.width/4;x<texture.width*3/4;x+=4){var color=texture.GetPixel(x,y);sum+=(color.r+color.g+color.b)/3;count++;}return sum/count;}
        private static IEnumerator Run()
        {
            var world=Object.FindFirstObjectByType<WorldSessionManager>();var planet=Object.FindFirstObjectByType<PlanetTerrainMeshBuilder>();
            typeof(WorldSessionManager).GetMethod("EnsureInitialized",Private).Invoke(world,null);planet.subdivisions=96;typeof(WorldSessionManager).GetField("_initialTerrain",Private).SetValue(world,JsonUtility.ToJson(planet));PlanetClimateMap.Instance.mapResolution=64;PlanetOreSystem.Instance.mapResolution=32;
            NaturalPopulationPace.Current.enabled=false;world.Create("World visual fixture",77441,.5f,20,44);double deadline=EditorApplication.timeSinceStartup+180;while(world.Busy){if(EditorApplication.timeSinceStartup>deadline)throw new TimeoutException("world generation");yield return null;}
            var clock=ServiceLocator.Get<SimulationClock>();clock.Pause();yield return null;var camera=Camera.main;var orbit=camera.GetComponent<OrbitCamera>();var sun=Object.FindFirstObjectByType<SunSystem>();var light=sun.GetComponent<Light>();sun.enabled=false;
            var mesh=planet.transform.Find("RiversAndLakes").GetComponent<MeshFilter>().sharedMesh;var vertices=mesh.vertices;var indices=mesh.triangles;
            Check(vertices.Length>0&&vertices.All(v=>float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z))&&indices.All(i=>i>=0&&i<vertices.Length),"统一河湖网格有有效顶点与索引，无NaN裁切交点");
            File.WriteAllText(Path.Combine(Folder,"mesh.txt"),"vertices="+vertices.Length+" triangles="+indices.Length/3);
            var faces=new HashSet<(int,int,int)>();var edges=new Dictionary<(int,int),int>();
            for(int i=0;i<indices.Length;i+=3)
            {
                int a=indices[i],b=indices[i+1],c=indices[i+2];var normal=Vector3.Cross(vertices[b]-vertices[a],vertices[c]-vertices[a]);if(Vector3.Dot(normal,vertices[a])<0)throw new InvalidOperationException("inward water triangle");
                var sorted=new[]{a,b,c};Array.Sort(sorted);if(!faces.Add((sorted[0],sorted[1],sorted[2])))throw new InvalidOperationException("duplicate water triangle");
                foreach(var edge in new[]{(a,b),(b,c),(c,a)}){var key=(Math.Min(edge.Item1,edge.Item2),Math.Max(edge.Item1,edge.Item2));edges.TryGetValue(key,out int count);edges[key]=count+1;}
            }
            Check(edges.Values.All(v=>v<=2),"统一水面无重复三角形或超过两面的重叠边");
            File.WriteAllText(Path.Combine(Folder,"mesh.txt"),"vertices="+vertices.Length+" triangles="+indices.Length/3+" sharedEdges="+edges.Count(e=>e.Value==2));
            var land=planet.Data.Vertices.Where(v=>v.magnitude>planet.WaterRadius+3&&Mathf.Abs(v.normalized.y)<.35f&&Vector3.Dot(v.normalized,-light.transform.forward)>.45f).OrderByDescending(v=>v.magnitude).First();var up=land.normalized;
            var genome=GeneMutator.CreateBaseGenome();if(!genome.HasDefinition("gene_locomotion"))genome.Add(GeneInstance.Create("gene_locomotion",70,0));
            var actor=EntitySpawnCapture.Create(genome,planet.transform.TransformPoint(land+up),"Control sky fixture");actor.GetComponent<Rigidbody>().isKinematic=true;
            var view=Object.FindFirstObjectByType<EntityViewController>();var input=view.GetComponent<EntityPossessionInput>();Check(input.minPitch<0,"场景第一人称配置允许负俯仰抬头");
            input.minPitch=5;input.maxPitch=70;Check(view.EnterView(actor)&&input.minPitch<0,"旧第三人称正数俯仰范围进入控制后完成迁移");
            Vector3 heading=Vector3.ProjectOnPlane(Vector3.forward,up).normalized;input.ApplyLook(new Vector2(0,20),up,ref heading);yield return null;
            Check(Vector3.Dot(camera.transform.forward,up)>.7f,"控制模式真实镜头可向上看，准星方向同源");
            input.ApplyLook(new Vector2(0,-40),up,ref heading);yield return null;Check(Vector3.Dot(camera.transform.forward,up)<-.7f,"控制模式也可向下看，俯仰未被地表轨道覆盖");
            Check(EntityFirstPersonCamera.LookDirection(Vector3.forward,Vector3.forward,-45).z>.5f,"初始朝向与球面法线平行时仍有有效抬头轴");
            typeof(EntityPossessionInput).GetField("_pitch",Private).SetValue(input,-25f);yield return null;
            var weather=planet.GetComponent<PlanetWeatherVisuals>();var atmosphere=planet.transform.Find("AtmosphericScattering").GetComponent<Renderer>();
            light.transform.rotation=Quaternion.LookRotation(-up,Vector3.Cross(up,Vector3.forward));yield return null;var day=Capture("sky-day",camera);double dayBrightness=Brightness(day);
            atmosphere.enabled=false;var clear=Capture("sky-without-atmosphere",camera);double difference=Math.Abs(Brightness(clear)-dayBrightness);Check(difference>.005,"控制视角实际像素中大气层可见");Object.Destroy(clear);atmosphere.enabled=true;
            light.transform.rotation=Quaternion.LookRotation(up,Vector3.Cross(up,Vector3.forward));yield return null;var night=Capture("sky-night",camera);double nightBrightness=Brightness(night);
            Check(nightBrightness<dayBrightness*.7,"同一控制视角夜空亮度明显低于白天");
            var tangent=Vector3.ProjectOnPlane(Vector3.forward,up).normalized;light.transform.rotation=Quaternion.LookRotation(-tangent,up);yield return null;var twilight=Capture("sky-twilight",camera);
            File.WriteAllText(Path.Combine(Folder,"sky.txt"),"day="+dayBrightness+" night="+nightBrightness+" atmosphereDelta="+difference+" twilight="+Brightness(twilight));Object.Destroy(day);Object.Destroy(night);Object.Destroy(twilight);
            string originalPalette=File.ReadAllText(AtmospherePalette.FilePath);
            try
            {
                light.transform.rotation=Quaternion.LookRotation(-up,Vector3.Cross(up,Vector3.forward));
                File.WriteAllText(AtmospherePalette.FilePath,"{\"version\":1,\"dayColor\":\"#33FF66\",\"twilightColor\":\"#CC66FF\",\"nightColor\":\"#040820\"}");
                yield return new WaitForSecondsRealtime(2.2f);
                Check(atmosphere.sharedMaterial.GetColor("_ScatterColor").g>.99f&&atmosphere.sharedMaterial.GetColor("_TwilightColor").b>.99f,"外部白天、晨昏和夜空颜色通过真实轮询热更新材质");
                var custom=Capture("sky-custom-palette",camera);Check(Math.Abs(Brightness(custom)-dayBrightness)>.005,"自定义配色实际改变控制视角天空像素");Object.Destroy(custom);
                var valid=AtmospherePalette.Current;File.WriteAllText(AtmospherePalette.FilePath,"{\"version\":1,\"dayColor\":\"错误颜色\"}");AtmospherePalette.Reload();
                Check(ReferenceEquals(valid,AtmospherePalette.Current),"无效颜色配置保留上一份有效配色");
            }
            finally{File.WriteAllText(AtmospherePalette.FilePath,originalPalette);AtmospherePalette.Reload();}
            yield return null;
            // ★ 新增：除固定太阳方向像素对照外，验证正式太阳更新仍由运行/暂停的世界时钟驱动。
            float phase=sun.OrbitAngle;sun.enabled=true;clock.Play();yield return null;yield return null;yield return null;
            Check(Mathf.Abs(Mathf.DeltaAngle(phase,sun.OrbitAngle))>.01f,"运行世界时真实太阳相位推进，天空无需独立假时钟");
            clock.Pause();yield return null;phase=sun.OrbitAngle;yield return null;yield return null;
            Check(Mathf.Abs(Mathf.DeltaAngle(phase,sun.OrbitAngle))<.001f,"暂停后太阳相位保持，昼夜与世界同步");
            view.ExitView();orbit.enabled=false;yield return null;
            var hydro=planet.Data.Hydrology;Vector3 riverDirection=default;
            for(int i=0;i<hydro.River.Length;i++)if(hydro.River[i]){var dir=hydro.Channel(i);var point=PlanetCoordinate.AtLocal(planet.Data,dir*(planet.radius+hydro.Spill[i]*planet.heightScale));if(point.WaterRadius>planet.Data.Radius+planet.Data.SeaLevel*planet.Data.HeightScale+1&&point.WaterDepth>.5f){riverDirection=dir;break;}}
            Check(riverDirection!=default,"存在实际高于海面的河段用于近景显示验收");float waterRadius=planet.Data.Radius+hydro.Water(riverDirection)*planet.Data.HeightScale;var river=planet.transform.TransformPoint(riverDirection*waterRadius);var outward=planet.transform.TransformDirection(riverDirection);var forward=Vector3.Cross(outward,Vector3.right).normalized;
            camera.transform.SetPositionAndRotation(river+outward*3-forward*8,Quaternion.LookRotation(forward*12-outward*3,outward));yield return null;var riverImage=Capture("river-close",camera);Object.Destroy(riverImage);
            var panel=Object.FindFirstObjectByType<SimulationControlPanel>();typeof(SimulationControlPanel).GetMethod("SwitchPage",Private).Invoke(panel,new object[]{1});yield return null;
            EntityTemplateManager.SaveTemplate(actor);yield return null;var slot=ServiceLocator.Get<UIRoot>().MainCanvas.GetComponentsInChildren<EntitySpawnerUI>().First(s=>s.TemplateName==actor.DisplayName);PlacementQuantityPopup.Show(slot);var popup=Object.FindFirstObjectByType<PlacementQuantityPopup>();Check(((RectTransform)popup.transform).rect.height<=126,"数量及删除入口窗口使用紧密高度");PlacementQuantityPopup.CloseAll();PlacementQuantityPopup.Show(slot,true);popup=Object.FindFirstObjectByType<PlacementQuantityPopup>();Check(((RectTransform)popup.transform).rect.height<=104,"删除确认模式移除数量编辑器空白高度");PlacementQuantityPopup.CloseAll();
        }
    }
}
