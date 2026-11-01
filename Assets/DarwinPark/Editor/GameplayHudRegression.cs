using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DarwinPark.Core;
using DarwinPark.Entities;
using DarwinPark.Entities.Stats;
using DarwinPark.Genetics;
using DarwinPark.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace DarwinPark.EditorTools
{
    // ★ 新增：用真实Canvas缩放和实体表达验证HUD；不用固定坐标断言来重复旧布局错误。
    [InitializeOnLoad]
    public static class GameplayHudRegression
    {
        private const string Key="DarwinPark.GameplayHudReview";private static bool _started;
        private static readonly List<string> Passed=new List<string>();
        private static string Folder=>Path.GetFullPath("Artifacts/GameplayHudReview");
        static GameplayHudRegression(){EditorApplication.update+=()=>{if(!_started&&Application.isPlaying&&SessionState.GetBool(Key,false)&&LanSession.Instance!=null){_started=true;LanSession.Instance.StartCoroutine(Guard());}};}
        public static void RunBatch(){Application.runInBackground=true;SessionState.SetBool(Key,true);EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");EditorApplication.EnterPlaymode();}
        private static void Check(bool condition,string text){if(!condition)throw new InvalidOperationException(text);Passed.Add(text);Debug.Log("[GameplayHudReview] "+text);}
        private static IEnumerator Guard()
        {
            Directory.CreateDirectory(Folder);File.WriteAllText(Path.Combine(Folder,"result.txt"),"RUNNING");Passed.Clear();string error=null;var stack=new Stack<IEnumerator>();stack.Push(Run());
            while(stack.Count>0)
            {
                bool next=false;object current=null;try{next=stack.Peek().MoveNext();if(next)current=stack.Peek().Current;}catch(Exception ex){error=ex.ToString();Debug.LogException(ex);break;}
                if(!next){stack.Pop();continue;}if(current is IEnumerator nested)stack.Push(nested);else yield return current;
            }
            File.WriteAllText(Path.Combine(Folder,"result.txt"),(error==null?"PASS":"FAIL")+"\n"+error+"\n"+string.Join("\n",Passed));SessionState.SetBool(Key,false);Debug.Log("[GameplayHudReview] "+(error==null?"PASS":"FAIL"));EditorApplication.Exit(error==null?0:1);
        }
        private static DarwinEntity Spawn(bool movement,bool disabled,AgeState age)
        {
            var genome=new Genome();genome.Add(GeneInstance.Create("gene_growth",50,0));
            if(movement){var gene=GeneInstance.Create("gene_locomotion",50,0);gene.Enabled=!disabled;genome.Add(gene);if(!disabled)genome.Add(GeneInstance.Create("gene_locomotion",50,0));}
            var before=EntityRegistry.All.ToArray();DarwinEntity.Spawner(genome,new Vector3(0,350,0),"HUD统计验证");var entity=EntityRegistry.All.Single(e=>!before.Contains(e));
            entity.Genes.mutationChance=0;var life=LifeCycleSettings.Load();entity.GetComponent<EntityLifeCycle>().RestoreProgress(age==AgeState.Adult?life.adultProgress:age==AgeState.Elder?life.elderProgress:0);entity.Genes.RestoreStageState(age,7,3);entity.Genes.Recalculate();entity.Genes.enabled=false;
            entity.GetComponent<Rigidbody>().isKinematic=true;entity.GetComponent<PlanetGravity>().enabled=false;
            entity.GetComponent<DarwinPark.Entities.Vitals.EntityVitals>().enabled=false;entity.GetComponent<DarwinPark.Entities.Vitals.EntityEnvironmentHazard>().enabled=false;return entity;
        }
        private static Rect ScreenRect(RectTransform transform)
        {var corners=new Vector3[4];transform.GetWorldCorners(corners);var a=RectTransformUtility.WorldToScreenPoint(null,corners[0]);var b=RectTransformUtility.WorldToScreenPoint(null,corners[2]);return Rect.MinMaxRect(a.x,a.y,b.x,b.y);}
        private static IEnumerator Run()
        {
            Application.runInBackground=true;yield return null;yield return null;yield return null;
            var root=ServiceLocator.Get<UIRoot>();var control=Object.FindFirstObjectByType<SimulationControlPanel>();var stats=Object.FindFirstObjectByType<MapEntityStatisticsUI>(FindObjectsInactive.Include);var fps=FrameRateDisplay.Instance;var clock=ServiceLocator.Get<SimulationClock>();
            Check(stats!=null&&fps!=null&&RtsSelectionInput.Instance!=null,"新增统计、FPS和RTS组件真实挂载");
            WorldRuntimeGate.Ready=true;typeof(GameMainMenu).GetMethod("Resume",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(Object.FindFirstObjectByType<GameMainMenu>(),null);clock.Pause();
            typeof(SimulationControlPanel).GetMethod("SwitchPage",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(control,new object[]{0});yield return null;yield return null;
            foreach(var entity in EntityRegistry.All.ToArray())Object.DestroyImmediate(entity.gameObject);
            var adult=Spawn(true,false,AgeState.Adult);var young=Spawn(true,true,AgeState.Infant);var elder=Spawn(false,false,AgeState.Elder);var inactive=Spawn(true,false,AgeState.Adult);inactive.gameObject.SetActive(false);
            yield return null;stats.RefreshNow();Check(stats.Total==3&&stats.Infants==1&&stats.Adults==1&&stats.Elders==1,"统计全地图存活个体和年龄阶段，排除停用实体");
            Check(stats.SelectGene("gene_locomotion")&&stats.Carriers==2&&stats.Expressed==1,"筛选基因按实体去重，携带数量与当前生效数量分开");
            var inspector=Object.FindFirstObjectByType<GeneInspectorUI>();inspector.ShowEntityInfo(elder);stats.RefreshNow();Check(stats.Total==3&&stats.Carriers==2,"全地图统计不随当前查看实体改变");
            WorldAuthority.CanEdit=false;stats.SelectGene("gene_growth");Check(stats.Carriers==3&&stats.Expressed==3&&stats.transform.Find("GeneFilter").GetComponent<Dropdown>().interactable,"普通成员也能筛选只读地图数据");WorldAuthority.CanEdit=true;
            var page=typeof(SimulationControlPanel).GetMethod("SwitchPage",BindingFlags.NonPublic|BindingFlags.Instance);page.Invoke(control,new object[]{0});yield return null;Check(stats.gameObject.activeInHierarchy&&stats.Total==3,"第一页显示地图统计");page.Invoke(control,new object[]{1});Check(!stats.gameObject.activeInHierarchy,"第二页隐藏地图统计");page.Invoke(control,new object[]{0});
            elder.GetComponent<EntityStats>().Modify("health",-100,false);stats.RefreshNow();Check(stats.Total==2&&stats.Elders==0,"生命归零的实体不计入存活数量");
            young.gameObject.SetActive(false);stats.RefreshNow();Check(stats.Total==1,"成员移除后实时更新数量");young.gameObject.SetActive(true);
            stats.SelectGene(null);Check(stats.FilterGeneId==null&&stats.Total==2,"全部实体筛选可恢复");
            var newborn=Spawn(false,false,AgeState.Infant);yield return new WaitForSecondsRealtime(.6f);Check(stats.Total==3,"新实体自动进入统计，无需手动刷新");Object.DestroyImmediate(newborn.gameObject);
            yield return new WaitForSecondsRealtime(.6f);Check(stats.Total==2,"销毁后自动减少数量");
            stats.SelectGene("gene_locomotion");foreach(var gene in adult.Genes.Genome.Genes.Where(g=>g.DefinitionId=="gene_locomotion"))gene.Enabled=false;adult.Genes.Recalculate();
            yield return new WaitForSecondsRealtime(.6f);Check(stats.Carriers==2&&stats.Expressed==0,"基因禁用自动改变生效数，携带数保持正确");
            foreach(var gene in adult.Genes.Genome.Genes.Where(g=>g.DefinitionId=="gene_locomotion"))gene.Enabled=true;adult.Genes.Recalculate();stats.RefreshNow();
            var scaler=root.MainCanvas.GetComponent<CanvasScaler>();var mode=scaler.uiScaleMode;float original=scaler.scaleFactor;scaler.uiScaleMode=CanvasScaler.ScaleMode.ConstantPixelSize;LanSession.Instance.SetRtsMode(true);
            foreach(float scale in new[]{.7f,1f,1.4f})
            {
                scaler.scaleFactor=scale;yield return null;yield return null;Canvas.ForceUpdateCanvases();
                Check(control.TryGetScreenRect(out var bottom),"底栏实际边界可读取 scale="+scale);var bar=RtsSelectionInput.CommandBarRect;
                Check(bar.yMax<=Screen.height-bottom.yMax-RtsSelectionInput.Instance.commandBarGap+.5f,"RTS完整位于底栏上方 scale="+scale);
                Check(RtsSelectionInput.IsOverCommandBar(new Vector2(bar.center.x,Screen.height-bar.center.y))&&!RtsSelectionInput.IsOverCommandBar(bottom.center),"RTS命中范围跟随位置且不覆盖权能栏 scale="+scale);
                var section=root.MainCanvas.transform.Find("BottomPanel/ContentArea/Page1/ParamSec") as RectTransform;
                var card=ScreenRect((RectTransform)stats.transform);var parameters=ScreenRect(section);
                Check(card.xMin>parameters.xMax&&card.yMin>=bottom.yMin&&card.yMax<=bottom.yMax,"地图统计位于指定右侧并留在底栏内 scale="+scale);
                Check(card.width<=stats.preferredWidth*scale+.5f,"统计栏宽度受260逻辑单位限制，不拉伸填满屏幕 scale="+scale);
                Check(card.height<=stats.preferredHeight*scale+.5f,"统计卡片高度受164逻辑单位限制 scale="+scale);
            }
            scaler.uiScaleMode=mode;scaler.scaleFactor=original;yield return null;yield return null;Canvas.ForceUpdateCanvases();
            inspector.ShowEntityInfo(adult);yield return null;var genePanel=root.MainCanvas.transform.Find("GenePanel") as RectTransform;
            Check(Screen.height-ScreenRect(genePanel).yMax>=fps.DisplayRect.yMax,"实体信息面板为左上FPS保留空间");
            int samples=fps.SampleCount;float time=clock.GameTime;yield return new WaitForSecondsRealtime(.7f);Check(fps.SampleCount>samples&&fps.CurrentFps>0&&clock.GameTime==time,"暂停模拟仍刷新真实FPS");
            clock.SetTimeScale(16);clock.Play();samples=fps.SampleCount;yield return new WaitForSecondsRealtime(.7f);Check(fps.SampleCount>samples&&fps.CurrentFps>0,"16倍模拟仍按真实帧采样");clock.Pause();
            // ★ 新增：真正制造时钟积压，核验显示实际倍率/待算状态并可恢复，避免只验证无负载的FPS标签。
            clock.RestoreState(clock.GameTime,16);var budget=(SimulationStepBudget)typeof(SimulationClock).GetField("_budget",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(clock);budget.Consume(10,16,.5f);clock.Play();
            yield return new WaitForSecondsRealtime(.7f);Check(clock.PendingGameTime>0&&fps.DisplayRect.height>=fps.size.y+fps.simulationLineHeight,"过载时显示实际模拟倍率和待算时间");
            yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(Folder,"simulation-budget.png"));yield return null;
            clock.Pause();clock.RestoreState(clock.GameTime,1);yield return new WaitForSecondsRealtime(.6f);Check(Mathf.Abs(fps.DisplayRect.height-fps.size.y)<.1f,"积压恢复后FPS显示回到紧凑高度");
            stats.SelectGene("gene_locomotion");yield return null;stats.RefreshNow();var dropdown=stats.transform.Find("GeneFilter").GetComponent<Dropdown>();dropdown.Show();yield return new WaitForSecondsRealtime(.25f);
            Check(dropdown.GetComponentsInChildren<Toggle>().Length>=dropdown.options.Count,"筛选下拉列表可实际展开全部基因");dropdown.Hide();
            var filterRect=ScreenRect(dropdown.GetComponent<RectTransform>());Check(EntityPlacementUtility.IsPointerOverUI(filterRect.center),"点击基因筛选属于UI，不触发RTS地面指令");
            inspector.HideEntityInfo();yield return new WaitForSecondsRealtime(.25f);Canvas.ForceUpdateCanvases();yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(Folder,"rts-statistics-fps.png"));yield return null;yield return null;
            WorldRuntimeGate.Ready=false;stats.RefreshNow();Check(stats.Total==0&&stats.Carriers==0,"离开世界清空地图统计");
        }
    }
}
