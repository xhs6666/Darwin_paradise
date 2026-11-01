using DarwinPark.Core;
using DarwinPark.Entities;
using UnityEngine;
using UnityEngine.UI;

namespace DarwinPark.UI
{
    // ★ 新增：独立的存档内设置弹窗，输入值与持久化规则共用权威操作入口，不影响全局画面设置。
    public sealed class WorldGameSettingsPanel:MonoBehaviour
    {
        public static WorldGameSettingsPanel Instance {get;private set;}
        private void Awake()=>Instance=this;
        private void OnDestroy(){if(Instance==this)Instance=null;}
        public Vector2 panelSize=new Vector2(360,220);public bool Visible=>_overlay!=null;
        private GameObject _overlay;private InputField _limit;private Text _status;private bool _wasRunning,_blocked;
        public static WorldGameSettingsPanel Ensure(){var root=ServiceLocator.Get<UIRoot>();return root.GetComponent<WorldGameSettingsPanel>()??root.gameObject.AddComponent<WorldGameSettingsPanel>();}
        public void Open()
        {
            if(Visible||!WorldAuthority.CanEdit||!WorldRuntimeGate.Ready||WorldRuntimeGate.Loading)return;
            _blocked=GameInputGate.Blocked;_wasRunning=ServiceLocator.Get<SimulationClock>().IsRunning;if(!WorldAuthority.Online)ServiceLocator.Get<SimulationClock>().Pause();GameInputGate.Blocked=true;
            _overlay=UIFactory.NewRect("WorldGameSettingsOverlay",ServiceLocator.Get<UIRoot>().MainCanvas.transform,typeof(Image));UIFactory.Stretch(_overlay.GetComponent<RectTransform>());_overlay.GetComponent<Image>().color=new Color(0,0,0,.6f);
            var panel=UIFactory.NewRect("Panel",_overlay.transform,typeof(Image));UIVisualTheme.Paint(panel.GetComponent<Image>());var rect=panel.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.sizeDelta=panelSize;
            Label(panel.transform,"Title","本存档游戏设置",new Vector2(16,-12),new Vector2(328,26),18);
            Label(panel.transform,"LimitLabel","实体上限（0 表示不限）",new Vector2(16,-51),new Vector2(328,22),14);
            var input=UIFactory.NewRect("EntityLimit",panel.transform,typeof(Image),typeof(InputField));UIVisualTheme.Paint(input.GetComponent<Image>(),"field");Place(input.GetComponent<RectTransform>(),new Vector2(16,-80),new Vector2(328,30));_limit=input.GetComponent<InputField>();
            var text=UIFactory.NewText("Text",input.transform,"",16);UIFactory.Stretch(text.rectTransform,8,2);_limit.textComponent=text;_limit.contentType=InputField.ContentType.IntegerNumber;_limit.text=EntityPopulationLimit.Maximum.ToString();
            _status=Label(panel.transform,"Status","当前实体 "+EntityRegistry.All.Count+"；降低上限不会删除已有实体",new Vector2(16,-116),new Vector2(328,38),12);
            var save=UIFactory.NewButton("Apply",panel.transform,"保存设置",Apply,new Vector2(152,32));Place(save.GetComponent<RectTransform>(),new Vector2(16,-170),new Vector2(152,32));
            var close=UIFactory.NewButton("Close",panel.transform,"关闭",Close,new Vector2(152,32));Place(close.GetComponent<RectTransform>(),new Vector2(192,-170),new Vector2(152,32));_overlay.transform.SetAsLastSibling();
        }
        private static void Place(RectTransform rect,Vector2 point,Vector2 size){rect.anchorMin=rect.anchorMax=new Vector2(0,1);rect.pivot=new Vector2(0,1);rect.anchoredPosition=point;rect.sizeDelta=size;}
        private static Text Label(Transform parent,string name,string text,Vector2 point,Vector2 size,int font){var label=UIFactory.NewText(name,parent,text,font);Place(label.rectTransform,point,size);return label;}
        public void Apply()
        {
            if(!WorldAuthority.CanEdit||!int.TryParse(_limit.text,out int value)||value<0){_status.text="请输入非负整数，0 表示不限";return;}
            try{_status.text=LanEditActions.Submit(new LanCommand{kind="worldSettings",count=value,running=_wasRunning})?(WorldAuthority.IsReplica?"设置已发送给管理员":"设置已保存到当前存档"):"设置未应用";}catch(System.Exception ex){_status.text="保存失败："+ex.Message;Debug.LogException(ex);}
        }
        private void Update(){if(!Visible)return;if(!WorldAuthority.CanEdit||!WorldRuntimeGate.Ready||WorldRuntimeGate.Loading){Close();return;}if(Input.GetKeyDown(KeyCode.Escape)){GameInputGate.ConsumeEscape();Close();}}
        public void Close(){if(!Visible)return;_overlay.SetActive(false);Destroy(_overlay);_overlay=null;GameInputGate.Blocked=WorldRuntimeGate.Ready?_blocked:true;if(!WorldAuthority.Online&&_wasRunning&&WorldRuntimeGate.Ready&&!WorldRuntimeGate.Loading)ServiceLocator.Get<SimulationClock>().Play();}
        private void OnDisable(){Close();}
    }
}
