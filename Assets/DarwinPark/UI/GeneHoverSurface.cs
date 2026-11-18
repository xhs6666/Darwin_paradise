using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace DarwinPark.UI
{
    // ★ 新增：整张基因卡都可悬浮查看原因，移入数值框不关闭提示；滚轮继续交给原ScrollRect。
    public sealed class GeneHoverSurface:MonoBehaviour,IPointerEnterHandler,IPointerExitHandler,IScrollHandler,IPointerClickHandler
    {
        private Action _enter,_exit,_doubleClick;private ScrollRect _scroll;
        public static void Bind(GameObject card,Action enter,Action exit,Action doubleClick=null)
        {var surface=card.AddComponent<GeneHoverSurface>();surface._enter=enter;surface._exit=exit;surface._doubleClick=doubleClick;surface._scroll=card.GetComponentInParent<ScrollRect>();card.GetComponent<Image>().raycastTarget=true;}
        public void OnPointerEnter(PointerEventData data)=>_enter?.Invoke();
        public void OnPointerExit(PointerEventData data)=>_exit?.Invoke();
        // ★ 适配：同类卡片事件合并到一个组件；输入控件仍自行接收点击，双击数值框不会打开基因树。
        public void OnScroll(PointerEventData data)=>_scroll?.OnScroll(data);
        public void OnPointerClick(PointerEventData data){if(data.button==PointerEventData.InputButton.Left&&data.clickCount==2)_doubleClick?.Invoke();}
        private void OnDisable()=>_exit?.Invoke();
    }
}
