using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DarwinPark.Core;
using DarwinPark.Entities;
using DarwinPark.Entities.Ecology;
using DarwinPark.Entities.Logic;
using DarwinPark.Entities.Simulation;
using DarwinPark.Entities.Stats;
using DarwinPark.Genetics;
using DarwinPark.Planet;
using DarwinPark.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;
namespace DarwinPark.EditorTools
{
    // ★ 新增：真实场景验证矿脉一致性、补给失效、自然节奏、一次种源、水系和陆地路线。
    [InitializeOnLoad]public static class WorldEvolutionRegression
    {
        private const string Key="DarwinPark.WorldEvolution";private static bool _started;private static readonly List<string> Passed=new();private static string Folder=>Path.GetFullPath("Artifacts/WorldEvolutionReview");
        static WorldEvolutionRegression(){EditorApplication.update+=()=>{if(!_started&&Application.isPlaying&&SessionState.GetBool(Key,false)&&LanSession.Instance!=null){_started=true;LanSession.Instance.StartCoroutine(Guard());}};}
        public static void RunBatch(){SessionState.SetBool(Key,true);Application.runInBackground=true;EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));EditorApplication.EnterPlaymode();}
        private static void Check(bool ok,string text){if(!ok)throw new InvalidOperationException(text);Passed.Add(text);if(Passed.Count%16==0)File.WriteAllLines(Path.Combine(Folder,"progress.txt"),Passed);}
        private static IEnumerator Guard(){Directory.CreateDirectory(Folder);string error=null;var stack=new Stack<IEnumerator>();stack.Push(Run());while(stack.Count>0){bool next=false;object current=null;try{next=stack.Peek().MoveNext();if(next)current=stack.Peek().Current;}catch(Exception ex){error=ex.ToString();Debug.LogException(ex);break;}if(!next){stack.Pop();continue;}if(current is IEnumerator nested)stack.Push(nested);else yield return current;}File.WriteAllText(Path.Combine(Folder,"result.txt"),(error==null?"PASS":"FAIL")+"\n"+error+"\n"+string.Join("\n",Passed));SessionState.SetBool(Key,false);EditorApplication.Exit(error==null?0:1);}
        private static IEnumerator Wait(Func<bool> done,string name){double start=Time.realtimeSinceStartupAsDouble;while(!done()){if(Time.realtimeSinceStartupAsDouble-start>90)throw new TimeoutException(name);yield return null;}}
        private static void Clear(){foreach(var e in EntityRegistry.All.ToArray())Object.DestroyImmediate(e.gameObject);ColonyResource.ClearWorld();}
        private static DarwinEntity Spawn(string template,Vector3 point,AgeState age=AgeState.Adult)
        {DarwinEntity child=null;void Capture(DarwinEntity e)=>child=e;EntityRegistry.OnRegistered+=Capture;try{DarwinEntity.Spawner(EcologyDefaults.Template(template).CreateGenome(),point,template);}finally{EntityRegistry.OnRegistered-=Capture;}child.GetComponent<EntityLifeCycle>().RestoreProgress(age==AgeState.Infant?0:LifeCycleSettings.Load().adultProgress);child.Genes.RestoreStageState(age,age==AgeState.Infant?1:3,age==AgeState.Infant?1:2);child.Genes.Recalculate();child.GetComponent<Rigidbody>().isKinematic=true;return child;}
        private static IEnumerator Run()
        {
            NaturalPopulationPace.Current.enabled=false;GameplayTuning.Current.mutationRoundsEnabled=false;GameplayTuning.Current.populationRatioControlEnabled=false;
            var session=Object.FindFirstObjectByType<WorldSessionManager>();session.autoSaveInterval=36000;var planet=Object.FindFirstObjectByType<PlanetTerrainMeshBuilder>();planet.subdivisions=64;typeof(WorldSessionManager).GetField("_initialTerrain",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(session,JsonUtility.ToJson(planet));PlanetClimateMap.Instance.mapResolution=128;PlanetOreSystem.Instance.mapResolution=64;
            session.Create("自然节奏与水系回归",742091,.5f,20,44);yield return Wait(()=>!session.Busy,"生成新世界");var clock=ServiceLocator.Get<SimulationClock>();clock.Pause();Clear();
            Vector3 home=planet.transform.position+planet.transform.up*(planet.radius+planet.heightScale);for(int i=0;i<512;i++){var d=PlanetTerrainSampler.DirectionFromUV(i*.61803398875f%1,.25f+(i%64)/128f);var p=EcologyQueries.Surface(planet.transform.position+d*(planet.radius+planet.heightScale));if((p-planet.transform.position).magnitude>planet.LocalWaterRadius(p)+3){home=p;break;}}
            yield return Ore(planet,home);yield return Food(home);Clear();NaturalPopulationPace.Current.enabled=true;Pace(home);yield return Navigation(planet);
            Check(planet.Data.Hydrology!=null&&planet.Data.Hydrology.RiverCells>0,"新世界生成实际汇流河网");Check(planet.Data.Hydrology.LakeCells>0,"优先洪泛保留内陆盆地积水");
            var hydro=planet.Data.Hydrology;for(int i=0;i<hydro.Bed.Length;i+=113){int node=i,steps=0;while(hydro.Downstream[node]>=0){int next=hydro.Downstream[node];Check(hydro.Spill[node]+.00001f>=hydro.Spill[next],"下游水位不高于上游");node=next;if(++steps>hydro.Bed.Length)throw new InvalidOperationException("汇流出现环");}}
            Check(planet.transform.Find("RiversAndLakes")?.GetComponent<MeshFilter>().sharedMesh.vertexCount>0,"河湖合并网格实际挂载并可渲染");Check(planet.Data.Vertices.All(v=>float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z)),"地形与河床顶点全部有限");
            var cfg=PlanetClimateSystem.Instance.config;Check(cfg.Classify(28,90,false)==ClimateType.TropicalRain&&cfg.Classify(28,55,false)==ClimateType.Savanna,"雨林和热带草原按温湿条件区分");Check(cfg.Classify(5,70,false)==ClimateType.Taiga&&cfg.Classify(10,20,false)==ClimateType.ColdDesert,"寒带森林及寒冷荒漠可分类");Check(Vector3.Distance((Vector4)EarthClimateRules.Color(28,90,cfg),(Vector4)EarthClimateRules.Color(28,55,cfg))>.2f,"雨林深青绿与热带草原金黄有明显色差");Check(EarthClimateRules.Humidity(Vector3.forward,cfg)>EarthClimateRules.Humidity(PlanetTerrainSampler.DirectionFromUV(.5f,.5f+28/180f),cfg),"赤道辐合较副热带下沉区更湿润");
            string snapshot=JsonUtility.ToJson(planet);LandformSaveCompatibility.Restore(planet,"{}");Check(!planet.landforms.enabled,"旧档缺地貌节点不隐式重建不同几何");JsonUtility.FromJsonOverwrite(snapshot,planet);
            // ★ 新增：旧世界通过真实后台参数链路切换地貌，资源能量、实体身份和日期保持。
            var testResource=ColonyResource.Create(new ColonyResourceSave{id=Guid.NewGuid().ToString("N"),kind=ColonyResourceKind.Energy,energy=17,local=EcologyQueries.ToLocal(home),scale=Vector3.one});string resourceId=testResource.State.id;int alive=EntityRegistry.All.Count;float gameTime=clock.GameTime;var update=WorldParameterUpdate.Ensure();
            Check(update.Request("landforms","0",out _),"可请求关闭地貌并后台重建");yield return Wait(()=>!update.Busy,"关闭地貌更新");Check(planet.Data.Hydrology==null&&!planet.landforms.enabled,"关闭后恢复原地形生成路径");Check(ColonyResource.Find(resourceId)?.State.energy==17&&EntityRegistry.All.Count==alive&&clock.GameTime==gameTime,"重建保留真实资源营养、实体数和日期");
            Check(update.Request("landforms","1",out _),"旧世界可显式启用新地貌");yield return Wait(()=>!update.Busy,"启用地貌更新");Check(planet.Data.Hydrology!=null&&planet.landforms.enabled,"显式启用后生成新水系并保存开关");Check(JsonUtility.FromJson<PlanetTerrainConfig>(WorldSaveStore.Read(WorldSaveStore.FileFor(session.Current.id)).terrainParameters).landforms.enabled,"地貌开关实际写入存档");
            Clear();session.Create("少量自然种源",742091,.5f,20,44);yield return Wait(()=>!session.Busy&&EntityRegistry.All.Count==NaturalPopulationPace.Current.initialPopulation,"一次建立自然种源");clock.Pause();
            Check(EntityRegistry.All.Count==10&&EntityRegistry.All.Count(e=>EcologyEnergyBudget.IsProducer(e.Genes))==7&&EntityRegistry.All.Count(e=>e.Genes.GetAttribute("moveSpeed")>0)==3,"新世界一次建立7生产者和3移动种源");Check(EntityRegistry.All.All(e=>e.Genes.CurrentAge==AgeState.Infant&&!planet.IsSubmerged(e.transform.position)),"种源均为陆地幼体");
            Check(session.GetComponents<NaturalWorldSeeder>().Length==1,"连续创建复用唯一种源组件");session.SaveNow(false);var disk=WorldSaveStore.Read(WorldSaveStore.FileFor(session.Current.id));session.Open(disk);yield return Wait(()=>!session.Busy,"重载种源存档");clock.Pause();for(int i=0;i<15;i++)yield return null;Check(EntityRegistry.All.Count==10,"旧档重载不重复领取初始种源");Clear();for(int i=0;i<15;i++)yield return null;Check(EntityRegistry.All.Count==0,"种群灭绝或清场后不自动补员");
            yield return Visuals(planet);
        }
        // ★ 新增：导出真实气候纹理与河湖近景；测试结束前隐藏云/UI，便于检查水面是否被地形遮住。
        private static IEnumerator Visuals(PlanetTerrainMeshBuilder planet)
        {
            var map=PlanetClimateMap.Instance;map.mapResolution=512;map.GenerateClimateMap();
            File.WriteAllBytes(Path.Combine(Folder,"climate-map.png"),map.ClimateTexture.EncodeToPNG());
            foreach(var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))canvas.enabled=false;
            foreach(var renderer in planet.GetComponentsInChildren<Renderer>(true))if(renderer.name=="CloudVolume")renderer.enabled=false;
            var camera=Camera.main;var orbit=camera.GetComponent<OrbitCamera>();if(orbit!=null)orbit.enabled=false;
            var hydro=planet.Data.Hydrology;
            foreach(bool lake in new[]{false,true})
            {
                int cell=-1;float best=-1;for(int i=0;i<hydro.Bed.Length;i++)if(lake?hydro.Lake[i]:hydro.River[i]&&!hydro.Lake[i]){float rank=lake?hydro.Spill[i]-hydro.Bed[i]:hydro.Accumulation[i];if(rank>best){cell=i;best=rank;}}
                Check(cell>=0,"河流和盆地湖泊均有可检查的真实近景");var dir=hydro.Directions[cell];float ground=PlanetTerrainSampler.SampleHeight(planet.Data,dir)*planet.heightScale+planet.radius;
                var point=planet.transform.TransformPoint(dir*ground);camera.transform.position=planet.transform.TransformPoint(dir*(ground+45));var up=planet.transform.TransformDirection(Vector3.Cross(dir,Mathf.Abs(dir.y)>.9f?Vector3.forward:Vector3.up).normalized);
                camera.transform.rotation=Quaternion.LookRotation(point-camera.transform.position,up);camera.nearClipPlane=.1f;
                yield return null;yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(Folder,lake?"lake-close.png":"river-close.png"));yield return null;
            }
        }
        private static IEnumerator Ore(PlanetTerrainMeshBuilder planet,Vector3 point)
        {
            var ore=PlanetOreSystem.Instance;ore.RestoreAmounts(Enumerable.Repeat(1f,4096).ToArray(),64);float before=ore.GetOreAmount(point),taken=0;for(int i=0;i<1000;i++)taken+=ore.ConsumeOre(point,.01f);var batch=ore.GetComponent<OreTextureUploadBatch>();long count=batch.Uploads;
            Check(Mathf.Abs(ore.GetOreAmount(point)-(before-taken))<.005f,"连续开采即时扣除CPU真实矿量");Check(batch.Changes>=1000&&batch.Uploads==count,"1000次开采没有逐次上传GPU");Check(ore.OreMapTexture.GetPixels().All(p=>p.r>.99f),"帧末前纹理仍为上次完整图");yield return null;yield return new WaitForEndOfFrame();Check(batch.Uploads==count+1,"同帧1000次修改只上传一次");Check(ore.OreMapTexture.GetPixels().Any(p=>p.r<.95f),"合并上传反映真实扣量");
            ore.ConsumeOre(point,1);count=batch.Uploads;ore.RestoreAmounts(Enumerable.Repeat(.35f,4096).ToArray(),64);yield return null;yield return new WaitForEndOfFrame();Check(batch.Uploads==count&&Mathf.Abs(ore.GetOreAmount(point)-35)<.01f,"恢复矿图取消旧待上传数据");Check(Mathf.Abs(ore.ConsumeOre(point,1000)-35)<.01f&&ore.GetOreAmount(point)==0,"矿量不足仍只转移真实剩余营养");
        }
        private static IEnumerator Food(Vector3 point)
        {
            var queen=Spawn("ant_founder",point);var member=AntColonyMember.Ensure(queen.GetComponent<PopulationBehavior>());member.Assign(AntRole.Queen,Guid.NewGuid().ToString("N"));var stats=queen.GetComponent<EntityStats>();stats.TryGet("nutrient",out float food);stats.TryGet("storedNutrient",out float reserve);stats.Modify("nutrient",1-food,false);stats.Modify("storedNutrient",-reserve,false);
            var nursery=ColonyResource.Create(new ColonyResourceSave{id=Guid.NewGuid().ToString("N"),group=member.GroupId,kind=ColonyResourceKind.Nursery,energy=10,local=EcologyQueries.ToLocal(point),scale=Vector3.one});
            var first=ColonyFoodSources.Nearest(member,true);long visits=ColonyFoodSources.CandidateVisits,hits=ColonyFoodQueryCache.Hits;for(int i=0;i<100;i++)Check(ColonyFoodSources.Nearest(member,true)==first,"同帧复用相同有效补给");Check(ColonyFoodSources.CandidateVisits==visits&&ColonyFoodQueryCache.Hits>=hits+100,"复用不重复遍历候选");nursery.Remove();Check(ColonyFoodSources.Nearest(member,true)==null,"同帧资源耗尽/移除立即失效");
            var supply=ColonyResource.Create(new ColonyResourceSave{id=Guid.NewGuid().ToString("N"),group=member.GroupId,kind=ColonyResourceKind.Supply,energy=10,local=EcologyQueries.ToLocal(point),scale=Vector3.one});GameplayTuning.Current.colonyShareBackupFood=false;Check(ColonyFoodSources.Nearest(member,true)==null,"关闭备用食物时缓存真实无结果");GameplayTuning.Current.colonyShareBackupFood=true;Check(ColonyFoodSources.Nearest(member,true)==supply,"原地修改共享开关使缓存缺额立即失效");supply.CarryTo(point+Vector3.up*100);Check(ColonyFoodSources.Nearest(member,true)==null,"移动资源离开进食范围后不可继续消费旧坐标");yield return null;
        }
        private static void Pace(Vector3 point)
        {
            var cfg=NaturalPopulationPace.Current;var actor=Spawn("producer",point,AgeState.Infant);var life=actor.GetComponent<EntityLifeCycle>();float speed=NaturalPopulationPace.LifeSpeed(actor.Genes);Check(speed>0&&float.IsFinite(speed)&&Mathf.Abs(speed-StationaryCohortKernel.LifeSpeed(actor.Genes))<1e-8f,"个体与合并组使用同一自然成长速度");life.AdvanceShared(LifeCycleSettings.Load().adultProgress/speed+.001f,speed);Check(actor.Genes.CurrentAge==AgeState.Adult,"参考成长确实跨入成年阶段");
            foreach(float size in new[]{.001f,1,1000}){float days=EcologyReproductionTiming.GestationSeconds(actor.Genes,size)/LifeCycleSettings.Load().secondsPerDay;Check(days>=15&&days<=360,"新孕育时长严格处于15到360游戏天");}Check(EcologyReproductionTiming.CooldownSeconds(actor.Genes)==cfg.postpartumYears*365*LifeCycleSettings.Load().secondsPerDay,"自然代际产后间隔按游戏年结算");
            var logic=actor.Genes.Genome.Genes.Select(actor.Genes.GetLogic).OfType<ReproductionGeneLogic>().First();actor.Genes.IsGestating=true;actor.Genes.Recalculate();logic.RestoreState(new ReproductionSaveState{gestation=123});logic.OnUpdate(1);Check(Mathf.Abs(logic.CaptureState().gestation-122)<.001f,"旧孕育剩余时间不被新时长重置");
            // 这是理想参考谱系投影，关闭环境/变异干扰；不能作为完整生态人口保证。
            var births=new List<double>(Enumerable.Repeat(0d,10));double firstThousand=0;for(double day=0;day<=60*365&&firstThousand==0;day+=1){int count=births.Count;for(int i=0;i<count;i++){double age=day-births[i],interval=cfg.postpartumYears*365+cfg.producerGestationDays;if(age<cfg.maturityYears*365+cfg.producerGestationDays||age>=cfg.elderYears*365)continue;double offset=age-cfg.maturityYears*365-cfg.producerGestationDays;if(Math.Abs(offset%interval)<.1)births.Add(day);}if(births.Count(x=>day-x<cfg.lifespanYears*365)>=1000)firstThousand=day/365;}Check(firstThousand>=45&&firstThousand<=55,"理想10个体参考谱系约50年达到1000，非强制人口增长");File.WriteAllText(Path.Combine(Folder,"ideal-lineage.txt"),"First1000Years="+firstThousand+"\n仅理想参考，正式生态受营养、配偶、环境、变异影响。");
        }
        private static PlanetTerrainData Synthetic(Func<Vector3,float> sample)
        {int sub=48;var data=new PlanetTerrainData{Subdivisions=sub,Radius=100,HeightScale=20,SeaLevel=.5f,FaceHeights=new float[6][]};for(int f=0;f<6;f++){data.FaceHeights[f]=new float[(sub+1)*(sub+1)];for(int y=0;y<=sub;y++)for(int x=0;x<=sub;x++){float u=x*2f/sub-1,v=y*2f/sub-1;var dir=(f<2?new Vector3(f==0?1:-1,v,u):f<4?new Vector3(u,f==2?1:-1,v):new Vector3(u,v,f==4?1:-1)).normalized;data.FaceHeights[f][y*(sub+1)+x]=sample(dir);}}return data;}
        private static IEnumerator Navigation(PlanetTerrainMeshBuilder planet)
        {
            var settings=new LandNavigationSettings{longitudeCells=64,latitudeCells=32};var data=Synthetic(d=>Mathf.Abs(d.x)<.14f&&Mathf.Abs(d.y)<.14f&&d.z>.9f?.35f:.7f);data.Navigation=new LandNavigationGraph(data,settings);var graph=data.Navigation;var a=new Vector3(-.35f,0,1).normalized;var b=new Vector3(.35f,0,1).normalized;Check(!graph.Segment(a,b)&&graph.Connected(a,b),"湖泊阻断直线但两侧陆地仍连通");var old=planet.Data;typeof(PlanetTerrainMeshBuilder).GetProperty("Data").SetValue(planet,data);LandPathScheduler.Configure(planet);
            var route=new LandRoutePlan();var current=planet.transform.TransformPoint(a*(data.Radius+.7f*data.HeightScale));var target=planet.transform.TransformPoint(b*(data.Radius+.7f*data.HeightScale));int steps=0;while(Vector3.Distance(current,target)>3&&steps++<600){if(route.Resolve(planet,current,target,out var waypoint)){var local=PlanetTerrainSampler.WorldToLocalDirection(planet.transform,current);var next=PlanetTerrainSampler.WorldToLocalDirection(planet.transform,waypoint);var moved=Vector3.RotateTowards(local,next,.02f,0).normalized;Check(graph.Dry(moved),"绕湖路径每步保持在陆地");current=planet.transform.TransformPoint(moved*(data.Radius+.7f*data.HeightScale));}yield return null;}Check(Vector3.Distance(current,target)<=3,"全局预算A*真实绕湖抵达目标");
            var islands=Synthetic(d=>Mathf.Abs(d.y)<.2f?.3f:.7f);var separated=new LandNavigationGraph(islands,settings);Check(!separated.Connected(Vector3.up,Vector3.down),"无游泳能力不能跨越海洋连接两块陆地");typeof(PlanetTerrainMeshBuilder).GetProperty("Data").SetValue(planet,old);LandPathScheduler.Configure(planet);
        }
    }
}
