using System.Collections;
using System.Collections.Generic;
using DarwinPark.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DarwinPark.UI
{
    // ★ 修复（防Bug回归）：原生Dropdown在Start前Show会在淡入时抛异常，留下模板、列表且没有关闭遮罩。
    // 独立CanvasGroup仅在初始化的一帧内阻止展开，不覆盖业务的interactable/联机权限状态。
    [RequireComponent(typeof(Dropdown))]
    public sealed class RuntimeDropdownDismissal:MonoBehaviour,IPointerDownHandler,IPointerClickHandler,ICancelHandler
    {
        private static readonly HashSet<RuntimeDropdownDismissal> Active=new();
        private Dropdown _dropdown;private CanvasGroup _initializationGate;private Coroutine _arming;
        private bool _ready,_openAtPointerDown;
        private Transform Popup=>transform.Find("Dropdown List");
        public bool IsOpen=>_dropdown!=null&&(_dropdown.template!=null&&_dropdown.template.gameObject.activeSelf||Popup is Transform popup&&popup.gameObject.activeSelf);
        private void Awake(){_dropdown=GetComponent<Dropdown>();_initializationGate=gameObject.AddComponent<CanvasGroup>();}
        private void OnEnable()
        {
            Active.Add(this);_ready=false;_initializationGate.interactable=false;_initializationGate.blocksRaycasts=true;
            _arming=StartCoroutine(ArmAfterStart());
        }
        private IEnumerator ArmAfterStart()
        {
            yield return null;
            _ready=true;_initializationGate.interactable=true;_initializationGate.blocksRaycasts=true;_arming=null;
        }
        public void ClosePopup()
        {
            if(_dropdown==null)return;
            if(_ready&&_dropdown.isActiveAndEnabled)_dropdown.Hide();
            // ★ 修复（防Bug回归）：模板不是弹出列表；Hide只销毁列表，必须单独隐藏残留模板并立即停止其拦截。
            if(Popup is Transform popup)popup.gameObject.SetActive(false);
            if(_dropdown.template!=null)_dropdown.template.gameObject.SetActive(false);
        }
        public static bool CloseOpenPopups()
        {
            bool closed=false;foreach(var control in Active)if(control!=null&&control.IsOpen){control.ClosePopup();closed=true;}return closed;
        }
        public void OnPointerDown(PointerEventData data){_openAtPointerDown=IsOpen;}
        public void OnPointerClick(PointerEventData data){if(data.button==PointerEventData.InputButton.Left&&_openAtPointerDown)ClosePopup();}
        public void OnCancel(BaseEventData data){ClosePopup();GameInputGate.ConsumeEscape();data.Use();}
        private void LateUpdate()
        {
            if(_dropdown.template!=null&&_dropdown.template.gameObject.activeSelf)_dropdown.template.gameObject.SetActive(false);
            if(!IsOpen)return;
            var canvas=GetComponentInParent<Canvas>();
            if(canvas==null||!canvas.isActiveAndEnabled){ClosePopup();return;}
            // ★ 适配：异常展开时原生Blocker可能未生成；在鼠标抬起时补关，按下仍由遮罩拦截以免穿透游戏。
            if(Input.GetMouseButtonUp(0)&&Popup is RectTransform popup&&!RectTransformUtility.RectangleContainsScreenPoint(popup,Input.mousePosition,canvas.worldCamera)&&!RectTransformUtility.RectangleContainsScreenPoint((RectTransform)transform,Input.mousePosition,canvas.worldCamera))ClosePopup();
        }
        private void OnDisable()
        {
            if(_arming!=null){StopCoroutine(_arming);_arming=null;}ClosePopup();_ready=false;Active.Remove(this);
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] private static void Reset()=>Active.Clear();
    }
}
