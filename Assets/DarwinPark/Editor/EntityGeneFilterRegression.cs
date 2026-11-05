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
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace DarwinPark.EditorTools
{
    // ★ 新增：实际界面验证组合条件、分页、定位及只读权限；不在用户世界制造实体或修改基因。
    [InitializeOnLoad] public static class EntityGeneFilterRegression
    {
        private const string Key="DarwinPark.EntityGeneFilterReview";
        private static bool _started;private static readonly List<string> Passed=new List<string>();
        private static readonly BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
        private static string Folder=>Path.GetFullPath("Artifacts/EntityGeneFilterReview");
        static EntityGeneFilterRegression(){EditorApplication.update+=()=>{if(!_started&&Application.isPlaying&&SessionState.GetBool(Key,false)&&LanSession.Instance!=null){_started=true;LanSession.Instance.StartCoroutine(Guard());}};}
        public static void RunBatch(){Application.runInBackground=true;SessionState.SetBool(Key,true);EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");EditorApplication.EnterPlaymode();}
        private static void Check(bool condition,string text){if(!condition)throw new InvalidOperationException(text);Passed.Add(text);}
        private static IEnumerator Guard()
        {
            Directory.CreateDirectory(Folder);File.WriteAllText(Path.Combine(Folder,"result.txt"),"RUNNING");Passed.Clear();string error=null;var stack=new Stack<IEnumerator>();stack.Push(Run());
            while(stack.Count>0){bool next=false;object value=null;try{next=stack.Peek().MoveNext();if(next)value=stack.Peek().Current;}catch(Exception ex){error=ex.ToString();Debug.LogException(ex);break;}if(!next){stack.Pop();continue;}if(value is IEnumerator nested)stack.Push(nested);else yield return value;}
            File.WriteAllText(Path.Combine(Folder,"result.txt"),(error==null?"PASS":"FAIL")+"\n"+error+"\n"+string.Join("\n",Passed));SessionState.SetBool(Key,false);Debug.Log("[EntityGeneFilterReview] "+(error==null?"PASS":"FAIL")+" "+Passed.Count);EditorApplication.Exit(error==null?0:1);
        }
        private static DarwinEntity Spawn(string name,bool movement,bool nutrition,bool disabled=false)
        {
            var genome=new Genome();genome.Add(GeneInstance.Create("gene_growth",50,0));
            if(movement){var gene=GeneInstance.Create("gene_locomotion",50,0);gene.Enabled=!disabled;genome.Add(gene);var copy=GeneInstance.Create("gene_locomotion",50,0);copy.Enabled=!disabled;genome.Add(copy);}
            if(nutrition)genome.Add(GeneInstance.Create("gene_nutrition",50,0));
            var before=EntityRegistry.All.ToArray();DarwinEntity.Spawner(genome,new Vector3(0,350,0),name);var entity=EntityRegistry.All.Single(e=>!before.Contains(e));
            entity.Genes.mutationChance=0;entity.Genes.Recalculate();entity.Genes.enabled=false;entity.GetComponent<Rigidbody>().isKinematic=true;entity.GetComponent<PlanetGravity>().enabled=false;
            entity.GetComponent<DarwinPark.Entities.Vitals.EntityVitals>().enabled=false;entity.GetComponent<DarwinPark.Entities.Vitals.EntityEnvironmentHazard>().enabled=false;return entity;
        }
        private static IEnumerator Capture(string name){yield return null;yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(Folder,name+".png"));yield return null;yield return null;}
        // ★ 新增：通过真实GraphicRaycaster和EventSystem派发鼠标事件，验证点击遮罩和列表项目而非只手调Hide。
        private static Vector2 Center(RectTransform rect){var corners=new Vector3[4];rect.GetWorldCorners(corners);return RectTransformUtility.WorldToScreenPoint(null,(corners[0]+corners[2])*.5f);}
        private static GameObject Click(Vector2 point)
        {
            Canvas.ForceUpdateCanvases();var data=new PointerEventData(EventSystem.current){position=point,button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(data,hits);if(hits.Count==0)throw new InvalidOperationException("点击位置没有UI命中");
            data.pointerCurrentRaycast=hits[0];var target=hits[0].gameObject;ExecuteEvents.ExecuteHierarchy(target,data,ExecuteEvents.pointerDownHandler);ExecuteEvents.ExecuteHierarchy(target,data,ExecuteEvents.pointerUpHandler);ExecuteEvents.ExecuteHierarchy(target,data,ExecuteEvents.pointerClickHandler);return target;
        }
        private static IEnumerator Run()
        {
            yield return null;yield return null;yield return null;yield return null;
            var root=ServiceLocator.Get<UIRoot>();var stats=Object.FindFirstObjectByType<MapEntityStatisticsUI>(FindObjectsInactive.Include);
            var probe=DefaultControls.CreateDropdown(new DefaultControls.Resources());probe.transform.SetParent(root.MainCanvas.transform,false);RuntimeDropdownSkin.Apply(probe.GetComponent<Dropdown>());
            string probeError="none";try{probe.GetComponent<Dropdown>().Show();}catch(Exception ex){probeError=ex.ToString();}File.WriteAllText(Path.Combine(Folder,"early-dropdown.txt"),probeError+"\ntemplate="+probe.GetComponent<Dropdown>().template.gameObject.activeSelf+"\nlist="+(probe.transform.Find("Dropdown List")!=null));
            Check(probeError=="none"&&!probe.GetComponent<Dropdown>().template.gameObject.activeSelf&&probe.transform.Find("Dropdown List")==null&&probe.GetComponent<CanvasGroup>().blocksRaycasts,"创建当帧展开不会抛淡入异常或留下Option A模板，保持阻止点击穿透");probe.GetComponent<Dropdown>().interactable=false;yield return null;yield return null;Check(!probe.GetComponent<Dropdown>().interactable&&!probe.GetComponent<Dropdown>().IsInteractable(),"初始化保护不覆盖业务禁用与只读权限");Object.DestroyImmediate(probe);
            var filter=stats.GetComponent<MapEntityGeneFilterUI>();Check(filter!=null&&filter.Card!=null,"组合筛选组件在真实统计卡启动时挂载");
            var clock=ServiceLocator.Get<SimulationClock>();WorldRuntimeGate.Ready=true;typeof(GameMainMenu).GetMethod("Resume",Private).Invoke(Object.FindFirstObjectByType<GameMainMenu>(),null);clock.Pause();
            var control=Object.FindFirstObjectByType<SimulationControlPanel>();var switchPage=typeof(SimulationControlPanel).GetMethod("SwitchPage",Private);switchPage.Invoke(control,new object[]{0});
            foreach(var entity in EntityRegistry.All.ToArray())Object.DestroyImmediate(entity.gameObject);
            var a=Spawn("包含行动",true,false);var b=Spawn("只有营养",false,true);var c=Spawn("行动被禁用",true,false,true);var d=Spawn("行动与营养",true,true);var e=Spawn("仅生长",false,false);
            var inactive=Spawn("停用实体",true,false);inactive.gameObject.SetActive(false);yield return null;
            filter.ResetFilter();Check(filter.Total==5&&filter.Matches.Count==5,"无条件时显示所有存活实体，排除停用个体");
            Check(filter.SetGene("gene_locomotion",true)&&filter.Matches.Count==3,"包含行动基因，复制基因按实体去重");
            Check(filter.Matches.Contains(c),"禁用或不生效仍算携带基因");filter.SetGene("gene_growth",true);Check(filter.Matches.Count==3,"多个必须包含条件按全部满足组合");
            filter.SetGene("gene_nutrition",false);Check(filter.Matches.Count==2&&filter.Matches.Contains(a)&&filter.Matches.Contains(c),"包含与不包含条件同时生效");
            filter.SetGene("gene_locomotion",false);Check(!filter.Criteria.Included.Contains("gene_locomotion")&&filter.Matches.Count==1&&filter.Matches[0]==e,"同一基因切换规则互斥，多个排除任一命中即排除");
            filter.ResetFilter();var include=filter.Card.Find("IncludeGenes").GetComponent<Dropdown>();int index=include.options.FindIndex(o=>o.text.Contains("行动基因"));Check(index>=2,"实际下拉菜单包含可选行动基因");
            include.Show();yield return new WaitForSecondsRealtime(.25f);Check(include.transform.Find("Dropdown List")!=null&&!include.template.gameObject.activeSelf,"初始化后正常展开且隐藏原始模板");yield return Capture("dropdown-open");
            var outside=Click(new Vector2(Screen.width*.2f,Screen.height*.7f));Check(outside.name=="Blocker","点击列表外部实际命中关闭遮罩而非底层世界");yield return new WaitForSecondsRealtime(.25f);Check(include.transform.Find("Dropdown List")==null&&!include.template.gameObject.activeSelf,"点击外部彻底收起列表和模板");
            include.Show();yield return new WaitForSecondsRealtime(.25f);Click(Center(include.GetComponent<RectTransform>()));yield return new WaitForSecondsRealtime(.25f);Check(include.transform.Find("Dropdown List")==null,"再次点击展开按钮可收起菜单");
            include.Show();yield return new WaitForSecondsRealtime(.25f);var picked=include.transform.Find("Dropdown List").GetComponentsInChildren<Toggle>().First(t=>t.GetComponentInChildren<Text>().text==include.options[index].text);Click(Center(picked.GetComponent<RectTransform>()));yield return new WaitForSecondsRealtime(.25f);Check(filter.Criteria.Included.Contains("gene_locomotion")&&include.transform.Find("Dropdown List")==null,"点击真实列表项目应用条件并关闭菜单");filter.ResetFilter();
            include.Show();yield return new WaitForSecondsRealtime(.25f);var menu=Object.FindFirstObjectByType<GameMainMenu>();bool menuVisible=menu.Visible;Check(RuntimeDropdownDismissal.CloseOpenPopups()&&menu.Visible==menuVisible&&!include.GetComponent<RuntimeDropdownDismissal>().IsOpen,"Esc菜单路由优先收起下拉且不打开暂停菜单");yield return new WaitForSecondsRealtime(.25f);
            include.Show();yield return new WaitForSecondsRealtime(.25f);ExecuteEvents.Execute(include.gameObject,new BaseEventData(EventSystem.current),ExecuteEvents.cancelHandler);Check(!include.GetComponent<RuntimeDropdownDismissal>().IsOpen&&GameInputGate.Blocked,"取消事件关闭菜单并消费本帧Esc输入");yield return new WaitForSecondsRealtime(.25f);
            include.template.gameObject.SetActive(true);include.GetComponent<RuntimeDropdownDismissal>().ClosePopup();Check(!include.template.gameObject.activeSelf,"异常残留的原始Option A模板也能关闭");
            include.Show();yield return new WaitForSecondsRealtime(.25f);switchPage.Invoke(control,new object[]{1});yield return null;Check(include.transform.Find("Dropdown List")==null&&!include.template.gameObject.activeSelf&&root.MainCanvas.transform.Find("Blocker")==null,"切换页面清理列表模板与遮罩");switchPage.Invoke(control,new object[]{0});yield return null;yield return null;
            include.Show();yield return new WaitForSecondsRealtime(.25f);root.MainCanvas.enabled=false;yield return null;yield return null;Check(!include.GetComponent<RuntimeDropdownDismissal>().IsOpen,"隐藏HUD时不残留独立排序的弹出列表");root.MainCanvas.enabled=true;yield return new WaitForSecondsRealtime(.25f);yield return Capture("dropdown-closed");
            include.value=index;Check(filter.Matches.Count==3,"从实际下拉菜单选择包含条件");include.value=index;Check(filter.Matches.Count==5,"再次选择同一基因可取消条件");include.value=index;include.value=1;Check(filter.Criteria.Included.Count==0&&filter.Matches.Count==5,"清空本组独立于占位项，不会因value为0而失效");
            filter.SetGene("gene_locomotion",true);filter.SetGene("gene_nutrition",false);filter.OpenResults();yield return null;
            Check(filter.Results.Visible&&filter.Results.VisibleRowCount==2,"生成筛选后的真实实体列表与数量");
            filter.Results.transform.Find("EntityRow0").GetComponent<Button>().onClick.Invoke();Check(filter.LocatedEntity!=null&&(bool)typeof(OrbitCamera).GetField("_isFocusing",Private).GetValue(Camera.main.GetComponent<OrbitCamera>()),"点击列表条目实际启动相机定位");
            filter.Results.transform.Find("EntityRow0").GetComponent<RightClickHandler>().OnPointerClick(new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Right});
            Check(Object.FindFirstObjectByType<GeneInspectorUI>().CurrentEntity==filter.LocatedEntity,"右键列表实体可查看原属性面板");Object.FindFirstObjectByType<GeneInspectorUI>().HideEntityInfo();
            var canvasScaler=root.MainCanvas.GetComponent<CanvasScaler>();var originalMode=canvasScaler.uiScaleMode;float originalScale=canvasScaler.scaleFactor;canvasScaler.uiScaleMode=CanvasScaler.ScaleMode.ConstantPixelSize;canvasScaler.scaleFactor=.7f;
            yield return null;yield return null;stats.RefreshNow();Canvas.ForceUpdateCanvases();Check(((RectTransform)filter.Results.transform).rect.height<filter.resultWindowSize.y,"少量结果时列表自动缩短，避免过多留白");yield return Capture("combined-filter-list");
            var corners=new Vector3[4];include.GetComponent<RectTransform>().GetWorldCorners(corners);Check(EntityPlacementUtility.IsPointerOverUI(RectTransformUtility.WorldToScreenPoint(null,(corners[0]+corners[2])*.5f)),"筛选下拉区域阻止RTS或投放穿透");
            float oldMinimum=filter.minimumWidth;filter.minimumWidth=100000;yield return null;Check(!filter.WideLayout&&!filter.Card.gameObject.activeSelf,"宽度不足时收起独立卡，不挤压原统计");
            stats.transform.Find("OpenEntityFilter").GetComponent<Button>().onClick.Invoke();yield return null;Check(filter.Card.gameObject.activeSelf,"窄屏通过筛选按钮展开条件");yield return Capture("narrow-filter");
            filter.OpenResults();yield return null;Check(filter.Results.Visible&&!filter.Card.gameObject.activeSelf,"窄屏切换为列表时收起条件卡");
            filter.minimumWidth=oldMinimum;canvasScaler.uiScaleMode=originalMode;canvasScaler.scaleFactor=originalScale;yield return null;
            WorldAuthority.CanEdit=false;filter.ResetFilter();filter.SetGene("gene_locomotion",true);filter.OpenResults();Check(filter.Matches.Count==3&&filter.Results.LocateRow(0),"普通成员也能只读筛选并定位");WorldAuthority.CanEdit=true;
            filter.SetGene("gene_nutrition",false);c.Genes.Genome.Genes.RemoveAll(g=>g.DefinitionId=="gene_locomotion");c.Genes.Recalculate();yield return new WaitForSecondsRealtime(.65f);Check(filter.Matches.Count==1&&filter.Matches[0]==a,"实际基因移除后匹配数量与列表自动刷新");
            a.GetComponent<EntityStats>().Modify("health",-100000);Check(!filter.Locate(a),"列表中刚死亡的实体不能被继续定位");filter.RefreshNow();Check(filter.Matches.Count==0&&filter.Results.VisibleRowCount==0,"死亡后列表安全变为空结果");
            filter.ResetFilter();for(int i=0;i<27;i++)Spawn("分页实体"+i,false,false);filter.RefreshNow();filter.OpenResults();Check(filter.Results.PageCount>1&&filter.Results.GetComponentsInChildren<Button>(true).Count(button=>button.name.StartsWith("EntityRow"))==filter.resultsPerPage,"大量匹配复用固定行，不按实体总数创建UI");
            filter.Results.ChangePage(1);Check(filter.Results.Page==1&&filter.Results.LocateRow(0),"下一页条目仍正确定位相应实体");
            foreach(var entity in EntityRegistry.All.ToArray())if(entity!=b&&entity!=e)Object.DestroyImmediate(entity.gameObject);filter.RefreshNow();Check(filter.Results.Page==0&&filter.Matches.Count==2,"实体减少时页码回退，不越界访问旧列表");
            switchPage.Invoke(control,new object[]{1});yield return null;Check(!filter.Results.Visible&&!filter.Card.gameObject.activeInHierarchy,"切到第二页隐藏筛选和列表");switchPage.Invoke(control,new object[]{0});yield return null;
            WorldRuntimeGate.Ready=false;filter.RefreshNow();Check(filter.Matches.Count==0&&!filter.Results.Visible,"离开世界清空匹配结果并关闭列表");
        }
    }
}
