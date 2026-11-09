using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace DarwinPark.UI
{
    // ★ 新增：整张基因卡都可悬浮查看原因，移入数值框不关闭提示；滚轮继续交给原ScrollRect。
    public sealed class GeneHoverSurface:MonoBehaviour,IPointerEnterHandler,IPointerExitHandler,IScrollHandler
    {
        private Action _enter,_exit;
        public static void Bind(GameObject card,Action enter,Action exit)
        {var surface=card.AddComponent<GeneHoverSurface>();surface._enter=enter;surface._exit=exit;card.GetComponent<Image>().raycastTarget=true;}
        public void OnPointerEnter(PointerEventData data)=>_enter?.Invoke();
        public void OnPointerExit(PointerEventData data)=>_exit?.Invoke();
        public void OnScroll(PointerEventData data)=>GetComponentInParent<ScrollRect>()?.OnScroll(data);
        private void OnDisable()=>_exit?.Invoke();
    }
}
