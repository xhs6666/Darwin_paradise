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
    // ★ 新增：统计全地图存活实体，不依赖镜头、当前选择或管理员权限；复制基因按实体去重计数。
    public sealed class MapEntityStatisticsUI : MonoBehaviour
    {
        public float leftOffset=840,rightOffset=12,preferredWidth=260,preferredHeight=164,readOnlyHeight=164,refreshInterval=.5f;
        public int Total {get;private set;}
        public int Carriers {get;private set;}
        public int Expressed {get;private set;}
        public int Infants {get;private set;}
        public int Adults {get;private set;}
        public int Elders {get;private set;}
        public string FilterGeneId=>_filter!=null&&_filter.value>0&&_filter.value<=_genes.Count?_genes[_filter.value-1].Id:null;
        private Text _total,_ages,_result;private Dropdown _filter;private string _catalog;
        private readonly List<GeneDefinition> _genes=new List<GeneDefinition>();private float _nextRefresh;
        private void Start(){Build();RefreshNow();}
        private Text Label(string name,string text,float y,float height,int fontSize)
        {
            var label=UIFactory.NewText(name,transform,text,fontSize);Place(label.rectTransform,y,height);
            label.resizeTextForBestFit=true;label.resizeTextMinSize=11;label.resizeTextMaxSize=fontSize;return label;
        }
        private static void Place(RectTransform rect,float y,float height)
        {
            rect.anchorMin=new Vector2(0,1);rect.anchorMax=Vector2.one;rect.pivot=new Vector2(.5f,1);
            rect.offsetMin=new Vector2(12,-y-height);rect.offsetMax=new Vector2(-12,-y);
        }
        private void Build()
        {
            UIVisualTheme.Paint(GetComponent<Image>());GetComponent<Image>().raycastTarget=false;
            Label("Heading","地图实体统计",4,17,13).color=UIVisualTheme.Muted;
            _total=Label("Total","实体总数  0",22,25,20);_total.color=UIVisualTheme.Accent;
            _ages=Label("Ages","",48,19,12);
            var dropdown=DefaultControls.CreateDropdown(new DefaultControls.Resources());dropdown.name="GeneFilter";dropdown.transform.SetParent(transform,false);
            _filter=dropdown.GetComponent<Dropdown>();Place(dropdown.GetComponent<RectTransform>(),71,26);
            UIVisualTheme.Paint(dropdown.GetComponent<Image>(),"field");UIVisualTheme.Paint(_filter.template.GetComponent<Image>());
            foreach(var text in dropdown.GetComponentsInChildren<Text>(true)){text.font=UIFactory.DefaultFont;text.fontSize=13;text.color=UIVisualTheme.Text;}
            var arrow=dropdown.transform.Find("Arrow").GetComponent<Image>();arrow.sprite=UIIconRegistry.Get("ui_icon_next");arrow.color=UIVisualTheme.Accent;arrow.preserveAspect=true;arrow.rectTransform.sizeDelta=new Vector2(16,16);arrow.rectTransform.localRotation=Quaternion.Euler(0,0,-90);
            var item=_filter.template.GetComponentInChildren<Toggle>(true);UIVisualTheme.Paint(item.targetGraphic as Image,"hover");if(item.graphic!=null)item.graphic.color=UIVisualTheme.Accent;
            // ★ 适配：统计列表与组合筛选使用同一完整模板皮肤。
            RuntimeDropdownSkin.Apply(_filter);
            _filter.onValueChanged.AddListener(_=>RefreshNow());_result=Label("FilterResult","",101,30,12);
            // ★ 适配：设置与清理独立组件管理，不混入统计口径。
            gameObject.AddComponent<MapEntityActions>();
            // ★ 适配：组合筛选与分页定位由独立组件提供，原统计与“清除全部”口径保持明确。
            MapEntityGeneFilterUI.Attach(this);
        }
        private void LateUpdate()
        {
            // ★ 适配：信息区仅放在第一页参数按钮右侧；宽度不足时限制到剩余空间，不挤压原按钮。
            // ★ 修复（防Bug回归）：原来横向拉伸填满整片空白，形成过长面板；改为受配置限制的紧凑固定宽度。
            var parent=(RectTransform)transform.parent;float width=Mathf.Max(0,Mathf.Min(preferredWidth,parent.rect.width-leftOffset-rightOffset));
            // ★ 修复（防Bug回归）：继续缩短卡片宽高，底部留空不再把信息卡纵向拉满。
            float bottom=Mathf.Max(0,parent.rect.height-(WorldAuthority.CanEdit?preferredHeight:readOnlyHeight));
            var rect=(RectTransform)transform;var min=new Vector2(leftOffset,bottom);var max=new Vector2(leftOffset+width,0);var topAnchor=new Vector2(0,1);
            // ★ 修复（防Bug回归）：稳定布局不重复标脏Canvas，避免统计功能导致整条底栏每帧重建。
            if(rect.anchorMin!=Vector2.zero||rect.anchorMax!=topAnchor||rect.offsetMin!=min||rect.offsetMax!=max)
            {rect.anchorMin=Vector2.zero;rect.anchorMax=topAnchor;rect.offsetMin=min;rect.offsetMax=max;}
            if(Time.unscaledTime>=_nextRefresh)RefreshNow();
        }
        public bool SelectGene(string id)
        {
            if(_filter==null)return false;int index=_genes.FindIndex(g=>g.Id==id);if(id!=null&&index<0)return false;
            _filter.SetValueWithoutNotify(id==null?0:index+1);RefreshNow();return true;
        }
        public void RefreshNow()
        {
            if(_filter==null)return;_nextRefresh=Time.unscaledTime+Mathf.Max(.1f,refreshInterval);
            string selected=FilterGeneId;var catalog=GeneDatabase.All.ToArray();string signature=string.Join("|",catalog.Select(g=>g.Id+":"+g.DisplayName));
            if(_catalog!=signature)
            {
                _catalog=signature;_genes.Clear();_genes.AddRange(catalog.OrderBy(g=>g.Tier).ThenBy(g=>g.Id));
                _filter.ClearOptions();var labels=new List<string>{"基因筛选：全部实体"};labels.AddRange(_genes.Select(g=>"基因："+g.DisplayName));_filter.AddOptions(labels);
                _filter.SetValueWithoutNotify(selected==null?0:Mathf.Max(0,_genes.FindIndex(g=>g.Id==selected)+1));_filter.RefreshShownValue();
            }
            Total=Carriers=Expressed=Infants=Adults=Elders=0;selected=FilterGeneId;
            if(WorldRuntimeGate.Ready)foreach(var entity in EntityRegistry.All)
            {
                if(!EcologyQueries.Alive(entity)||entity.Genes==null)continue;Total++;
                switch(entity.Genes.CurrentAge){case AgeState.Infant:Infants++;break;case AgeState.Adult:Adults++;break;case AgeState.Elder:Elders++;break;}
                if(selected==null)continue;
                bool carries=false,active=false;
                foreach(var gene in entity.Genes.Genome.Genes)if(gene.DefinitionId==selected){carries=true;break;}
                if(carries)Carriers++;
                if(entity.Genes.Evaluation!=null)foreach(var gene in entity.Genes.Evaluation.Genes)if(gene.Gene.DefinitionId==selected&&gene.IsActive){active=true;break;}
                if(active)Expressed++;
            }
            _total.text="实体总数  "+Total;_ages.text=$"幼年 {Infants}   成年 {Adults}   老年 {Elders}";
            _result.text=selected==null?"全地图存活个体 · 可筛选基因":$"携带 {Carriers}  生效 {Expressed}\n占比 {(Total>0?Carriers*100f/Total:0):F1}%";
        }
    }
}
