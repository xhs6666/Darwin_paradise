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
using Object=UnityEngine.Object;

namespace DarwinPark.EditorTools
{
    // ★ 新增：真实输入入口、物理遮挡、实际伤害、活动区与存档往返，避免只验证设置页文字。
    [InitializeOnLoad] public static class ControlsActivityRegression
    {
        private const string Key="DarwinPark.ControlsActivityReview",Pref="DarwinPark.Controls.v1";
        private static bool _started;private static string _savedPrefs,_savedConfig;
        private static readonly BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
        private static readonly List<string> Passed=new List<string>();
        private static string Folder=>Path.GetFullPath("Artifacts/ControlsActivityReview");
        static ControlsActivityRegression(){EditorApplication.update+=()=>{if(!_started&&Application.isPlaying&&SessionState.GetBool(Key,false)&&LanSession.Instance!=null){_started=true;LanSession.Instance.StartCoroutine(Guard());}};}
        public static void RunBatch(){Application.runInBackground=true;EditorApplication.isPaused=false;SessionState.SetBool(Key,true);EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");EditorApplication.EnterPlaymode();}
        private static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);Passed.Add(message);File.WriteAllText(Path.Combine(Folder,"progress.txt"),string.Join("\n",Passed));}
        private static IEnumerator Guard()
        {
            Directory.CreateDirectory(Folder);_savedPrefs=PlayerPrefs.HasKey(Pref)?PlayerPrefs.GetString(Pref):null;_savedConfig=File.Exists(GameplayTuning.FilePath)?File.ReadAllText(GameplayTuning.FilePath):null;
            string error=null;var stack=new Stack<IEnumerator>();stack.Push(Run());
            while(stack.Count>0){bool more=false;object current=null;try{more=stack.Peek().MoveNext();if(more)current=stack.Peek().Current;}catch(Exception ex){error=ex.ToString();Debug.LogException(ex);break;}if(!more){stack.Pop();continue;}if(current is IEnumerator nested)stack.Push(nested);else yield return current;}
            if(_savedPrefs==null)PlayerPrefs.DeleteKey(Pref);else PlayerPrefs.SetString(Pref,_savedPrefs);PlayerPrefs.Save();ControlBindings.ClearCache();
            if(_savedConfig!=null)File.WriteAllText(GameplayTuning.FilePath,_savedConfig);GameplayTuning.Reload();
            File.WriteAllText(Path.Combine(Folder,"result.txt"),(error==null?"PASS":"FAIL")+"\n"+error+"\n"+string.Join("\n",Passed));SessionState.SetBool(Key,false);Debug.Log("[ControlsActivity] "+(error==null?"PASS":"FAIL")+" "+Passed.Count);EditorApplication.Exit(error==null?0:1);
        }
        private static DarwinEntity Spawn(Vector3 point,bool colony=false)
        {
            var genome=new Genome();foreach(var def in new[]{("gene_growth",50f),("gene_locomotion",80f)})genome.Add(GeneInstance.Create(def.Item1,def.Item2,GeneDatabase.Get(def.Item1).Tier));
            if(colony)genome.Add(GeneInstance.Create("gene_population",20,GeneDatabase.Get("gene_population").Tier));
            var before=EntityRegistry.All.ToArray();DarwinEntity.Spawner(genome,point,"控制活动回归");var entity=EntityRegistry.All.Single(e=>!before.Contains(e));
            entity.Genes.mutationChance=0;entity.GetComponent<EntityLifeCycle>().RestoreProgress(LifeCycleSettings.Load().adultProgress);entity.Genes.RestoreStageState(AgeState.Adult,3,2);entity.Genes.Recalculate();
            foreach(var component in entity.GetComponents<MonoBehaviour>())if(component is EntityGeneController||component is DarwinPark.Entities.Vitals.EntityVitals||component is DarwinPark.Entities.Vitals.EntityEnvironmentHazard||component is PlanetGravity||component is EntitySurfaceAnchor||component is PopulationBehavior||component is TerritoryBehavior||component.GetType().Name=="EntityAIController")component.enabled=false;
            entity.GetComponent<Rigidbody>().isKinematic=true;entity.transform.localScale=Vector3.one;return entity;
        }
        private static IEnumerator Capture(string name){yield return null;yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(Folder,name+".png"));yield return null;yield return null;}
        private static IEnumerator Run()
        {
            yield return null;yield return null;
            ControlBindings.RestoreDefaults();var page=new ControlSettingsPage();
            Check(ControlBindings.Get(ControlAction.Attack)==KeyCode.Mouse0&&ControlBindings.Get(ControlAction.Jump)==KeyCode.Space,"默认按键与原玩法一致");
            Check(!ControlBindings.TryBind(ControlAction.Forward,KeyCode.S,out _),"拒绝同场景冲突按键");
            Check(!ControlBindings.TryBind(ControlAction.Attack,KeyCode.Escape,out _),"Esc保留给菜单");
            page.Begin(ControlAction.Attack);page.HandleEvent(new Event{type=EventType.KeyDown,keyCode=KeyCode.F});
            Check(!page.Waiting.HasValue&&ControlBindings.Get(ControlAction.Attack)==KeyCode.F,"设置页捕获键盘攻击键");
            page.Begin(ControlAction.Jump);page.HandleEvent(new Event{type=EventType.KeyDown,keyCode=KeyCode.Escape});
            Check(!page.Waiting.HasValue&&ControlBindings.Get(ControlAction.Jump)==KeyCode.Space,"Esc仅取消改键");
            page.Begin(ControlAction.Attack);page.HandleEvent(new Event{type=EventType.MouseDown,button=1});
            Check(ControlBindings.Get(ControlAction.Attack)==KeyCode.Mouse1,"支持鼠标攻击改键");
            ControlBindings.TryBind(ControlAction.Jump,KeyCode.J,out _);ControlBindings.TryBind(ControlAction.Pause,KeyCode.O,out _);ControlBindings.ClearCache();
            var possession=Object.FindFirstObjectByType<EntityPossessionInput>();
            Check(possession.jumpKey==KeyCode.J&&possession.attackMouseButton==1,"重载偏好后实际操控输入读取新绑定");
            var pause=Object.FindFirstObjectByType<SimulationPauseHotkey>();var clock=ServiceLocator.Get<SimulationClock>();GameInputGate.Blocked=false;
            Check(pause.HandlePauseKey(KeyCode.O,true,false)&&!pause.HandlePauseKey(KeyCode.P,true,false),"暂停使用新键且旧键失效");GameInputGate.Blocked=true;
            ControlBindings.RestoreDefaults();Check(possession.jumpKey==KeyCode.Space&&possession.attackMouseButton==0,"恢复默认立即应用实际输入");

            var session=Object.FindFirstObjectByType<WorldSessionManager>();var builder=Object.FindFirstObjectByType<PlanetTerrainMeshBuilder>();builder.subdivisions=20;typeof(WorldSessionManager).GetField("_initialTerrain",Private).SetValue(session,JsonUtility.ToJson(builder));PlanetClimateMap.Instance.mapResolution=64;PlanetOreSystem.Instance.mapResolution=32;
            session.Create("控制和活动区回归",20261104,.5f,20,44);double start=EditorApplication.timeSinceStartup;while(session.Busy){if(EditorApplication.timeSinceStartup-start>100)throw new TimeoutException("生成超时");yield return null;}
            Check(session.Current!=null,"实际世界初始化");clock.Pause();foreach(var e in EntityRegistry.All.ToArray())Object.DestroyImmediate(e.gameObject);ColonyResource.ClearWorld();
            var menu=Object.FindFirstObjectByType<GameMainMenu>();var root=ServiceLocator.Get<UIRoot>();
            Check(root.MainCanvas.transform.Find("BottomPanel/TabBar/SettingsShortcut")==null,"底栏误加按钮已移除");
            menu.ShowPause();typeof(GameMainMenu).GetField("_page",Private).SetValue(menu,"settings");typeof(GameMainMenu).GetField("_settingsBack",Private).SetValue(menu,"pause");typeof(GameMainMenu).GetField("_settingsTab",Private).SetValue(menu,1);
            Check(GameInputGate.Blocked&&!clock.IsRunning,"改键时暂停并屏蔽底层游戏输入");yield return Capture("controls-settings");
            typeof(GameMainMenu).GetMethod("ReturnFromSettings",Private).Invoke(menu,null);Check((string)typeof(GameMainMenu).GetField("_page",Private).GetValue(menu)=="pause","设置返回原暂停页");typeof(GameMainMenu).GetMethod("Resume",Private).Invoke(menu,null);

            var origin=builder.transform.position+Vector3.up*1000;var self=Spawn(origin+Vector3.up*.5f);var target=Spawn(origin+new Vector3(0,.5f,1.6f));var manual=self.gameObject.AddComponent<EntityManualControl>();manual.BeginControl(builder.transform);
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=origin-Vector3.up*.1f;floor.transform.localScale=new Vector3(20,.2f,20);Physics.SyncTransforms();
            var ray=new Ray(origin+Vector3.up*1.5f,Vector3.forward);manual.SetAim(ray);
            Check(EntityAttackAim.Pick(self.gameObject,ray,10,manual.aimRadius)==target,"扩大判定框命中低于准星的近身实体且不被地面误挡");
            Check(self.GetComponent<Collider>().bounds.size.x<2,"攻击扩框不改变真实物理体积");
            var action=self.Genes.Genome.Genes.First(g=>self.Genes.GetLogic(g) is LocomotionGeneLogic);var logic=self.Genes.GetLogic(action);var stats=target.GetComponent<EntityStats>();stats.TryGet("health",out float beforeHp);manual.SetInput(Vector3.zero,true);logic.OnUpdate(.1f);stats.TryGet("health",out float afterHp);
            Check(afterHp<beforeHp,"真实操控攻击造成目标伤害");
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=origin+new Vector3(0,1,.8f);wall.transform.localScale=new Vector3(3,3,.2f);Physics.SyncTransforms();
            Check(EntityAttackAim.Pick(self.gameObject,ray,10,manual.aimRadius)==null,"扩大判定不能穿墙攻击");Object.DestroyImmediate(wall);
            Check(EntityAttackAim.Pick(self.gameObject,new Ray(origin+Vector3.up*1.5f,Vector3.back),10,manual.aimRadius)==null,"背后和离开准星的实体不会被误击");Object.DestroyImmediate(floor);

            Vector3 home=EcologyQueries.Surface(builder.transform.position+Vector3.up*(builder.radius+builder.heightScale));var queen=AntColonyMember.Ensure(Spawn(home,true).GetComponent<PopulationBehavior>());queen.Assign(AntRole.Queen,Guid.NewGuid().ToString("N"));
            var soldier=AntColonyMember.Ensure(Spawn(home,true).GetComponent<PopulationBehavior>());soldier.Assign(AntRole.Soldier,queen.GroupId);
            var supply=ColonyActivityRange.Boundary(new ActivityCircle("test",home,queen.Radius*.9f),0);queen.State.supplyPoints.Clear();queen.State.supplyPoints.Add(EcologyQueries.ToLocal(supply));ColonyResource.Convert(queen.GroupId,supply,10,false);
            Vector3 extended=ColonyActivityRange.Boundary(new ActivityCircle("test",home,queen.Radius*1.4f),0),outside=ColonyActivityRange.Boundary(new ActivityCircle("test",home,queen.Radius*2.4f),0);
            Check(EcologyQueries.Distance(home,extended)>queen.Radius&&ColonyActivityRange.Contains(soldier.Population,extended),"补给点活动圆扩展到王后圆外");
            Check(!ColonyActivityRange.Contains(soldier.Population,outside),"所有活动圆外的目标被排除");
            target.transform.position=extended;Check(EcologyMotion.CanHunt(soldier.Genes,target),"士兵可巡猎补给点活动区");target.transform.position=outside;Check(!EcologyMotion.CanHunt(soldier.Genes,target),"士兵不能跨出活动区捕猎");
            var terrain=soldier.GetComponent<TerritoryBehavior>();Check(terrain.AllowsTarget(extended)&&!terrain.AllowsTarget(outside),"个人领地判断使用同一活动圆并集");
            var saved=queen.Capture();queen.State.supplyPoints.Clear();queen.Restore(saved);Check(ColonyActivityRange.Contains(soldier.Population,extended),"活动中心随原存档字段往返恢复");
            PopulationRangeLayer.SetVisible(true);yield return null;yield return new WaitForSecondsRealtime(AntColonySettings.Current.scanInterval+.1f);
            Check(PopulationRangeLayer.ActiveCircleCount>=2,"种群图层绘制补给点活动圆");
            var resource=ColonyResource.Convert(queen.GroupId,outside,10,false);Check(ColonyActivityRange.Contains(soldier.Population,outside),"实际额外能量存放点纳入范围");resource.Remove();

            // ★ 适配：暂停时运动代谢为零；先启动模拟，才能核实运行中的配置倍率。
            clock.Play();var tuning=GameplayTuning.Current;var body=self.GetComponent<Rigidbody>();body.isKinematic=false;body.linearVelocity=Vector3.forward*2;float baseCost=EcologyEnergyBudget.Consumption(self.Genes,0,1);
            string config=File.ReadAllText(GameplayTuning.FilePath);var modified=GameplayTuning.Parse(config);modified.movementMetabolismMultiplier=2;File.WriteAllText(GameplayTuning.FilePath,JsonUtility.ToJson(modified,true));GameplayTuning.Reload();
            Check(Mathf.Approximately(EcologyEnergyBudget.Consumption(self.Genes,0,1),baseCost*2)&&baseCost>0,"外部配置令实际行动饱食消耗翻倍");
            modified.movementMetabolismMultiplier=0;File.WriteAllText(GameplayTuning.FilePath,JsonUtility.ToJson(modified));GameplayTuning.Reload();Check(EcologyEnergyBudget.Consumption(self.Genes,0,1)==0,"零倍率关闭行动消耗且不影响基础参数");
            bool rejected=false;try{GameplayTuning.Parse("{\"version\":1,\"movementMetabolismMultiplier\":-1}");}catch(InvalidDataException){rejected=true;}Check(rejected,"负数配置被明确拒绝");
            File.WriteAllText(GameplayTuning.FilePath,config);GameplayTuning.Reload();Check(Mathf.Approximately(GameplayTuning.Current.movementMetabolismMultiplier,tuning.movementMetabolismMultiplier),"配置恢复后重新加载生效");
            yield return Capture("activity-circles");
        }
    }
}
