using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DarwinPark.Core
{
    /// <summary>
    /// 空格键暂停/继续。
    /// ★ 新增：独立输入组件，挂到 GameRoot；不把热键逻辑塞进 SimulationClock，
    /// 便于后续改键或按场景禁用。输入框获得焦点时跳过，避免输入基因数值被误暂停。
    /// </summary>
    public class SimulationPauseHotkey : MonoBehaviour
    {
        [Header("热键")]
        public KeyCode pauseKey = KeyCode.Space;
        public KeyCode viewPauseKey = KeyCode.P;

        private SimulationClock _clock;

        private void Start()
        {
            _clock = ServiceLocator.Get<SimulationClock>("SimulationPauseHotkey 需要 SimulationClock");
        }

        private void Update()
        {
            // ★ 适配：覆盖菜单拥有输入时不处理世界操作。
            if (DarwinPark.Core.GameInputGate.Blocked) return;
            if (_clock == null) return;
            // ★ 修复（防Bug回归）：操控视角空格留给跳跃，P 仍能启动/暂停，不能把全部时间控制一起禁掉。
            bool view=Camera.main!=null&&Camera.main.GetComponent<OrbitCamera>() is OrbitCamera orbit&&orbit.EntityViewActive;
            if(Input.GetKeyDown(viewPauseKey))HandlePauseKey(viewPauseKey,view,IsTypingInInputField());
            else if(Input.GetKeyDown(pauseKey))HandlePauseKey(pauseKey,view,IsTypingInInputField());
        }

        public bool HandlePauseKey(KeyCode key,bool entityView,bool typing)
        {
            if(GameInputGate.Blocked||!WorldAuthority.CanEdit||typing||key!=viewPauseKey&&(entityView||key!=pauseKey))return false;
            if(_clock==null&&!ServiceLocator.TryGet<SimulationClock>(out _clock))return false;
            _clock.Toggle();return true;
        }

        private bool IsTypingInInputField()
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null) return false;

            var selected = eventSystem.currentSelectedGameObject;
            if (selected == null) return false;

            var input = selected.GetComponentInParent<InputField>();
            return input != null && input.isFocused;
        }
    }
}
