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
        private int _openedFrame;
        public static void Show(EntitySpawnerUI source)
        {
            if(_current!=null){bool same=_current._source==source;_current.gameObject.SetActive(false);Destroy(_current.gameObject);if(same)return;}
            EntityPlacementTool.Instance?.CancelPlacement();
            if(!ServiceLocator.TryGet<UIRoot>(out var root))return;
            var go=UIFactory.NewRect("PlacementQuantity",root.MainCanvas.transform,typeof(Image));
            var rt=go.GetComponent<RectTransform>();rt.anchorMin=rt.anchorMax=new Vector2(.5f,0);
            rt.pivot=new Vector2(.5f,0);rt.anchoredPosition=new Vector2(0,225);rt.sizeDelta=new Vector2(360,110);
            go.GetComponent<Image>().color=new Color(.06f,.1f,.15f,.98f);
            _current=go.AddComponent<PlacementQuantityPopup>();_current._source=source;_current._openedFrame=Time.frameCount;
            var title=UIFactory.NewText("Title",go.transform,$"每次放置数量（1～{source.maxPlacementCount}）",15,TextAnchor.UpperCenter);
            UIFactory.Stretch(title.rectTransform,8,6);title.raycastTarget=false;
            var field=UIFactory.NewRect("Count",go.transform,typeof(Image),typeof(InputField));
            var fr=field.GetComponent<RectTransform>();fr.sizeDelta=new Vector2(95,30);fr.anchoredPosition=new Vector2(-85,-5);
            field.GetComponent<Image>().color=new Color(.12f,.2f,.28f);
            var input=field.GetComponent<InputField>();_current._input=input;
            var text=UIFactory.NewText("Text",field.transform,"",16,TextAnchor.MiddleCenter);UIFactory.Stretch(text.rectTransform,5,2);
            input.textComponent=text;input.contentType=InputField.ContentType.IntegerNumber;input.text=source.placementCount.ToString();
            var apply=UIFactory.NewButton("Apply",go.transform,"确定",_current.Apply,new Vector2(95,30));
            apply.GetComponent<RectTransform>().anchoredPosition=new Vector2(70,-5);
            var hint=UIFactory.NewText("Hint",go.transform,"回车确定 · 右键取消",12,TextAnchor.LowerCenter);UIFactory.Stretch(hint.rectTransform,8,4);hint.raycastTarget=false;
        }
        public void Apply()
        {
            if(_source!=null && int.TryParse(_input.text,out int value))
            {_source.placementCount=Mathf.Clamp(value,1,_source.maxPlacementCount);Close();}
        }
        private void Close(){gameObject.SetActive(false);Destroy(gameObject);}
        private void Update()
        {
            // ★ 适配：工具中的 Shift/回车不能提交背景游戏面板。
            if(DarwinPark.Core.GameInputGate.Blocked)return;
            if(Time.frameCount==_openedFrame)return;
            if(Input.GetMouseButtonDown(1)||Input.GetKeyDown(KeyCode.Escape))Close();
            else if(Input.GetKeyDown(KeyCode.Return)||Input.GetKeyDown(KeyCode.KeypadEnter))Apply();
        }
        private void OnDestroy(){if(_current==this)_current=null;}
    }
}
