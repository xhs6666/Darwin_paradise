using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DarwinPark.Core;
using DarwinPark.Entities;
using DarwinPark.Entities.Ecology;
using DarwinPark.Entities.Stats;
using DarwinPark.Genetics;
using DarwinPark.Planet;
using DarwinPark.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace DarwinPark.EditorTools
{
    // ★ 新增：自动检查设置导航、Shader数据、焦点及外部配置；RunInteractive供原生鼠标点击验收。
    [InitializeOnLoad] public static class LayersSettingsRegression
    {
        private const string Key="DarwinPark.LayersSettingsReview";
        private static bool _started;private static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private static readonly List<string> Passed=new List<string>();private static string _config,_prefs;private static bool _hadPrefs;
        private static string Folder=>Path.GetFullPath("Artifacts/LayersSettingsReview");
        static LayersSettingsRegression(){EditorApplication.update+=InteractiveStart;EditorApplication.update+=()=>{if(!_started&&Application.isPlaying&&SessionState.GetBool(Key,false)&&LanSession.Instance!=null){_started=true;LanSession.Instance.StartCoroutine(Guard());}};}
        public static void RunBatch(){Application.runInBackground=true;SessionState.SetBool(Key,true);EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");var view=EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));view.Show();view.Focus();EditorApplication.EnterPlaymode();}
        public static void RunInteractive(){Application.runInBackground=true;SessionState.SetBool("DarwinPark.SettingsInteractive",true);EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");EditorApplication.EnterPlaymode();}
        private static void InteractiveStart()
        {
            if(!SessionState.GetBool("DarwinPark.SettingsInteractive",false)||!Application.isPlaying||Object.FindFirstObjectByType<GameMainMenu>()==null)return;EditorApplication.update-=InteractiveStart;
            var menu=Object.FindFirstObjectByType<GameMainMenu>();menu.StartCoroutine(Interactive(menu));
        }
        private static IEnumerator Interactive(GameMainMenu menu)
        {
            yield return null;Set(menu,"_page","settings");Set(menu,"_settingsBack","main");var view=EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));view.Show();view.Focus();
            while(Page(menu)=="settings")yield return null;
            Directory.CreateDirectory(Folder);File.WriteAllText(Path.Combine(Folder,"real-pointer-result.txt"),"PASS: native mouse returned from settings to "+Page(menu));SessionState.SetBool("DarwinPark.SettingsInteractive",false);EditorApplication.Exit(0);
        }
        private static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);Passed.Add(message);File.WriteAllText(Path.Combine(Folder,"progress.txt"),string.Join("\n",Passed));}
        private static void Set(object target,string field,object value)=>target.GetType().GetField(field,Private).SetValue(target,value);
        private static string Page(GameMainMenu menu)=>(string)typeof(GameMainMenu).GetField("_page",Private).GetValue(menu);
        private static IEnumerator Guard()
        {
            Directory.CreateDirectory(Folder);_config=File.ReadAllText(GameplayTuning.FilePath);_hadPrefs=PlayerPrefs.HasKey("DarwinPark.Controls.v1");_prefs=PlayerPrefs.GetString("DarwinPark.Controls.v1");
            string error=null;var stack=new Stack<IEnumerator>();stack.Push(Run());
            while(stack.Count>0){bool more=false;object current=null;try{more=stack.Peek().MoveNext();if(more)current=stack.Peek().Current;}catch(Exception ex){error=ex.ToString();Debug.LogException(ex);break;}if(!more){stack.Pop();continue;}if(current is IEnumerator nested)stack.Push(nested);else yield return current;}
            File.WriteAllText(GameplayTuning.FilePath,_config);GameplayTuning.Reload();if(_hadPrefs)PlayerPrefs.SetString("DarwinPark.Controls.v1",_prefs);else PlayerPrefs.DeleteKey("DarwinPark.Controls.v1");PlayerPrefs.Save();ControlBindings.ClearCache();
            File.WriteAllText(Path.Combine(Folder,"result.txt"),(error==null?"PASS":"FAIL")+"\n"+error+"\n"+string.Join("\n",Passed));SessionState.SetBool(Key,false);Debug.Log("[LayersSettings] "+(error==null?"PASS":"FAIL")+" "+Passed.Count);EditorApplication.Exit(error==null?0:1);
        }
        private static IEnumerator Capture(string name){yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(Folder,name+".png"));yield return null;yield return null;}
        private static DarwinEntity Spawn(Vector3 point,string group,AntRole role)
        {
            var genome=new Genome();foreach(var def in new[]{("gene_growth",50f),("gene_population",20f)})genome.Add(GeneInstance.Create(def.Item1,def.Item2,GeneDatabase.Get(def.Item1).Tier));
            var before=EntityRegistry.All.ToArray();DarwinEntity.Spawner(genome,point,"种群回归");var entity=EntityRegistry.All.Single(e=>!before.Contains(e));entity.Genes.mutationChance=0;entity.Genes.RestoreStageState(AgeState.Adult,3,2);entity.Genes.Recalculate();
            foreach(var component in entity.GetComponents<MonoBehaviour>())if(component is EntityGeneController||component is DarwinPark.Entities.Vitals.EntityVitals||component is DarwinPark.Entities.Vitals.EntityEnvironmentHazard||component is PlanetGravity||component is EntitySurfaceAnchor||component is PopulationBehavior||component.GetType().Name=="EntityAIController")component.enabled=false;
            entity.GetComponent<Rigidbody>().isKinematic=true;AntColonyMember.Ensure(entity.GetComponent<PopulationBehavior>()).Assign(role,group);return entity;
        }
        private static IEnumerator Run()
        {
            yield return null;yield return null;ControlBindings.RestoreDefaults();
            var config=GameplayTuning.Parse("{/*中文*/\"version\":1,//行动消耗\n\"movementMetabolismMultiplier\":0.25}");Check(config.movementMetabolismMultiplier==.25f,"中文行注释与块注释可解析");
            Check(CommentedJson.Strip("{\"path\":\"https://example/a/*b*/\"}").Contains("https://example/a/*b*/"),"注释解析保留字符串中的URL和斜杠");
            config.movementMetabolismMultiplier=.4f;File.WriteAllText(GameplayTuning.FilePath,"// 外部编辑\n"+JsonUtility.ToJson(config));File.SetLastWriteTimeUtc(GameplayTuning.FilePath,DateTime.UtcNow.AddSeconds(2));yield return new WaitForSecondsRealtime(2.2f);GameplayTuning.Poll();Check(GameplayTuning.Current.movementMetabolismMultiplier==.4f,"磁盘外部修改无需重启自动生效");
            File.WriteAllText(GameplayTuning.FilePath,_config);GameplayTuning.Reload();
            var menu=Object.FindFirstObjectByType<GameMainMenu>();
            foreach(int tab in new[]{0,1,2})
            {
                Set(menu,"_page","settings");Set(menu,"_settingsBack","main");Set(menu,"_settingsTab",tab);yield return null;yield return null;
                Check(menu.SettingsWindowRect.width==Mathf.Min(480,Screen.width-32)&&menu.SettingsWindowRect.height==Mathf.Min(560,Screen.height-32),"设置恢复旧尺寸 分页"+tab);
                if(tab==0)yield return Capture("settings-old-size");typeof(GameMainMenu).GetMethod("ReturnFromSettings",Private).Invoke(menu,null);Check(Page(menu)=="main","设置分页返回原主菜单 分页"+tab);
            }
            var session=Object.FindFirstObjectByType<WorldSessionManager>();var builder=Object.FindFirstObjectByType<PlanetTerrainMeshBuilder>();builder.subdivisions=32;Set(session,"_initialTerrain",JsonUtility.ToJson(builder));PlanetClimateMap.Instance.mapResolution=64;PlanetOreSystem.Instance.mapResolution=32;
            session.Create("图层和设置验证",20261104,.5f,20,44);double start=EditorApplication.timeSinceStartup;while(session.Busy){if(EditorApplication.timeSinceStartup-start>100)throw new TimeoutException("世界加载超时");yield return null;}var clock=ServiceLocator.Get<SimulationClock>();clock.Pause();foreach(var e in EntityRegistry.All.ToArray())Object.DestroyImmediate(e.gameObject);ColonyResource.ClearWorld();
            menu.ShowPause();Set(menu,"_page","settings");Set(menu,"_settingsBack","pause");Set(menu,"_settingsTab",1);yield return null;typeof(GameMainMenu).GetMethod("ReturnFromSettings",Private).Invoke(menu,null);Check(Page(menu)=="pause"&&GameInputGate.Blocked,"设置返回暂停菜单并保留门控");typeof(GameMainMenu).GetMethod("Resume",Private).Invoke(menu,null);
            Vector3 home=EcologyQueries.Surface(builder.transform.position+Vector3.up*(builder.radius+builder.heightScale));string group=Guid.NewGuid().ToString("N");var queen=Spawn(home,group,AntRole.Queen).GetComponent<AntColonyMember>();Spawn(home,group,AntRole.Soldier);
            Check(queen.Radius<AntColonySettings.Current.colonyRadius,"种群初始活动半径缩小");Check(queen.State.supplyPoints.Count==0&&ColonyActivityRange.Circles(queen.Population).Count()==1,"没有多余能量时不预建检查点或范围");
            Vector3 supply=ColonyActivityRange.Boundary(new ActivityCircle("t",home,queen.Radius*.8f),0);queen.State.supplyPoints.Add(EcologyQueries.ToLocal(supply));Check(ColonyActivityRange.Circles(queen.Population).Count()==1,"旧档空检查点也不产生范围");
            var resource=ColonyResource.Convert(group,supply,10,false);Check(ColonyActivityRange.Circles(queen.Population).Count()==2,"实际有剩余能量后扩展活动圆");
            var control=Object.FindFirstObjectByType<SimulationControlPanel>();control.SetLayer("layer_population");yield return null;
            Check(PopulationRangeLayer.Visible&&!PopulationRangeLayer.Outline&&PopulationRangeLayer.ActiveCircleCount==2,"默认以合并半透明区域显示");
            var renderer=builder.GetComponent<MeshRenderer>();var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block);Check(block.GetVector("_PopulationStyle").x==1&&block.GetTexture("_PopulationData")!=null&&renderer.sharedMaterial.shader.isSupported,"真实地表Shader收到填色及合并圆数据");
            var orbit=Camera.main.GetComponent<OrbitCamera>();Vector3 cameraPoint=home+EcologyQueries.Up(home)*110;orbit.RestoreView(cameraPoint,Quaternion.LookRotation(home-cameraPoint,Vector3.forward),Vector3.Distance(cameraPoint,builder.transform.position));yield return Capture("population-fill");
            var root=ServiceLocator.Get<UIRoot>();var button=root.MainCanvas.transform.Find("BottomPanel/ContentArea/Page1/LayerSec/layer_population").GetComponent<Button>();
            button.GetComponent<RightClickHandler>().OnPointerClick(new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Right});yield return null;renderer.GetPropertyBlock(block);Check(PopulationRangeLayer.Outline&&block.GetVector("_PopulationStyle").x==2,"按钮右键切换合并外轮廓");yield return Capture("population-union-outline");
            EventSystem.current.SetSelectedGameObject(button.gameObject);button.onClick.Invoke();yield return null;renderer.GetPropertyBlock(block);Check(!PopulationRangeLayer.Visible&&control.CurrentLayer=="default"&&block.GetFloat("_PopulationCount")==0,"左键取消实际清除范围及Shader状态");Check(button.transition==Selectable.Transition.None&&button.image.sprite==UIVisualTheme.SurfaceSprite("button"),"焦点停在按钮上也不残留绿色选中状态");
            Check(control.HandleLayerKey(ControlBindings.Get(ControlAction.CycleLayer))&&control.CurrentLayer!="default","默认切换图层快捷键生效");Check(control.HandleLayerKey(ControlBindings.Get(ControlAction.ClearLayer))&&control.CurrentLayer=="default","默认取消图层快捷键生效");
            ControlBindings.TryBind(ControlAction.CycleLayer,KeyCode.K,out _);Check(!control.HandleLayerKey(KeyCode.L)&&control.HandleLayerKey(KeyCode.K),"设置改键后图层新键生效旧键失效");Check(!control.HandleLayerKey(KeyCode.K,true),"输入框打字不触发图层快捷键");GameInputGate.Blocked=true;Check(!control.HandleLayerKey(KeyCode.K),"菜单门控期间不触发图层切换");GameInputGate.Blocked=false;
            resource.State.energy=0;Check(ColonyActivityRange.Circles(queen.Population).Count()==1,"补给耗尽立即移除检查点范围");resource.Remove();
            var stats=Object.FindFirstObjectByType<MapEntityStatisticsUI>();var population=stats.GetComponent<MapPopulationListUI>();yield return null;population.RefreshNow();Check(population.GroupCount==1&&population.MemberCount==2,"种群列表正确统计活群与成员");Check(population.Card.rect.width==stats.preferredWidth&&population.Card.rect.height==stats.preferredHeight&&stats.GetComponent<MapEntityGeneFilterUI>().preferredWidth==stats.preferredWidth,"三张卡片使用同规格宽高");Check(population.Locate(0),"列表支持定位王后或领袖");population.OpenList();yield return null;Check(root.MainCanvas.transform.Find("PopulationListWindow").gameObject.activeSelf,"种群列表可以展开查看");yield return Capture("population-list");
            foreach(var e in EntityRegistry.All.ToArray())Object.DestroyImmediate(e.gameObject);population.RefreshNow();Check(population.GroupCount==0&&population.MemberCount==0,"种群灭绝后列表同步清空");
        }
    }
}
