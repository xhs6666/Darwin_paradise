using DarwinPark.Core;
using UnityEngine;
using UnityEngine.UI;

namespace DarwinPark.UI
{
    /// <summary>★ 新增：独立的槽位数量编辑器，修改默认/模板每次放置数量，不立即生成实体。</summary>
    public sealed class PlacementQuantityPopup : MonoBehaviour
    {
        private static PlacementQuantityPopup _current;
        private EntitySpawnerUI _source;
        private InputField _input;
        private Text _title,_hint;
        private Button _apply,_delete;
        private bool _deleting;
        private int _openedFrame;
        // ★ 修复（防Bug回归）：编辑器脚本重载会清空静态引用；重绑窗口后，丢失来源的旧窗口也能自行关闭。
        private void OnEnable(){if(_current!=null&&_current!=this)CloseAll();_current=this;}
        private void OnDisable(){if(_current==this)_current=null;}
        public static bool Visible=>_current!=null&&_current.gameObject.activeInHierarchy;
        // ★ 修复（防Bug回归）：窗口属于根 Canvas，关闭时立即清空引用并停用，不能仅等待延迟销毁。
        public static void CloseAll(){if(_current==null)return;var current=_current;_current=null;current.gameObject.SetActive(false);Destroy(current.gameObject);}
        public static void CloseFor(EntitySpawnerUI source){if(_current!=null&&_current._source==source)CloseAll();}
        public static void Show(EntitySpawnerUI source,bool deleting=false)
        {
            if(source==null||!source.isActiveAndEnabled||!WorldAuthority.CanEdit||GameInputGate.Blocked)return;
            if(_current!=null){bool same=_current._source==source&&_current._deleting==deleting;CloseAll();if(same)return;}
            EntityPlacementTool.Instance?.CancelPlacement();
            if(!ServiceLocator.TryGet<UIRoot>(out var root))return;
            var go=UIFactory.NewRect("PlacementQuantity",root.MainCanvas.transform,typeof(Image));
            var rt=go.GetComponent<RectTransform>();rt.anchorMin=rt.anchorMax=new Vector2(.5f,0);
            bool template=!string.IsNullOrEmpty(source.TemplateName);
            rt.pivot=new Vector2(.5f,0);rt.anchoredPosition=new Vector2(0,225);rt.sizeDelta=new Vector2(330,template?126:88);
            // ★ 适配：放置数量弹窗沿用原位置与输入规则，共享深色面板皮肤。
            UIVisualTheme.Paint(go.GetComponent<Image>());
            _current=go.AddComponent<PlacementQuantityPopup>();_current._source=source;_current._openedFrame=Time.frameCount;
            var title=UIFactory.NewText("Title",go.transform,$"每次放置数量（1～{source.maxPlacementCount}）",15,TextAnchor.UpperCenter);_current._title=title;
            UIFactory.Stretch(title.rectTransform,8,6);title.raycastTarget=false;
            title.rectTransform.anchorMin=new Vector2(0,1);title.rectTransform.anchorMax=Vector2.one;title.rectTransform.offsetMin=new Vector2(8,-28);title.rectTransform.offsetMax=new Vector2(-8,-6);
            var field=UIFactory.NewRect("Count",go.transform,typeof(Image),typeof(InputField));
            var fr=field.GetComponent<RectTransform>();fr.sizeDelta=new Vector2(95,28);fr.anchoredPosition=new Vector2(-72,template?12:-5);
            UIVisualTheme.Paint(field.GetComponent<Image>(),"field");
            var input=field.GetComponent<InputField>();_current._input=input;
            var text=UIFactory.NewText("Text",field.transform,"",16,TextAnchor.MiddleCenter);UIFactory.Stretch(text.rectTransform,5,2);
            input.textComponent=text;input.contentType=InputField.ContentType.IntegerNumber;input.text=source.placementCount.ToString();
            var apply=UIFactory.NewButton("Apply",go.transform,"确定",_current.Apply,new Vector2(95,30));
            _current._apply=apply;
            UIVisualTheme.StyleButton(apply,true);
            apply.GetComponent<RectTransform>().anchoredPosition=new Vector2(64,template?12:-5);
            var hint=UIFactory.NewText("Hint",go.transform,"回车确定 · 右键取消",12,TextAnchor.LowerCenter);_current._hint=hint;UIFactory.Stretch(hint.rectTransform,8,4);hint.raycastTarget=false;
            hint.rectTransform.anchorMin=Vector2.zero;hint.rectTransform.anchorMax=new Vector2(1,0);hint.rectTransform.offsetMin=new Vector2(8,3);hint.rectTransform.offsetMax=new Vector2(-8,21);
            if(template){_current._delete=UIFactory.NewButton("DeleteTemplate",go.transform,"删除此模板",_current.RequestDelete,new Vector2(180,26));_current._delete.GetComponent<RectTransform>().anchoredPosition=new Vector2(0,-23);UIVisualTheme.Paint(_current._delete.image,"danger");}
            if(deleting&&template)_current.RequestDelete();
        }
        public void Apply()
        {
            if(_source==null||!_source.isActiveAndEnabled||!WorldAuthority.CanEdit||GameInputGate.Blocked){CloseAll();return;}
            if(_deleting)
            {
                try{if(!EntityTemplateManager.DeleteTemplate(_source.TemplateName)){_hint.text="模板不存在或没有删除权限";return;}CloseAll();}catch(System.Exception error){_hint.text="删除失败："+error.Message;}
                return;
            }
            if(_source!=null && int.TryParse(_input.text,out int value))
            {_source.placementCount=Mathf.Clamp(value,1,_source.maxPlacementCount);CloseAll();}
        }
        private void RequestDelete()
        {
            // ★ 新增：右上角删除入口与数量页共用确认窗口；确认前不会改写模板文件。
            _deleting=true;_title.text="确认删除模板："+_source.TemplateName;_title.supportRichText=false;
            _input.gameObject.SetActive(false);_delete?.gameObject.SetActive(false);_apply.GetComponentInChildren<Text>().text="确认删除";_apply.GetComponent<RectTransform>().anchoredPosition=Vector2.zero;
            _hint.text="只删除模板，不影响已生成实体 · 右键取消";
            // ★ 修复（防Bug回归）：确认模式不沿用数量编辑器的空白高度，标题、按钮、说明紧密排布。
            ((RectTransform)transform).sizeDelta=new Vector2(330,104);
            _title.rectTransform.offsetMin=new Vector2(8,-36);_title.resizeTextForBestFit=true;_title.resizeTextMinSize=11;_title.resizeTextMaxSize=15;
            _apply.GetComponent<RectTransform>().anchoredPosition=new Vector2(0,-2);
        }
        private void Update()
        {
            // ★ 适配：工具中的 Shift/回车不能提交背景游戏面板。
            if(GameInputGate.Blocked||_source==null||!_source.isActiveAndEnabled||!WorldAuthority.CanEdit){CloseAll();return;}
            if(Time.frameCount==_openedFrame)return;
            if(Input.GetMouseButtonDown(1)||Input.GetKeyDown(KeyCode.Escape))CloseAll();
            else if(Input.GetKeyDown(KeyCode.Return)||Input.GetKeyDown(KeyCode.KeypadEnter))Apply();
        }
        private void OnDestroy(){if(_current==this)_current=null;}
    }
}
