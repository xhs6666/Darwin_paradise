using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DarwinPark.Core;
using DarwinPark.Entities;
using DarwinPark.Genetics;
using DarwinPark.Planet;
using DarwinPark.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace DarwinPark.EditorTools
{
    // ★ 新增：真实构建界面并保存九页截图，检查主题资源、原锚点及交互链路；测试世界使用独立 ID。
    [InitializeOnLoad]
    public static class UIStyleRegression
    {
        private const string Key="DarwinPark.UIStyleReview";
        private static readonly List<string> Passed=new List<string>();
        private static string Folder=>Path.GetFullPath("Artifacts/UIStyleReview");
        private static string _testSave;
        private static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static UIStyleRegression()
        {
            EditorApplication.playModeStateChanged+=state=>
            {
                if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Key,false))EditorApplication.delayCall+=Validate;
            };
        }
        public static void RunBatch()
        {
            SessionState.SetBool(Key,true);Directory.CreateDirectory(Folder);
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            var view=EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));
            view.position=new Rect(20,20,1600,940);view.Show();
            EditorApplication.EnterPlaymode();
        }
        [MenuItem("Tools/Darwin Park/Validate Reference UI Style")]
        public static void Validate()
        {
            var session=Object.FindFirstObjectByType<WorldSessionManager>();
            if(!Application.isPlaying||session==null||session.Current!=null)throw new InvalidOperationException("请在未加载世界的启动菜单执行");
            Directory.CreateDirectory(Folder);Passed.Clear();session.StartCoroutine(Guard(Run()));
        }
        private static object Get(object target,string name)=>target.GetType().GetField(name,Private).GetValue(target);
        private static void Set(object target,string name,object value)=>target.GetType().GetField(name,Private).SetValue(target,value);
        private static void Invoke(object target,string name)=>target.GetType().GetMethod(name,Private).Invoke(target,null);
        private static void Check(bool value,string message)
        {if(!value)throw new InvalidOperationException(message);Passed.Add(message);}
        private static IEnumerator Guard(IEnumerator run)
        {
            while(true)
            {
                bool next;object value=null;
                try{next=run.MoveNext();if(next)value=run.Current;}
                catch(Exception ex){Debug.LogException(ex);Finish(false,ex.ToString());yield break;}
                if(!next)break;yield return value;
            }
            Finish(true,"");
        }
        private static void Finish(bool passed,string error)
        {
            // ★ 适配：只清理本轮创建且位于 save/<GUID> 内的测试文件，不触碰其他世界。
            if(!string.IsNullOrEmpty(_testSave))
            {
                string owned=Path.GetFullPath(_testSave);
                string root=Path.GetFullPath(GamePaths.Saves)+Path.DirectorySeparatorChar;
                if(owned.StartsWith(root,StringComparison.OrdinalIgnoreCase)&&Guid.TryParseExact(Path.GetFileName(owned),"N",out _))
                    foreach(string file in new[]{"world.json","world.json.bak"})
                    {string path=Path.Combine(owned,file);if(File.Exists(path))File.Delete(path);}
                if(Directory.Exists(owned)&&!Directory.EnumerateFileSystemEntries(owned).Any())Directory.Delete(owned);
            }
            File.WriteAllText(Path.Combine(Folder,"result.txt"),(passed?"PASS":"FAIL")+"\n"+error+"\n"+string.Join("\n",Passed));
            Debug.Log("[UIStyleReview] "+(passed?"PASS":"FAIL")+": "+Passed.Count+" checks");
            if(SessionState.GetBool(Key,false)){SessionState.SetBool(Key,false);EditorApplication.Exit(passed?0:1);}
        }
        private static IEnumerator Capture(string name)
        {
            yield return null;yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(Folder,name+".png"));
            yield return null;yield return null;
        }
        private static IEnumerator Run()
        {
            var menu=Object.FindFirstObjectByType<GameMainMenu>();
            var session=Object.FindFirstObjectByType<WorldSessionManager>();
            var root=ServiceLocator.Get<UIRoot>();
            Check(root.MainCanvas.GetComponent<GraphicRaycaster>()!=null,"主 Canvas 保留点击射线");
            Check(UIVisualTheme.Data.geneIcons.Length==14,"14 类基因图标映射完整");
            foreach(var entry in UIVisualTheme.Data.geneIcons)Check(UIIconRegistry.Get(entry.icon)!=null,"基因图标可加载："+entry.id);
            foreach(string icon in new[]{"planet","time_play","time_pause","ui_icon_prev","ui_icon_next","btn_param_temp","btn_param_hum","btn_param_terrain","btn_param_seed","layer_ore","layer_terrain","layer_climate","entity_template_default"})
                Check(UIIconRegistry.Get(icon)!=null,"导航/HUD 图标可加载："+icon);
            var bottom=root.MainCanvas.transform.Find("BottomPanel").GetComponent<RectTransform>();
            Check(bottom.anchorMin==Vector2.zero&&bottom.anchorMax==Vector2.right&&bottom.anchoredPosition==Vector2.zero&&Mathf.Approximately(bottom.sizeDelta.y,210),"底栏仍为原全宽底部 210 高矩形");
            var geometry=(bottom.anchorMin,bottom.anchorMax,bottom.pivot,bottom.anchoredPosition,bottom.sizeDelta);
            UIVisualTheme.Paint(bottom.GetComponent<Image>());
            Check(geometry==(bottom.anchorMin,bottom.anchorMax,bottom.pivot,bottom.anchoredPosition,bottom.sizeDelta),"主题应用不改变 RectTransform");
            var layers=bottom.Find("ContentArea/Page1/LayerSec");
            Check(layers.Cast<Transform>().Where(t=>t.GetComponent<LayoutElement>()?.ignoreLayout!=true).Select(t=>t.name).SequenceEqual(new[]{"layer_ore","layer_terrain","layer_climate"}),"补图标后原图层按钮保留排列顺序");
            yield return new WaitForSecondsRealtime(2);
            yield return Capture("01-main");
            foreach(var page in new[]{("settings","02-settings"),("create","03-create"),("multiplayer","04-multiplayer")})
            {
                if(page.Item1=="create")Invoke(menu,"NewWorldForm");else Set(menu,"_page",page.Item1);
                yield return Capture(page.Item2);
            }
            var tools=Object.FindFirstObjectByType<GeneTreeWorkbench>();tools.Open();Set(menu,"_page","tools");
            tools.GetType().GetMethod("Select",Private).Invoke(tools,new object[]{"gene_territory"});
            yield return Capture("05-gene-tree");
            Check(tools.Visible&&!tools.HasUnsavedChanges,"打开与绘制工具不改写基因配置");
            tools.Close();Set(menu,"_page","saves");
            var previews=new List<WorldSaveEntry>();
            foreach(string name in new[]{"2026-10-28_12-00-00_78031","群落实验","初始生态"})
                previews.Add(new WorldSaveEntry{name=name,data=new WorldSaveData{name=name,seed=78031,gameTime=4380}});
            Set(menu,"_saves",previews);yield return Capture("06-saves");
            var builder=Object.FindFirstObjectByType<PlanetTerrainMeshBuilder>();
            builder.subdivisions=32;Set(session,"_initialTerrain",JsonUtility.ToJson(builder));
            PlanetClimateMap.Instance.mapResolution=128;PlanetOreSystem.Instance.mapResolution=64;
            session.Create("UI 样式回归",78031,.5f,20,44);Set(menu,"_page","loading");
            bool captured=false;double started=EditorApplication.timeSinceStartup;
            while(session.Busy)
            {
                if(!captured&&session.Progress>=.66f){captured=true;yield return Capture("07-loading");}
                if(EditorApplication.timeSinceStartup-started>100)throw new TimeoutException("世界加载超时");
                yield return null;
            }
            Check(session.Current!=null&&!menu.Visible,"新建完成后仍进入游戏");
            _testSave=Path.GetDirectoryName(WorldSaveStore.FileFor(session.Current.id));
            var clock=ServiceLocator.Get<SimulationClock>();clock.Pause();
            var genome=new Genome();foreach(string id in new[]{"gene_growth","gene_reproduction","gene_nutrition","gene_locomotion","gene_territory"})genome.Add(GeneInstance.Create(id,50,0));
            DarwinEntity.Spawner(genome,builder.transform.position+Vector3.up*(builder.WaterRadius+3),"苍穹 · 行者");yield return null;
            var entity=EntityRegistry.All.Single();var inspector=Object.FindFirstObjectByType<GeneInspectorUI>();
            inspector.ShowEntityInfo(entity);EntityFavoritesManager.Instance.ToggleFavorite(entity);EntityFavoritesManager.Instance.TogglePanel();
            var panel=((GameObject)Get(inspector,"_panel")).GetComponent<RectTransform>();
            Check(panel.anchorMin==Vector2.up&&panel.anchorMax==Vector2.up&&panel.pivot==Vector2.up&&panel.anchoredPosition==inspector.infoCornerOffset&&panel.sizeDelta.x==420,"实体面板保持原左上位置与宽度");
            entity.GetComponent<DarwinPark.Entities.Stats.EntityStats>().Modify("health",-12,false);inspector.RefreshDisplayedInfo();
            var summary=panel.Find("ScrollView/Viewport/Content/BaseInfo/Stat_health");
            Check(summary.Find("Value").GetComponent<Text>().text=="88"&&Mathf.Abs(summary.Find("Track/Fill").GetComponent<RectTransform>().anchorMax.x-.88f)<.001f,"分栏数值和进度条同步刷新真实生命值与上限");
            var favorites=((GameObject)Get(EntityFavoritesManager.Instance,"_listPanel")).GetComponent<RectTransform>();
            Check(favorites.anchorMin==new Vector2(0,.5f)&&favorites.anchoredPosition==new Vector2(20,0)&&favorites.sizeDelta==new Vector2(200,400),"收藏栏保持原左侧中部位置与尺寸");
            yield return Capture("08-game");
            Invoke(inspector,"ShowAddGeneMenu");yield return null;
            var add=((GameObject)Get(inspector,"_addGenePanel")).GetComponent<RectTransform>();
            Check(add.anchorMin==Vector2.zero&&add.anchorMax==Vector2.zero&&add.anchoredPosition==inspector.addCornerOffset&&add.sizeDelta.x==350,"添加基因窗保持原左下位置与宽度");
            var content=(Transform)Get(inspector,"_addGeneListParent");
            Check(content.Cast<Transform>().Count(t=>t.gameObject.activeSelf)==GeneDatabase.All.Count(),"全部基因仍生成可点击条目");
            Check(content.GetComponentInParent<ScrollRect>().verticalScrollbar!=null,"添加基因窗保留滚动条");
            yield return Capture("09-add-genes");
            var addButton=content.Find("Btn_gene_population").GetComponent<Button>();
            Check(addButton.GetComponentsInChildren<Image>().Where(i=>i.name=="ThemeIcon").All(i=>!i.raycastTarget),"新增图标不拦截按钮点击");
            addButton.onClick.Invoke();
            Check(entity.Genes.Genome.Genes.Any(g=>g.DefinitionId=="gene_population"),"新皮肤按钮仍能实际添加基因");
            Check(!add.gameObject.activeSelf,"添加成功后关闭弹窗");
            var simulation=Object.FindFirstObjectByType<SimulationControlPanel>();
            EntityTemplateManager.SaveTemplate(entity);
            simulation.GetType().GetMethod("SwitchPage",Private).Invoke(simulation,new object[]{1});yield return null;
            var template=bottom.GetComponentsInChildren<EntitySpawnerUI>().First(s=>s.CustomGenome!=null);
            Check(template.GetComponentsInChildren<Image>().Any(i=>i.name=="ThemeIcon"&&i.sprite!=null),"实体模板显示独立图标并保留原生成组件");
            yield return Capture("10-spawn-templates");
            simulation.GetType().GetMethod("SwitchPage",Private).Invoke(simulation,new object[]{0});
            menu.ShowPause();yield return Capture("11-pause");
            Check(menu.Visible&&!clock.IsRunning&&GameInputGate.Blocked,"暂停继续接管游戏输入与时钟");
            Invoke(menu,"Resume");
            Check(!menu.Visible&&!GameInputGate.Blocked,"返回游戏恢复 HUD 与输入");
            menu.ShowPause();menu.ReturnToMainMenu();
            Check(session.Current==null&&EntityRegistry.All.Count==0,"保存并返回主界面链路保持正常");
            Check(File.Exists(Path.Combine(_testSave,"world.json")),"主题不影响存档落盘");
        }
    }
}
