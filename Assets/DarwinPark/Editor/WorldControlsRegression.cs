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
using DarwinPark.Entities.Stats;
using DarwinPark.Genetics;
using DarwinPark.Planet;
using DarwinPark.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace DarwinPark.EditorTools
{
    // ★ 新增：真实按钮、存档、生成/孕育上限以及后台重算原世界的集成回归；低分辨率夹具不作为性能基准。
    [InitializeOnLoad] public static class WorldControlsRegression
    {
        private const string Key="DarwinPark.WorldControls";private static bool _started;private static readonly List<string> Passed=new List<string>();
        private static readonly BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
        private static string Folder=>Path.GetFullPath("Artifacts/WorldControlsReview");
        static WorldControlsRegression(){EditorApplication.update+=()=>{if(!_started&&Application.isPlaying&&SessionState.GetBool(Key,false)&&LanSession.Instance!=null){_started=true;LanSession.Instance.StartCoroutine(Guard());}};}
        public static void RunBatch(){Application.runInBackground=true;SessionState.SetBool(Key,true);EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");EditorApplication.EnterPlaymode();}
        private static void Check(bool condition,string text){if(!condition)throw new InvalidOperationException(text);Passed.Add(text);}
        private static IEnumerator Guard()
        {
            Directory.CreateDirectory(Folder);File.WriteAllText(Path.Combine(Folder,"result.txt"),"RUNNING");Passed.Clear();string error=null;var stack=new Stack<IEnumerator>();stack.Push(Run());
            while(stack.Count>0){bool next=false;object value=null;try{next=stack.Peek().MoveNext();if(next)value=stack.Peek().Current;}catch(Exception ex){error=ex.ToString();Debug.LogException(ex);break;}if(!next){stack.Pop();continue;}if(value is IEnumerator nested)stack.Push(nested);else yield return value;}
            File.WriteAllText(Path.Combine(Folder,"result.txt"),(error==null?"PASS":"FAIL")+"\n"+error+"\n"+string.Join("\n",Passed));SessionState.SetBool(Key,false);Debug.Log("[WorldControls] "+(error==null?"PASS":"FAIL")+" "+Passed.Count);EditorApplication.Exit(error==null?0:1);
        }
        private static DarwinEntity Spawn(Vector3 point)
        {
            var genome=new Genome();foreach(string id in new[]{"gene_growth","gene_nutrition","gene_reproduction","gene_environment"})genome.Add(GeneInstance.Create(id,50,GeneDatabase.Get(id).Tier));
            DarwinEntity result=null;void Added(DarwinEntity e)=>result=e;EntityRegistry.OnRegistered+=Added;try{DarwinEntity.Spawner(genome,point,"世界设置验证");}finally{EntityRegistry.OnRegistered-=Added;}
            if(result==null)return null;result.Genes.mutationChance=0;result.Genes.enabled=false;result.GetComponent<EntityLifeCycle>().RestoreProgress(LifeCycleSettings.Load().adultProgress);result.Genes.RestoreStageState(AgeState.Adult,3,2);result.Genes.Recalculate();
            result.GetComponent<Rigidbody>().isKinematic=true;result.GetComponent<DarwinPark.Entities.Vitals.EntityVitals>().enabled=false;result.GetComponent<DarwinPark.Entities.Vitals.EntityEnvironmentHazard>().enabled=false;return result;
        }
        private static IEnumerator Capture(string name){yield return null;yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(Folder,name+".png"));yield return null;yield return null;}
        private static IEnumerator WaitForUpdate(WorldParameterUpdate update)
        {double start=EditorApplication.timeSinceStartup;int frames=0;while(update.Busy){if(EditorApplication.timeSinceStartup-start>=120)throw new TimeoutException("参数更新超时");frames++;yield return null;}Check(frames>1&&update.LastStatus=="已应用","加载动画跨帧运行并完成后台更新");}
        private static IEnumerator Run()
        {
            yield return null;yield return null;
            var world=Object.FindFirstObjectByType<WorldSessionManager>();var builder=Object.FindFirstObjectByType<PlanetTerrainMeshBuilder>();var climate=PlanetClimateMap.Instance;var clock=ServiceLocator.Get<SimulationClock>();
            builder.subdivisions=16;climate.mapResolution=64;typeof(WorldSessionManager).GetField("_initialTerrain",Private).SetValue(world,JsonUtility.ToJson(builder));world.Create("世界设置隔离回归",742031,.5f,20,44);while(world.Busy)yield return null;clock.Pause();
            var root=ServiceLocator.Get<UIRoot>();var control=Object.FindFirstObjectByType<SimulationControlPanel>();var page=typeof(SimulationControlPanel).GetMethod("SwitchPage",Private);
            var stats=root.MainCanvas.transform.Find("BottomPanel/ContentArea/Page1/MapEntityStatistics").GetComponent<MapEntityStatisticsUI>();
            Check(stats.gameObject.activeInHierarchy,"第一页显示实体统计");yield return Capture("page1");page.Invoke(control,new object[]{1});Check(!stats.gameObject.activeInHierarchy,"第二页不重复显示统计");page.Invoke(control,new object[]{0});yield return null;yield return null;
            Check(stats.gameObject.activeInHierarchy&&stats.GetComponent<MapEntityActions>()!=null,"实体统计和动作只放在第一页");Check(stats.transform.Find("WorldSettings/ThemeIcon").GetComponent<Image>().sprite!=null,"统计右上角齿轮实际加载图标");
            Vector3 home=EcologyQueries.Surface(builder.transform.position+Vector3.up*400);var a=Spawn(home);var b=Spawn(home+Vector3.right*3);var c=Spawn(home+Vector3.forward*3);yield return null;stats.RefreshNow();Check(stats.Total==3,"全地图实体统计与现有存活实体一致");
            stats.transform.Find("WorldSettings").GetComponent<Button>().onClick.Invoke();var settings=WorldGameSettingsPanel.Instance;Check(settings!=null&&settings.Visible&&GameInputGate.Blocked,"齿轮打开本存档设置并拦截背景输入");
            var overlay=root.MainCanvas.transform.Find("WorldGameSettingsOverlay/Panel");overlay.Find("EntityLimit").GetComponent<InputField>().text="3";overlay.Find("Apply").GetComponent<Button>().onClick.Invoke();
            Check(EntityPopulationLimit.Maximum==3&&WorldSaveStore.Read(WorldSaveStore.FileFor(world.Current.id)).gameSettings.entityLimit==3,"实体上限实际写盘读取，属于当前存档");yield return Capture("world-settings");settings.Close();
            Check(Spawn(home+Vector3.right*8)==null&&EntityRegistry.All.Count==3,"工厂在创建前拒绝超额实体");Check(!LanEditActions.Spawn(a.Genes.Genome,home),"手动放置同样受上限约束");
            Check(!RandomPlanetSpawner.Ensure(clock.gameObject).StartBatch(a.Genes.Genome,10,true),"随机生成在满额时有界停止");
            var reproduction=(ReproductionGeneLogic)a.Genes.GetLogic(a.Genes.Genome.Genes.Single(g=>g.DefinitionId=="gene_reproduction"));var nutrients=a.GetComponent<EntityStats>();nutrients.Feed(100);nutrients.TryGet("storedNutrient",out float reserve);reproduction.OnUpdate(.1f);nutrients.TryGet("storedNutrient",out float after);
            Check(!a.Genes.IsGestating&&reserve==after,"达到上限不启动新孕育、不扣储粮");a.Genes.IsGestating=true;a.Genes.Recalculate();reproduction.RestoreState(new ReproductionSaveState{gestation=0});reproduction.OnUpdate(.1f);Check(a.Genes.IsGestating&&EntityRegistry.All.Count==3,"完成孕育在满额时保留并等待空位");
            Object.DestroyImmediate(c.gameObject);reproduction.OnUpdate(.1f);Check(!a.Genes.IsGestating&&EntityRegistry.All.Count==3,"空位释放后真实出生且不突破上限");
            Check(WorldEntityActions.SetLimit(1,false)&&EntityRegistry.All.Count==3,"降低上限保留已有实体");
            using(EntityPopulationLimit.RestoreScope()){Check(Spawn(home+Vector3.right*9)!=null,"读档豁免完整恢复超过上限的已有实体");}Check(!EntityPopulationLimit.CanSpawn,"读档作用域结束立即恢复上限");
            world.SaveNow(false);var overCap=WorldSaveStore.Read(WorldSaveStore.FileFor(world.Current.id));world.Open(overCap);while(world.Busy)yield return null;Check(EntityRegistry.All.Count==4&&EntityPopulationLimit.Maximum==1&&!clock.IsRunning,"实际读档完整恢复超上限的四个实体并保持暂停");
            WorldAuthority.CanEdit=false;yield return null;page.Invoke(control,new object[]{1});yield return null;Check(!root.MainCanvas.transform.Find("BottomPanel/ContentArea/Page2/EntitySpawnPanel").gameObject.activeSelf,"成员第二页隐藏创造控件");page.Invoke(control,new object[]{0});yield return null;
            Check(stats.gameObject.activeInHierarchy&&!root.MainCanvas.transform.Find("BottomPanel/ContentArea/Page2/EntitySpawnPanel").gameObject.activeSelf,"普通成员能看第一页统计但看不到创造功能");Check(!stats.transform.Find("WorldSettings").gameObject.activeSelf&&!stats.transform.Find("ClearAllEntities").gameObject.activeSelf,"普通成员隐藏设置和清除按钮");
            Check(!WorldEntityActions.ClearAll()&&!WorldEntityActions.SetLimit(0,false),"直接调用管理入口也拒绝无权限操作");WorldAuthority.CanEdit=true;yield return null;
            Check(WorldEntityActions.SetLimit(0,false)&&EntityPopulationLimit.CanSpawn,"0可明确设置为不限");
            // 使用真实已有对象验证原地更新，不允许通过重新Open存档重建实体蒙混过关。
            var identities=EntityRegistry.All.ToDictionary(EcologyIdentity.Of,e=>e);var camera=Camera.main;Vector3 cameraPosition=camera.transform.position;Quaternion cameraRotation=camera.transform.rotation;float time=clock.GameTime;string worldId=world.Current.id;
            var terrainData=builder.Data;var update=WorldParameterUpdate.Ensure();float priorTemperature=ClimateDataHub.BaseTemperature;
            Check(update.Request("btn_param_temp",(priorTemperature+1).ToString(System.Globalization.CultureInfo.InvariantCulture),out _),"温度修改进入统一后台更新入口");Check(update.Busy&&WorldRuntimeGate.Loading&&GameInputGate.Blocked&&!root.MainCanvas.enabled&&!clock.IsRunning,"更新期间进度界面接管输入并暂停模拟");
            yield return Capture("parameter-loading");yield return WaitForUpdate(update);
            Check(ReferenceEquals(builder.Data,terrainData)&&Mathf.Abs(ClimateDataHub.BaseTemperature-priorTemperature-1)<.001f,"只改温度不重建地形");
            Check(world.Current.id==worldId&&clock.GameTime==time&&identities.All(p=>EcologyIdentity.Find(p.Key)==p.Value),"原世界身份、实体对象与游戏时间完整保留");
            Check(Vector3.Distance(camera.transform.position,cameraPosition)<.001f&&Quaternion.Angle(camera.transform.rotation,cameraRotation)<.01f,"参数加载后返回原镜头位置");
            Check(!WorldRuntimeGate.Loading&&!GameInputGate.Blocked&&root.MainCanvas.enabled&&!clock.IsRunning,"原本暂停的世界仍保持暂停，界面与输入恢复");
            clock.Play();yield return null;Check(update.Request("btn_param_hum","45",out _),"运行中也可修改湿度");yield return WaitForUpdate(update);Check(clock.IsRunning&&!GameInputGate.Blocked,"更新后恢复原本运行状态");clock.Pause();
            Check(update.Request("btn_param_seed","2147483647",out _),"最大32位整数种子不经float丢失精度");yield return WaitForUpdate(update);Check(builder.seed==int.MaxValue&&!ReferenceEquals(terrainData,builder.Data)&&identities.All(p=>EcologyIdentity.Find(p.Key)==p.Value),"种子重建地形且保留原实体对象");
            Check(update.Request("btn_param_terrain","60",out _),"地形参数正确映射为陆地比例");yield return WaitForUpdate(update);Check(Mathf.Abs(builder.continentRatio-.6f)<.0001f,"地形参数不会误改成湿度");
            Check(!update.Request("btn_param_seed","2147483648",out _)&&!update.Busy,"无效种子不启动加载、不污染世界");
            world.SaveNow(false);var saved=WorldSaveStore.Read(WorldSaveStore.FileFor(world.Current.id));Check(saved.seed==int.MaxValue&&saved.gameSettings.entityLimit==0,"参数与存档设置同次持久化");
            stats.SelectGene("gene_growth");stats.RefreshNow();Check(stats.Total>0,"清理前有真实实体供按钮清除");yield return Capture("page2-actions");stats.transform.Find("ClearAllEntities").GetComponent<Button>().onClick.Invoke();yield return null;stats.RefreshNow();
            Check(EntityRegistry.All.Count==0&&stats.Total==0,"一键清除全部实体并即时更新统计");Check(WorldSaveStore.Read(WorldSaveStore.FileFor(world.Current.id)).entities.Count==0,"清除结果已保存，不会读档复活");
            var old=JsonUtility.FromJson<WorldSaveData>(JsonUtility.ToJson(saved));old.gameSettings=null;WorldSaveStore.Validate(old);Check(old.gameSettings.version==1&&old.gameSettings.entityLimit==3000,"旧档缺少设置时补齐资源默认值");
        }
    }
}
