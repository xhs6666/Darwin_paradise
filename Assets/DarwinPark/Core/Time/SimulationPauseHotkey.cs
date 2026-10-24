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

        private SimulationClock _clock;

        private void Start()
        {
            _clock = ServiceLocator.Get<SimulationClock>("SimulationPauseHotkey 需要 SimulationClock");
        }

        private void Update()
        {
            if (_clock == null) return;
            if (!Input.GetKeyDown(pauseKey)) return;
            if (IsTypingInInputField()) return;

            _clock.Toggle();
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
