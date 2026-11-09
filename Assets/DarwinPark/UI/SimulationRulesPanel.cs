using DarwinPark.Core;
using DarwinPark.Entities.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace DarwinPark.UI
{
    // ★ 新增：第一页独立规则卡与统计/筛选同为260×164，窄屏用“规则”展开同尺寸卡，不挤压旧控件。
    public sealed class SimulationRulesPanel:MonoBehaviour
    {
        public float cardWidth=260,cardHeight=164,leftOffset=1380;
        private RectTransform _card;private Button _toggle,_apply,_reload,_launch;private InputField _fraction,_seconds;private Text _status;
        private bool _dirty,_floating;private GameplayTuning _seen;private float _next;
        public RectTransform Card=>_card;
        // ★ 适配：分页规则在独立脚本维护，保留原卡尺寸和输入控件路径。
        private RuleCardExtensions _extensions;
        public void MarkRuleDirty()=>_dirty=true;
        private void Start()
        {
            UIFactory.Stretch((RectTransform)transform);
            var card=UIFactory.NewRect("Card",transform,typeof(Image));_card=(RectTransform)card.transform;UIVisualTheme.Paint(card.GetComponent<Image>());
            Label("Title","规则控制",10,3,240,18,13).color=UIVisualTheme.Muted;
            _toggle=Button("MutationEnabled","定时全局变异",Toggle,10,25,240,23);
            Label("FractionLabel","抽样",10,54,42,23,13);_fraction=Input("Fraction",55,54,100);Label("FractionUnit","% 活体",163,54,85,23,12);
            Label("PeriodLabel","周期",10,82,42,23,13);_seconds=Input("Seconds",55,82,100);Label("PeriodUnit","模拟秒",163,82,85,23,12);
            _apply=Button("ApplyRules","应用并保存",Apply,10,111,151,25);_reload=Button("ReloadRules","重载",Reload,167,111,83,25);
            _status=Label("Status","",10,139,240,19,11);_status.resizeTextForBestFit=true;_status.resizeTextMinSize=9;_status.resizeTextMaxSize=11;
            _launch=UIFactory.NewButton("OpenRules",transform,"规则",()=>{_floating=!_floating;},new Vector2(66,26)).GetComponent<Button>();
            _extensions=gameObject.AddComponent<RuleCardExtensions>();_extensions.Bind(this,_fraction,_seconds,_toggle);Sync();
        }
        private static void Place(RectTransform rect,float x,float y,float width,float height){var anchor=new Vector2(0,1);if(rect.anchorMin!=anchor||rect.anchorMax!=anchor||rect.pivot!=anchor)rect.anchorMin=rect.anchorMax=rect.pivot=anchor;var point=new Vector2(x,-y);var size=new Vector2(width,height);if(rect.anchoredPosition!=point)rect.anchoredPosition=point;if(rect.sizeDelta!=size)rect.sizeDelta=size;}
        private Text Label(string name,string value,float x,float y,float width,float height,int font){var label=UIFactory.NewText(name,_card,value,font);Place(label.rectTransform,x,y,width,height);return label;}
        private Button Button(string name,string value,UnityEngine.Events.UnityAction action,float x,float y,float width,float height){var button=UIFactory.NewButton(name,_card,value,()=>action(),new Vector2(width,height)).GetComponent<Button>();Place((RectTransform)button.transform,x,y,width,height);button.GetComponentInChildren<Text>().fontSize=12;return button;}
        private InputField Input(string name,float x,float y,float width)
        {
            var go=UIFactory.NewRect(name,_card,typeof(Image),typeof(InputField));Place((RectTransform)go.transform,x,y,width,23);var input=go.GetComponent<InputField>();var text=UIFactory.NewText("Text",go.transform,"",13);UIFactory.Stretch(text.rectTransform,7,1);input.textComponent=text;input.contentType=InputField.ContentType.DecimalNumber;input.onValueChanged.AddListener(_=>_dirty=true);return input;
        }
        private void Toggle(){_extensions.Toggle();}
        public void Reload()
        {GameplayTuning.Reload();Sync();if(_status!=null){_status.text="已载入当前有效规则";_next=Time.unscaledTime+2;}}
        private void Sync()
        {
            _seen=GameplayTuning.Current;_extensions.Sync(_seen);_dirty=false;
        }
        public void Apply()
        {
            if(!WorldAuthority.CanEdit||WorldAuthority.IsReplica||!WorldRuntimeGate.Ready||WorldRuntimeGate.Loading){_status.text="仅房主/管理员可修改";return;}
            try{_extensions.Save();Sync();_status.text="规则已保存并生效";_next=Time.unscaledTime+3;}catch(System.Exception ex){_status.text=ex.Message;_next=Time.unscaledTime+3;}
        }
        private void LateUpdate()
        {
            if(_card==null||_status==null)return;var parent=(RectTransform)transform.parent;
            bool wide=parent.rect.width>=leftOffset+cardWidth+12;_launch.gameObject.SetActive(!wide);var launch=(RectTransform)_launch.transform;launch.anchorMin=launch.anchorMax=launch.pivot=new Vector2(1,1);launch.anchoredPosition=new Vector2(-8,31);
            // ★ 修复（防Bug回归）：窄屏规则浮卡在筛选浮卡上方，两个入口同时打开也不重叠。
            _card.gameObject.SetActive(wide||_floating);Place(_card,wide?leftOffset:Mathf.Max(12,parent.rect.width-cardWidth-12),wide?0:-2*cardHeight-18,cardWidth,cardHeight);
            bool editable=WorldAuthority.CanEdit&&!WorldAuthority.IsReplica&&WorldRuntimeGate.Ready&&!WorldRuntimeGate.Loading;_toggle.interactable=_apply.interactable=_fraction.interactable=_seconds.interactable=editable;
            _seconds.interactable=editable&&_extensions.SecondsEditable;_extensions.SetEditable(editable);
            if(_floating&&UnityEngine.Input.GetKeyDown(KeyCode.Escape)){GameInputGate.ConsumeEscape();_floating=false;}
            if(Time.unscaledTime<_next)return;_next=Time.unscaledTime+.5f;
            if(!ReferenceEquals(_seen,GameplayTuning.Current)&&!_dirty&&!_fraction.isFocused&&!_seconds.isFocused)Sync();
            _status.text=!editable?"规则由房主本机控制":_dirty?"尚未保存":_extensions.Status();
        }
        private void OnDisable()=>_floating=false;
    }
}
