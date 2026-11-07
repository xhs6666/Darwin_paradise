using System.Collections.Generic;
using DarwinPark.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DarwinPark.UI
{
    // ★ 修复（防Bug回归）：读档/暂停返回不能只恢复旧布尔快照；HUD可见时必须有实际可用的射线、事件系统和输入模块。
    [DefaultExecutionOrder(100)] public sealed class WorldHudInputLifecycle:MonoBehaviour
    {
        private GameMainMenu _menu;private WorldSessionManager _session;private UIRoot _root;
        private EventSystem _events;private BaseInputModule _input;private bool _reportReady;
        private readonly List<string> _repairs=new List<string>();
        public static void Ensure(GameMainMenu menu)
        {if(menu.GetComponent<WorldHudInputLifecycle>()==null)menu.gameObject.AddComponent<WorldHudInputLifecycle>();}
        public static void PrepareWorldControls()
        {
            // ★ 适配：在加载遮罩结束前重建回调，给子控件Start和布局留出正常的准备帧。
            RuntimeDropdownDismissal.CloseOpenPopups();Object.FindFirstObjectByType<SimulationControlPanel>()?.RebuildForWorld();
        }
        private void Awake(){_menu=GetComponent<GameMainMenu>();_session=GetComponent<WorldSessionManager>();}
        private void OnEnable(){if(_session!=null)_session.Loaded+=OnWorldLoaded;}
        private void OnDisable(){if(_session!=null)_session.Loaded-=OnWorldLoaded;}
        private void OnWorldLoaded()
        {
            // 旧世界展开的原生列表不能继续持有全屏点击遮罩。
            RuntimeDropdownDismissal.CloseOpenPopups();_reportReady=true;Recover();
        }
        private void LateUpdate()
        {
            Recover();
            if(_reportReady&&WorldRuntimeGate.Ready&&!WorldRuntimeGate.Loading&&(_menu==null||!_menu.isActiveAndEnabled||!_menu.Visible))
            {
                _reportReady=false;Debug.Log("[界面输入] 世界就绪，画布="+(_root!=null&&_root.MainCanvas.enabled)+"，事件系统="+(_events!=null&&_events.isActiveAndEnabled)+"，输入模块="+(_input!=null?_input.GetType().Name:"无"));
            }
        }
        public void Recover()
        {
            // ★ 修复（防Bug回归）：菜单、加载/录制和参数更新有意隐藏HUD；不能强行恢复造成点击穿透。
            if(!WorldRuntimeGate.Ready||WorldRuntimeGate.Loading||WorldRuntimeGate.Recording||_menu!=null&&_menu.isActiveAndEnabled&&_menu.Visible||WorldParameterUpdate.Instance?.Busy==true)return;
            if(_root==null&&!ServiceLocator.TryGet(out _root)||_root==null||!_root.isActiveAndEnabled||_root.MainCanvas==null)return;
            _repairs.Clear();
            if(!_root.MainCanvas.enabled){_root.MainCanvas.enabled=true;_repairs.Add("HUD画布");}
            var raycaster=_root.MainCanvas.GetComponent<GraphicRaycaster>();
            if(raycaster==null){raycaster=_root.MainCanvas.gameObject.AddComponent<GraphicRaycaster>();_repairs.Add("点击射线组件");}
            else if(!raycaster.enabled){raycaster.enabled=true;_repairs.Add("点击射线开关");}
            EnsureEvents();
            bool modal=PopulationWorkbench.Instance?.Visible==true||RandomSpawnWorkbench.Instance?.Visible==true||WorldGameSettingsPanel.Instance?.Visible==true;
            bool choosing=LanSession.Instance?.IsClient==true&&string.IsNullOrEmpty(LanSession.Instance.OwnedEntityId);
            if(!modal&&!choosing&&GameInputGate.Blocked)
            {
                // ★ 适配：仅释放没有实际所有者的持久门控；ConsumeEscape的当帧拦截仍由GameInputGate保留。
                GameInputGate.Blocked=false;if(!GameInputGate.Blocked)_repairs.Add("遗留输入门控");
            }
            if(_repairs.Count>0)Debug.LogWarning("[界面输入] 已恢复："+string.Join("、",_repairs));
        }
        private void EnsureEvents()
        {
            if(EventSystem.current!=null&&EventSystem.current.isActiveAndEnabled)_events=EventSystem.current;
            if(_events==null)_events=_root.GetComponentInChildren<EventSystem>(true);
            if(_events==null)_events=Object.FindFirstObjectByType<EventSystem>();
            if(_events==null)
            {var host=new GameObject("EventSystem",typeof(EventSystem),typeof(StandaloneInputModule));host.transform.SetParent(_root.transform,false);_events=host.GetComponent<EventSystem>();_repairs.Add("事件系统");}
            if(!_events.gameObject.activeSelf){_events.gameObject.SetActive(true);_repairs.Add("事件节点");}
            if(!_events.enabled){_events.enabled=true;_repairs.Add("事件系统开关");}
            if(EventSystem.current==null||!EventSystem.current.isActiveAndEnabled)EventSystem.current=_events;
            if(_input==null||_input.gameObject!=_events.gameObject||!_input.isActiveAndEnabled)
            {
                _input=null;BaseInputModule existing=null;
                foreach(var module in _events.GetComponents<BaseInputModule>())
                {if(existing==null)existing=module;if(module.isActiveAndEnabled){_input=module;break;}}
                if(_input==null)
                {_input=existing!=null?existing:_events.gameObject.AddComponent<StandaloneInputModule>();_input.enabled=true;_repairs.Add("UI输入模块");}
            }
            // ★ 适配：保留已工作的输入后端，不添加第二套模块，也不使用已废弃的强制激活开关。
            if(Application.isFocused&&!_events.isFocused)
            {_events.SendMessage("OnApplicationFocus",true,SendMessageOptions.DontRequireReceiver);_repairs.Add("UI焦点同步");}
        }
    }
}
