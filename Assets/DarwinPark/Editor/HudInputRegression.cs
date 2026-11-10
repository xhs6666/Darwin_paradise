using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DarwinPark.Core;
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
    // ★ 新增：从真实存档副本加载后，检查底栏/筛选/种群的射线与实际输入模块点击。
    [InitializeOnLoad] public static class HudInputRegression
    {
        private const string Key="DarwinPark.HudInputReview";private static bool _started;
        private static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private static readonly List<string> Passed=new();
        private static string Folder=>Path.GetFullPath("Artifacts/HudInputReview");
        static HudInputRegression(){EditorApplication.update+=()=>{if(!_started&&Application.isPlaying&&SessionState.GetBool(Key,false)&&LanSession.Instance!=null){_started=true;LanSession.Instance.StartCoroutine(Guard());}};}
        public static void RunBatch(){Application.runInBackground=true;SessionState.SetBool(Key,true);EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");EditorApplication.EnterPlaymode();}
        private static void Check(bool value,string text){if(!value)throw new InvalidOperationException(text);Passed.Add(text);File.WriteAllLines(Path.Combine(Folder,"progress.txt"),Passed);}
        private static IEnumerator Guard()
        {
            Directory.CreateDirectory(Folder);Passed.Clear();File.WriteAllText(Path.Combine(Folder,"diagnostics.txt"),"");File.WriteAllText(Path.Combine(Folder,"result.txt"),"RUNNING");string error=null;var stack=new Stack<IEnumerator>();stack.Push(Run());
            while(stack.Count>0){bool next=false;object current=null;try{next=stack.Peek().MoveNext();if(next)current=stack.Peek().Current;}catch(Exception ex){error=ex.ToString();Debug.LogException(ex);break;}if(!next){stack.Pop();continue;}if(current is IEnumerator nested)stack.Push(nested);else yield return current;}
            File.WriteAllText(Path.Combine(Folder,"result.txt"),(error==null?"PASS":"FAIL")+"\n"+error+"\n"+string.Join("\n",Passed));SessionState.SetBool(Key,false);EditorApplication.Exit(error==null?0:1);
        }
        private static IEnumerator Wait(Func<bool> ready){double start=EditorApplication.timeSinceStartup;while(!ready()){if(EditorApplication.timeSinceStartup-start>90)throw new TimeoutException("世界加载超时");yield return null;}}
        private static Vector2 Center(RectTransform rect){var c=new Vector3[4];rect.GetWorldCorners(c);return RectTransformUtility.WorldToScreenPoint(null,(c[0]+c[2])*.5f);}
        private static void Dump(string label)
        {
            var root=ServiceLocator.Get<UIRoot>();var events=EventSystem.current;var menu=Object.FindFirstObjectByType<GameMainMenu>();
            var lines=new List<string>{label,"canvas="+root.MainCanvas.enabled+" raycaster="+root.MainCanvas.GetComponent<GraphicRaycaster>().enabled+" blocked="+GameInputGate.Blocked+" menu="+menu.Visible+" cursor="+Cursor.lockState,"events="+(events!=null?events.enabled.ToString():"missing")+" module="+events?.currentInputModule?.GetType().Name+" focused="+events?.isFocused};
            foreach(var group in root.MainCanvas.GetComponentsInChildren<CanvasGroup>(true))if(group.gameObject.activeInHierarchy)lines.Add("group "+group.name+" interact="+group.interactable+" raycasts="+group.blocksRaycasts);
            File.AppendAllLines(Path.Combine(Folder,"diagnostics.txt"),lines);
        }
        private static GameObject Click(Button button)
        {
            Canvas.ForceUpdateCanvases();var events=EventSystem.current;var data=new PointerEventData(events){position=Center(button.GetComponent<RectTransform>()),button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();events.RaycastAll(data,hits);
            File.AppendAllText(Path.Combine(Folder,"diagnostics.txt"),"button="+button.name+" interact="+button.IsInteractable()+" hits="+string.Join(",",hits.Take(8).Select(h=>h.gameObject.name))+"\n");
            if(hits.Count==0)throw new InvalidOperationException("按钮没有UI射线命中："+button.name);
            var target=hits[0].gameObject;data.pointerCurrentRaycast=hits[0];ExecuteEvents.ExecuteHierarchy(target,data,ExecuteEvents.pointerDownHandler);ExecuteEvents.ExecuteHierarchy(target,data,ExecuteEvents.pointerUpHandler);ExecuteEvents.ExecuteHierarchy(target,data,ExecuteEvents.pointerClickHandler);return target;
        }
        private static void ModuleClick(Button button)
        {
            Canvas.ForceUpdateCanvases();var events=EventSystem.current;var module=events.GetComponent<StandaloneInputModule>();
            if(module==null)throw new InvalidOperationException("测试夹具需要原生鼠标模块");
            bool focused=events.isFocused;var previous=module.inputOverride;var mouse=events.gameObject.AddComponent<HudRegressionInput>();mouse.Position=Center(button.GetComponent<RectTransform>());
            // 夹具不依赖测试窗口是否被桌面遮挡；实际UI仍通过完整的输入模块状态机处理。
            events.SendMessage("OnApplicationFocus",true,SendMessageOptions.DontRequireReceiver);
            try{module.inputOverride=mouse;mouse.Down=mouse.Held=true;module.Process();mouse.Down=mouse.Held=false;mouse.Up=true;module.Process();mouse.Up=false;}
            finally{module.inputOverride=previous;Object.DestroyImmediate(mouse);events.SendMessage("OnApplicationFocus",focused,SendMessageOptions.DontRequireReceiver);}
        }
        private static IEnumerator Run()
        {
            yield return null;yield return null;
            var session=Object.FindFirstObjectByType<WorldSessionManager>();session.autoSaveInterval=36000;var planet=Object.FindFirstObjectByType<PlanetTerrainMeshBuilder>();PlanetClimateMap.Instance.mapResolution=64;PlanetOreSystem.Instance.mapResolution=32;
            // 模拟脚本重载造成的非持久UnityEvent丢失，旧_uiBuilt仍为true。
            foreach(var stale in ServiceLocator.Get<UIRoot>().MainCanvas.GetComponentsInChildren<Button>(true))stale.onClick.RemoveAllListeners();
            string input=Path.Combine(Folder,"input/world.json");
            if(File.Exists(input)){var world=WorldSaveStore.Read(input);JsonUtility.FromJsonOverwrite(world.terrainParameters,planet);planet.subdivisions=24;world.terrainParameters=JsonUtility.ToJson(planet);world.running=false;session.Open(world);}
            else {planet.subdivisions=24;typeof(WorldSessionManager).GetField("_initialTerrain",Private).SetValue(session,JsonUtility.ToJson(planet));session.Create("界面点击回归",27291,.5f,20,44);}
            yield return Wait(()=>!session.Busy);yield return null;yield return null;
            var loadedClock=ServiceLocator.Get<SimulationClock>();var loadedPanel=Object.FindFirstObjectByType<SimulationControlPanel>();
            if(File.Exists(input))
            {
                Check(!loadedClock.IsRunning&&((Image)typeof(SimulationControlPanel).GetField("_playIcon",Private).GetValue(loadedPanel)).sprite==(Sprite)typeof(SimulationControlPanel).GetField("_iconPlay",Private).GetValue(loadedPanel),"暂停存档加载后按钮立即显示播放，而非默认运行图标");
                float beforePlay=loadedClock.GameTime;ModuleClick(loadedPanel.GetComponentsInChildren<Button>().Single(b=>b.name=="PlayBtn"));yield return null;yield return null;yield return null;
                Check(loadedClock.IsRunning&&loadedClock.GameTime>beforePlay,"读档后真实点击播放，状态和模拟时间一起推进");
            }
            loadedClock.Pause();Object.FindFirstObjectByType<RandomPlanetSpawner>().enabled=false;yield return null;yield return null;Dump("loaded");
            var panel=Object.FindFirstObjectByType<SimulationControlPanel>();var root=ServiceLocator.Get<UIRoot>();
            if(File.Exists(Path.Combine(Folder,"interactive.flag")))
            {
                foreach(var button in root.MainCanvas.GetComponentsInChildren<Button>(true)){var copy=button;button.onClick.AddListener(()=>File.AppendAllText(Path.Combine(Folder,"native-clicks.txt"),"CLICK "+copy.name+" frame="+Time.frameCount+"\n"));}
                File.WriteAllText(Path.Combine(Folder,"interactive-ready.txt"),"READY");float until=Time.realtimeSinceStartup+120;
                while(Time.realtimeSinceStartup<until&&!File.Exists(Path.Combine(Folder,"interactive-done.flag"))){yield return null;}
                PopulationWorkbench.Instance?.Close();typeof(SimulationControlPanel).GetMethod("SwitchPage",Private).Invoke(panel,new object[]{0});yield return null;
                Dump("after native clicks");
            }
            var next=(Button)typeof(SimulationControlPanel).GetField("_tabNextBtn",Private).GetValue(panel);Click(next);yield return null;
            Check((int)typeof(SimulationControlPanel).GetField("_currentPage",Private).GetValue(panel)==1,"旧回调丢失后读档仍重建底栏实际点击事件");
            Check(root.MainCanvas.GetComponentsInChildren<RectTransform>().Count(r=>r.name=="BottomPanel")==1,"读档只保留一个活动底栏，不叠加旧遮罩");
            var list=root.MainCanvas.GetComponentsInChildren<Button>().Single(b=>b.name=="AllPopulations");Click(list);yield return null;Check(PopulationWorkbench.Instance?.Visible==true,"存档加载后种群列表实际点击打开");PopulationWorkbench.Instance.Close();yield return null;Dump("population closed");
            var previous=(Button)typeof(SimulationControlPanel).GetField("_tabPrevBtn",Private).GetValue(panel);Click(previous);yield return null;
            yield return null;yield return null;
            // ★ 新增：分别覆盖宽屏与窄屏入口，布局调整只属于测试，最后恢复，不用窗口碰巧足够宽作为隐含前提。
            var filter=root.MainCanvas.GetComponentInChildren<MapEntityGeneFilterUI>();var launch=filter.GetComponentsInChildren<Button>(true).Single(b=>b.name=="OpenEntityFilter");var statistics=filter.GetComponent<MapEntityStatisticsUI>();float originalLeft=statistics.leftOffset,originalMinimum=filter.minimumWidth;
            statistics.leftOffset=12;yield return null;yield return null;Check(filter.WideLayout,"夹具实际进入宽屏停靠布局");
            bool wasVisible=filter.Card.gameObject.activeSelf;ModuleClick(launch);yield return null;Check(filter.Card.gameObject.activeSelf!=wasVisible,"宽屏筛选标题按钮实际改变卡片可见状态");ModuleClick(launch);yield return null;Check(filter.Card.gameObject.activeSelf==wasVisible,"再次点击宽屏筛选恢复原状态");
            statistics.leftOffset=originalLeft;filter.minimumWidth=100000;yield return null;yield return null;Check(!filter.WideLayout,"夹具实际进入窄屏浮卡布局");wasVisible=filter.Card.gameObject.activeSelf;ModuleClick(launch);yield return null;Check(filter.Card.gameObject.activeSelf!=wasVisible,"窄屏筛选标题按钮实际改变卡片可见状态");
            filter.minimumWidth=originalMinimum;yield return null;yield return null;
            var dropdown=root.MainCanvas.GetComponentsInChildren<Dropdown>().First(d=>d.name=="IncludeGenes");
            Canvas.ForceUpdateCanvases();var pointer=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left,position=Center(dropdown.GetComponent<RectTransform>())};ExecuteEvents.Execute(dropdown.gameObject,pointer,ExecuteEvents.pointerClickHandler);yield return null;yield return null;
            Check(dropdown.transform.Find("Dropdown List")!=null,"存档加载后筛选下拉可以展开");RuntimeDropdownDismissal.CloseOpenPopups();yield return new WaitForSecondsRealtime(.3f);Dump("dropdown closed");
            ModuleClick(next);yield return null;Check((int)typeof(SimulationControlPanel).GetField("_currentPage",Private).GetValue(panel)==1,"关闭筛选下拉后真实输入模块仍可翻页");
            var recovery=Object.FindFirstObjectByType<WorldHudInputLifecycle>();Check(recovery!=null,"菜单实际安装界面输入恢复组件");
            var menu=Object.FindFirstObjectByType<GameMainMenu>();var raycaster=root.MainCanvas.GetComponent<GraphicRaycaster>();
            // 复现旧快照已经是关闭状态：旧菜单返回会原样留下可见性/射线损坏。
            root.MainCanvas.enabled=false;raycaster.enabled=false;menu.ShowPause();yield return null;
            Check(!root.MainCanvas.enabled&&!raycaster.enabled,"暂停菜单有意隐藏HUD时恢复器不抢开射线");
            typeof(GameMainMenu).GetMethod("Resume",Private).Invoke(menu,null);yield return null;yield return null;
            Check(root.MainCanvas.enabled&&raycaster.enabled,"暂停返回后修复旧快照中的关闭画布和射线");
            ModuleClick(previous);yield return null;Check((int)typeof(SimulationControlPanel).GetField("_currentPage",Private).GetValue(panel)==0,"恢复后原生输入模块可以点击上一页");
            var events=EventSystem.current;foreach(var inputModule in events.GetComponents<BaseInputModule>())inputModule.enabled=false;events.enabled=false;yield return null;yield return null;
            Check(events.enabled&&events.GetComponents<BaseInputModule>().Count(m=>m.enabled)==1,"停用的事件系统和输入模块恢复且不重复安装");
            ModuleClick(next);yield return null;Check((int)typeof(SimulationControlPanel).GetField("_currentPage",Private).GetValue(panel)==1,"恢复事件模块后真实指针流程可翻页");
            Object.DestroyImmediate(EventSystem.current.gameObject);yield return null;yield return null;
            Check(EventSystem.current!=null&&Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Count(e=>e.isActiveAndEnabled)==1,"丢失事件节点后补建唯一可用系统");
            Object.DestroyImmediate(root.MainCanvas.GetComponent<GraphicRaycaster>());yield return null;yield return null;
            Check(root.MainCanvas.GetComponents<GraphicRaycaster>().Length==1&&root.MainCanvas.GetComponent<GraphicRaycaster>().enabled,"丢失射线组件后补建且不重复");
            GameInputGate.Blocked=true;yield return null;Check(!GameInputGate.Blocked,"没有实际弹窗的遗留输入门控被释放");
            GameInputGate.ConsumeEscape();recovery.Recover();Check(GameInputGate.Blocked,"Esc当帧防穿透标记仍保留");yield return null;
            list=root.MainCanvas.GetComponentsInChildren<Button>().Single(b=>b.name=="AllPopulations");ModuleClick(list);yield return null;
            Check(PopulationWorkbench.Instance.Visible&&GameInputGate.Blocked,"种群窗口打开时保留合法门控");
            int page=(int)typeof(SimulationControlPanel).GetField("_currentPage",Private).GetValue(panel);ModuleClick(previous);yield return null;
            Check((int)typeof(SimulationControlPanel).GetField("_currentPage",Private).GetValue(panel)==page,"种群窗口透明遮罩仍阻止点击底栏");PopulationWorkbench.Instance.Close();yield return null;
            var settings=WorldGameSettingsPanel.Ensure();settings.Open();yield return null;Check(settings.Visible&&GameInputGate.Blocked,"世界设置弹窗合法门控不被释放");settings.Close();yield return null;
            var work=RandomSpawnWorkbench.Ensure();work.Open(Object.FindFirstObjectByType<RandomSpawnPanel>(),false);yield return null;
            Check(work.Visible&&GameInputGate.Blocked,"原生生成窗口保持可交互及合法门控");work.Close();yield return null;
            WorldRuntimeGate.Loading=true;root.MainCanvas.enabled=false;root.MainCanvas.GetComponent<GraphicRaycaster>().enabled=false;yield return null;
            Check(!root.MainCanvas.enabled&&!root.MainCanvas.GetComponent<GraphicRaycaster>().enabled,"加载阶段不强开HUD和点击射线");WorldRuntimeGate.Loading=false;yield return null;
            Check(root.MainCanvas.enabled&&root.MainCanvas.GetComponent<GraphicRaycaster>().enabled,"加载标记解除后自动恢复点击链路");
            bool authority=WorldAuthority.CanEdit;WorldAuthority.CanEdit=false;yield return null;yield return null;
            typeof(SimulationControlPanel).GetMethod("SwitchPage",Private).Invoke(panel,new object[]{0});yield return null;
            var play=root.MainCanvas.GetComponentsInChildren<Button>().Single(b=>b.name=="PlayBtn");Check(!play.interactable,"只读权限禁用按钮保持禁用");WorldAuthority.CanEdit=authority;
            menu.ShowPause();for(int cycle=0;cycle<3;cycle++){menu.enabled=false;menu.enabled=true;}
            var loaded=(Action)typeof(WorldSessionManager).GetField("Loaded",Private).GetValue(session);
            Check(loaded.GetInvocationList().Count(d=>d.Target is GameMainMenu bound&&bound==menu)==1,"菜单反复启停后读档通知只有一个有效绑定");
            if(File.Exists(input)){var runningWorld=WorldSaveStore.Read(input);runningWorld.running=true;session.Open(runningWorld);yield return Wait(()=>!session.Busy);yield return null;yield return null;yield return null;
                Check(!menu.Visible&&root.MainCanvas.enabled&&root.MainCanvas.GetComponent<GraphicRaycaster>().enabled,"菜单重新启用后再次进入存档可正常返回可交互HUD");
                Check(loadedClock.IsRunning&&loadedClock.GameDeltaTime>0,"运行中存档加载完成自动恢复真实模拟，而非只改图标");loadedClock.Pause();}
            Dump("recovery scenarios complete");
            yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(Folder,"loaded-hud.png"));yield return null;
        }
    }
}
