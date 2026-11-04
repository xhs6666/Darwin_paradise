using System.Collections.Generic;
using System.Linq;
using DarwinPark.Core;
using DarwinPark.Entities;
using DarwinPark.Entities.Ecology;
using DarwinPark.Genetics;
using UnityEngine;
using UnityEngine.UI;

namespace DarwinPark.UI
{
    // ★ 新增：独立筛选卡放在统计右侧，窄屏由统计标题中的“筛选”按钮展开，不继续拉长原统计面板。
    [DefaultExecutionOrder(50)] public sealed class MapEntityGeneFilterUI:MonoBehaviour
    {
        public float preferredWidth=260,minimumWidth=230,cardHeight=164,gap=10,refreshInterval=.5f;
        public Vector2 resultWindowSize=new Vector2(320,372);
        public float minimumResultHeight=150;
        public int resultsPerPage=10;
        public float resultRowHeight=26;
        public EntityGeneFilter Criteria {get;}=new EntityGeneFilter();
        public IReadOnlyList<DarwinEntity> Matches=>_matches;
        public int Total {get;private set;}
        public DarwinEntity LocatedEntity {get;private set;}
        public bool WideLayout {get;private set;}
        public RectTransform Card=>_card;
        public EntityFilterResultsUI Results=>_results;
        private MapEntityStatisticsUI _stats;private RectTransform _card;
        private Dropdown _include,_exclude;private Text _count,_hint;private Button _show;
        private EntityFilterResultsUI _results;private string _catalog;private float _next;
        private bool _floating;
        private readonly List<GeneDefinition> _genes=new List<GeneDefinition>();
        private readonly List<DarwinEntity> _matches=new List<DarwinEntity>();
        public static void Attach(MapEntityStatisticsUI stats){if(stats.GetComponent<MapEntityGeneFilterUI>()==null)stats.gameObject.AddComponent<MapEntityGeneFilterUI>();}
        private void Start()
        {
            _stats=GetComponent<MapEntityStatisticsUI>();
            var launch=UIFactory.NewButton("OpenEntityFilter",transform,"筛选",ToggleCriteria,new Vector2(46,22));
            var buttonRect=launch.GetComponent<RectTransform>();buttonRect.anchorMin=buttonRect.anchorMax=Vector2.one;buttonRect.pivot=Vector2.one;buttonRect.anchoredPosition=new Vector2(-39,-3);launch.GetComponentInChildren<Text>().fontSize=12;
            _card=UIFactory.NewRect("EntityGeneFilter",transform.parent,typeof(Image)).GetComponent<RectTransform>();UIVisualTheme.Paint(_card.GetComponent<Image>());
            Label("Title","实体筛选",4,18,13).color=UIVisualTheme.Muted;
            _include=Dropdown("IncludeGenes",27,true);_exclude=Dropdown("ExcludeGenes",58,false);
            _count=Label("MatchCount","匹配 0",88,18,14);_count.color=UIVisualTheme.Accent;
            _hint=Label("RuleHint","包含全部 · 排除任一 · 按携带判断",108,16,11);
            _show=UIFactory.NewButton("ShowMatches",_card,"查看实体列表",OpenResults,new Vector2(160,26));Place(_show.GetComponent<RectTransform>(),129,26,0,.64f);
            var reset=UIFactory.NewButton("ResetFilter",_card,"重置",ResetFilter,new Vector2(80,26));Place(reset.GetComponent<RectTransform>(),129,26,.65f,1);
            foreach(var b in new[]{_show,reset})b.GetComponentInChildren<Text>().fontSize=12;
            RefreshNow();
        }
        private Text Label(string name,string text,float y,float height,int size)
        {var label=UIFactory.NewText(name,_card,text,size);Place(label.rectTransform,y,height);return label;}
        private static void Place(RectTransform rect,float y,float height,float left=0,float right=1)
        {rect.anchorMin=new Vector2(left,1);rect.anchorMax=new Vector2(right,1);rect.pivot=new Vector2(.5f,1);rect.offsetMin=new Vector2(10,-y-height);rect.offsetMax=new Vector2(-10,-y);}
        private Dropdown Dropdown(string name,float y,bool include)
        {
            var go=DefaultControls.CreateDropdown(new DefaultControls.Resources());go.name=name;go.transform.SetParent(_card,false);Place(go.GetComponent<RectTransform>(),y,26);
            var dropdown=go.GetComponent<Dropdown>();UIVisualTheme.Paint(go.GetComponent<Image>(),"field");UIVisualTheme.Paint(dropdown.template.GetComponent<Image>());
            foreach(var text in go.GetComponentsInChildren<Text>(true)){text.font=UIFactory.DefaultFont;text.fontSize=12;text.color=UIVisualTheme.Text;}
            var arrow=go.transform.Find("Arrow").GetComponent<Image>();arrow.sprite=UIIconRegistry.Get("ui_icon_next");arrow.color=UIVisualTheme.Accent;arrow.preserveAspect=true;arrow.rectTransform.sizeDelta=new Vector2(14,14);arrow.rectTransform.localRotation=Quaternion.Euler(0,0,-90);
            var toggle=dropdown.template.GetComponentInChildren<Toggle>(true);UIVisualTheme.Paint(toggle.targetGraphic as Image,"hover");if(toggle.graphic!=null)toggle.graphic.color=UIVisualTheme.Accent;
            // ★ 适配：处理默认模板的选中图与滚动条，不能只给外框换肤。
            RuntimeDropdownSkin.Apply(dropdown);
            // ★ 修复（防Bug回归）：占位项与“清空本组”分开，否则value一直是0时选清空不会触发事件。
            dropdown.onValueChanged.AddListener(index=>{if(index==0)return;if(index==1)Criteria.Clear(include);else if(index-2<_genes.Count){var id=_genes[index-2].Id;Criteria.Set(id,include,!Criteria.Contains(id,include));}dropdown.SetValueWithoutNotify(0);UpdateOptions();RefreshNow();});
            return dropdown;
        }
        public bool SetGene(string id,bool include,bool enabled=true)
        {
            if(!_genes.Any(g=>g.Id==id))return false;Criteria.Set(id,include,enabled);UpdateOptions();RefreshNow();return true;
        }
        public void ResetFilter(){Criteria.Clear();UpdateOptions();RefreshNow();}
        private void UpdateOptions()
        {
            if(_include==null)return;
            foreach(bool include in new[]{true,false})
            {
                var selected=_genes.Where(g=>Criteria.Contains(g.Id,include)).ToList();
                string caption=(include?"包含：":"不包含：")+(selected.Count==0?"不限":selected[0].DisplayName+(selected.Count>1?" 等"+selected.Count+"项":""));
                // ★ 修复（防Bug回归）：Dropdown.Start会刷新标题，占位项也必须带规则摘要，初次打开才能区分包含/不包含。
                var dropdown=include?_include:_exclude;var labels=new List<string>{caption,include?"清空包含条件（不限）":"清空排除条件（不限）"};
                labels.AddRange(_genes.Select(g=>(Criteria.Contains(g.Id,include)?"[已选] ":"[ ] ")+g.DisplayName));
                dropdown.ClearOptions();dropdown.AddOptions(labels);dropdown.SetValueWithoutNotify(0);
                dropdown.captionText.text=caption;
            }
        }
        public void RefreshNow()
        {
            if(_include==null)return;_next=Time.unscaledTime+Mathf.Max(.1f,refreshInterval);
            var catalog=GeneDatabase.All.ToArray();string signature=string.Join("|",catalog.Select(g=>g.Id+":"+g.DisplayName));
            if(signature!=_catalog)
            {
                _include.Hide();_exclude.Hide();_catalog=signature;_genes.Clear();_genes.AddRange(catalog.OrderBy(g=>g.Tier).ThenBy(g=>g.Id));
                Criteria.KeepKnown(new HashSet<string>(_genes.Select(g=>g.Id)));UpdateOptions();
            }
            _matches.Clear();Total=0;
            if(WorldRuntimeGate.Ready)foreach(var entity in EntityRegistry.All)
            {if(!EcologyQueries.Alive(entity)||entity.Genes==null)continue;Total++;if(Criteria.Matches(entity))_matches.Add(entity);}
            _matches.Sort((a,b)=>a.GetInstanceID().CompareTo(b.GetInstanceID()));
            _count.text="匹配 "+_matches.Count+" / "+Total+" 个实体";_show.interactable=WorldRuntimeGate.Ready;
            if(!WorldRuntimeGate.Ready){LocatedEntity=null;_results?.Hide();_floating=false;}
            _results?.Refresh();
        }
        private void LateUpdate()
        {
            if(_card==null)return;
            var parent=(RectTransform)_card.parent;var stats=(RectTransform)_stats.transform;
            float x=stats.offsetMax.x+gap,available=parent.rect.width-x-12;
            WideLayout=available>=minimumWidth;float width=WideLayout?Mathf.Min(preferredWidth,available):Mathf.Min(preferredWidth,parent.rect.width-24);
            _card.gameObject.SetActive(WideLayout||_floating);
            var anchor=new Vector2(0,1);if(_card.anchorMin!=anchor||_card.anchorMax!=anchor||_card.pivot!=anchor){_card.anchorMin=_card.anchorMax=anchor;_card.pivot=anchor;}
            var position=WideLayout?new Vector2(x,0):new Vector2(Mathf.Clamp(stats.offsetMin.x,12,Mathf.Max(12,parent.rect.width-width-12)),cardHeight+gap);
            var size=new Vector2(width,cardHeight);
            if(_card.anchoredPosition!=position)_card.anchoredPosition=position;if(_card.sizeDelta!=size)_card.sizeDelta=size;
            if(Time.unscaledTime>=_next)RefreshNow();
        }
        public void ToggleCriteria(){_results?.Hide();_floating=!_floating;if(_card!=null){_card.gameObject.SetActive(true);_card.SetAsLastSibling();}RefreshNow();}
        public void OpenResults()
        {
            RefreshNow();if(!WorldRuntimeGate.Ready)return;
            if(_results==null)_results=EntityFilterResultsUI.Create(this);_results?.Show();if(!WideLayout)_floating=false;
        }
        public bool Locate(DarwinEntity entity,bool inspect=false)
        {
            if(!WorldRuntimeGate.Ready||!Criteria.Matches(entity)){RefreshNow();return false;}
            var camera=Camera.main!=null?Camera.main.GetComponent<OrbitCamera>():null;if(camera==null)return false;
            EntityViewController.Instance?.ExitView();var collider=entity.GetComponent<Collider>();camera.FocusOn(collider!=null?collider.bounds.center:entity.transform.position);
            LocatedEntity=entity;if(inspect)Object.FindFirstObjectByType<GeneInspectorUI>()?.ShowEntityInfo(entity);_results?.Refresh();return true;
        }
        private void OnDisable(){if(_card!=null)_card.gameObject.SetActive(false);_results?.Hide();}
        private void OnDestroy(){if(_card!=null)Destroy(_card.gameObject);if(_results!=null)Destroy(_results.gameObject);}
    }
}
