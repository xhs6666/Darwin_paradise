using System.Collections.Generic;
using System.Linq;
using DarwinPark.Core;
using DarwinPark.Genetics;
using UnityEngine;
using UnityEngine.UI;

namespace DarwinPark.UI
{
    // ★ 新增：第二页的全星球快速生成栏，与原有默认/模板的定点放置操作并列。
    public sealed class RandomSpawnPanel : MonoBehaviour
    {
        public int defaultCount=10;
        public float leftOffset=440,preferredWidth=240;
        private RandomPlanetSpawner _spawner;private Dropdown _source;private InputField _count;private Button _range,_generate,_cancel;private Text _status;
        private readonly List<EntityTemplateData> _templates=new List<EntityTemplateData>();
        private bool _landOnly=true;
        public static void Attach(Transform parent)
        {
            if(parent.Find("RandomSpawn")!=null)return;
            var go=UIFactory.NewRect("RandomSpawn",parent,typeof(Image));UIVisualTheme.Paint(go.GetComponent<Image>());go.AddComponent<RandomSpawnPanel>();
        }
        private void Start()
        {
            _spawner=RandomPlanetSpawner.Ensure(ServiceLocator.Get<SimulationClock>().gameObject);Build();RefreshSources();EntityTemplateManager.OnTemplatesChanged+=RefreshSources;
        }
        private void OnDestroy(){EntityTemplateManager.OnTemplatesChanged-=RefreshSources;}
        private void Build()
        {
            var title=UIFactory.NewText("Title",transform,"随机生成 · 全星球",15);Place(title.rectTransform,0,-6,1,24);
            var dropdown=DefaultControls.CreateDropdown(new DefaultControls.Resources());dropdown.name="Source";dropdown.transform.SetParent(transform,false);_source=dropdown.GetComponent<Dropdown>();Place(dropdown.GetComponent<RectTransform>(),0,-32,1,28);
            foreach(var text in dropdown.GetComponentsInChildren<Text>(true)){text.font=UIFactory.DefaultFont;text.fontSize=13;text.color=UIVisualTheme.Text;}
            UIVisualTheme.Paint(dropdown.GetComponent<Image>(),"field");UIVisualTheme.Paint(_source.template.GetComponent<Image>());
            // ★ 修复（防Bug回归）：运行时默认下拉控件没有箭头贴图会显示白块，复用已有方向图标并统一选项皮肤。
            var arrow=dropdown.transform.Find("Arrow").GetComponent<Image>();arrow.sprite=UIIconRegistry.Get("ui_icon_next");arrow.color=UIVisualTheme.Accent;arrow.preserveAspect=true;arrow.rectTransform.sizeDelta=new Vector2(16,16);arrow.rectTransform.localRotation=Quaternion.Euler(0,0,-90);
            var item=_source.template.GetComponentInChildren<Toggle>(true);UIVisualTheme.Paint(item.targetGraphic as Image,"hover");if(item.graphic!=null)item.graphic.color=UIVisualTheme.Accent;
            // ★ 适配：默认滚动条和勾选图也需要换肤，避免白色/绿色方块。
            RuntimeDropdownSkin.Apply(_source);
            // ★ 适配：栏宽减半后给双字标签保留完整字宽，输入框和范围按钮继续各占独立区域。
            var label=UIFactory.NewText("QuantityLabel",transform,"数量",13);Place(label.rectTransform,0,-66,.18f,28);
            var field=UIFactory.NewRect("Quantity",transform,typeof(Image),typeof(InputField));_count=field.GetComponent<InputField>();Place(field.GetComponent<RectTransform>(),.18f,-66,.43f,28);_count.contentType=InputField.ContentType.IntegerNumber;
            var value=UIFactory.NewText("Text",field.transform,defaultCount.ToString(),14,TextAnchor.MiddleCenter);UIFactory.Stretch(value.rectTransform);_count.textComponent=value;_count.text=defaultCount.ToString();
            _range=UIFactory.NewButton("Range",transform,"范围：仅陆地",()=>{_landOnly=!_landOnly;_range.GetComponentInChildren<Text>().text=_landOnly?"范围：仅陆地":"范围：全部地表";},new Vector2(160,28));Place(_range.GetComponent<RectTransform>(),.44f,-66,1,28);
            _generate=UIFactory.NewButton("Generate",transform,"随机生成",Generate,new Vector2(170,30));Place(_generate.GetComponent<RectTransform>(),0,-100,.65f,30);
            _cancel=UIFactory.NewButton("Cancel",transform,"取消",()=>_spawner.Cancel(),new Vector2(90,30));Place(_cancel.GetComponent<RectTransform>(),.68f,-100,1,30);
            foreach(var button in new[]{_range,_generate,_cancel})button.GetComponentInChildren<Text>().fontSize=13;
            _status=UIFactory.NewText("Status",transform,"",12);Place(_status.rectTransform,0,-132,1,22);
        }
        private static void Place(RectTransform rt,float left,float top,float right,float height)
        {rt.anchorMin=new Vector2(left,1);rt.anchorMax=new Vector2(right,1);rt.pivot=new Vector2(.5f,1);rt.offsetMin=new Vector2(8,top-height);rt.offsetMax=new Vector2(-8,top);}
        private void RefreshSources()
        {
            if(_source==null)return;string selected=_source.value>0&&_source.value<=_templates.Count?_templates[_source.value-1].templateName:null;
            _templates.Clear();_templates.AddRange(EntityTemplateManager.Templates);_source.ClearOptions();var names=new List<string>{"默认实体"};names.AddRange(_templates.Select(t=>"模板："+t.templateName));_source.AddOptions(names);
            _source.value=selected!=null?Mathf.Max(0,_templates.FindIndex(t=>t.templateName==selected)+1):0;_source.RefreshShownValue();
        }
        public void Generate()
        {
            if(!WorldAuthority.CanEdit)return;
            if(_spawner==null)return;int count=int.TryParse(_count.text,out var parsed)?Mathf.Clamp(parsed,1,_spawner.maxCount):defaultCount;_count.text=count.ToString();Genome genome=null;
            if(_source.value>0&&_source.value<=_templates.Count)
            {genome=new Genome();foreach(var gene in _templates[_source.value-1].genes)genome.Add(GeneInstance.Create(gene.definitionId,gene.value,gene.tier));}
            _spawner.StartBatch(genome,count,_landOnly);
        }
        private void Update()
        {
            var rect=(RectTransform)transform;float available=((RectTransform)transform.parent).rect.width-leftOffset-12;
            rect.anchorMin=Vector2.zero;rect.anchorMax=new Vector2(0,1);rect.offsetMin=new Vector2(leftOffset,0);rect.offsetMax=new Vector2(leftOffset+Mathf.Min(preferredWidth,Mathf.Max(240,available)),0);
            if(_spawner==null)return;_status.text=_spawner.Status;_generate.interactable=WorldAuthority.CanEdit&&!_spawner.IsBusy;_source.interactable=_count.interactable=_range.interactable=WorldAuthority.CanEdit&&!_spawner.IsBusy;_cancel.interactable=WorldAuthority.CanEdit&&_spawner.IsBusy;
        }
    }
}
