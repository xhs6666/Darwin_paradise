using DarwinPark.Core;
using DarwinPark.Entities;
using UnityEngine;
using UnityEngine.UI;

namespace DarwinPark.UI
{
    /// <summary>★ 新增：实体视角专用锁鼠标、准星与按键；退出/失焦时成对释放输入。</summary>
    public sealed class EntityPossessionInput : MonoBehaviour
    {
        // ★ 适配：设置中的绑定直接用于输入，不保留另一套Inspector按键导致界面修改无效。
        public KeyCode forwardKey=>ControlBindings.Get(ControlAction.Forward);
        public KeyCode backKey=>ControlBindings.Get(ControlAction.Back);
        public KeyCode leftKey=>ControlBindings.Get(ControlAction.Left);
        public KeyCode rightKey=>ControlBindings.Get(ControlAction.Right);
        public KeyCode jumpKey=>ControlBindings.Get(ControlAction.Jump);
        public KeyCode cursorKey=>ControlBindings.Get(ControlAction.ReleaseCursor);
        public bool CursorReleased {get;private set;}
        public int attackMouseButton=>(int)ControlBindings.Get(ControlAction.Attack)-(int)KeyCode.Mouse0;
        public float lookSensitivity = 3, zoomSensitivity = 4, minPitch = -80, maxPitch = 80;
        public float crosshairSize = 18, crosshairThickness = 2;
        private CursorLockMode _savedLock;
        private bool _savedVisible, _active;
        private GameObject _crosshair;
        private EntityManualControl _manual;
        private EntityJumpMotor _jump;
        private float _pitch;
        public float Pitch => _pitch;
        private int _captureFrame;
        private bool _acceptAttack;
        public void Begin(EntityManualControl manual, Transform planet, float height, float distance)
        {
            _manual = manual;
            _jump = manual.GetComponent<EntityJumpMotor>() ?? manual.gameObject.AddComponent<EntityJumpMotor>();
            _jump.Bind(planet);
            _pitch = 0; // ★ 修复（防Bug回归）：第一人称进入时平视，滚轮不能改变眼位。
            _savedLock = Cursor.lockState; _savedVisible = Cursor.visible; _active = true;CursorReleased=false;
            SetCaptured(true);
            if (ServiceLocator.TryGet<UIRoot>(out var root) && root.MainCanvas != null)
            {
                _crosshair = UIFactory.NewRect("EntityCrosshair", root.MainCanvas.transform);
                var rt = _crosshair.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f, .5f);
                rt.sizeDelta = new Vector2(crosshairSize, crosshairSize);
                AddLine("Horizontal", new Vector2(crosshairSize, crosshairThickness));
                AddLine("Vertical", new Vector2(crosshairThickness, crosshairSize));
            }
        }
        private void AddLine(string name, Vector2 size)
        {
            var line = UIFactory.NewRect(name, _crosshair.transform, typeof(Image));
            var rt = line.GetComponent<RectTransform>(); rt.anchorMin = rt.anchorMax = new Vector2(.5f,.5f); rt.sizeDelta = size;
            line.GetComponent<Image>().color = Color.white; line.GetComponent<Image>().raycastTarget = false;
        }
        public void Tick(bool blocked, Vector3 up, ref Vector3 forward, ref float height, ref float distance, float minDistance, float maxDistance)
        {
            if (!_active) return;
            // ★ 修复（防Bug回归）：Alt 是按住释放、松开恢复；不能沿用切换开关造成松键后仍无法操控。
            SetCursorReleased(ControlBindings.Held(ControlAction.ReleaseCursor));
            bool running=!ServiceLocator.TryGet<SimulationClock>(out var clock)||clock.IsRunning;
            blocked |= !Application.isFocused || GameInputGate.Blocked || CursorReleased;
            SetCaptured(!blocked);
            if (_crosshair != null) _crosshair.SetActive(!blocked&&running);
            _acceptAttack=!blocked&&running&&Time.frameCount>_captureFrame+1;
            if (blocked||!running) { _manual.SetInput(Vector3.zero, false);_manual.ClearAttack(); return; }
            // ★ 修复（防Bug回归）：刚锁鼠标时的居中位移不代表玩家转向，跳过捕获后的前两帧。
            if (Time.frameCount > _captureFrame + 1)
            {
                forward = Quaternion.AngleAxis(Input.GetAxis("Mouse X") * lookSensitivity, up) * forward;
                _pitch = Mathf.Clamp(_pitch - Input.GetAxis("Mouse Y") * lookSensitivity, minPitch, maxPitch);
            }
            // ★ 修复（防Bug回归）：锁定第一人称，禁用滚轮拉远。
            float y = (Input.GetKey(forwardKey) ? 1 : 0) - (Input.GetKey(backKey) ? 1 : 0);
            float x = (Input.GetKey(rightKey) ? 1 : 0) - (Input.GetKey(leftKey) ? 1 : 0);
            if(_acceptAttack && ControlBindings.Down(ControlAction.Attack))_manual.QueueAttack();
            if(!running)_manual.ClearAttack();
            // ★ 适配：准星始终对应真实相机中心射线，自身碰撞体由独立选取器排除。
            if(Camera.main!=null)_manual.SetAim(Camera.main.ViewportPointToRay(new Vector3(.5f,.5f,0)));
            _manual.SetInput(forward * y + Vector3.Cross(up, forward) * x, _acceptAttack && ControlBindings.Held(ControlAction.Attack));
            if (running && Input.GetKeyDown(jumpKey)) _jump.TryJump(_manual.CanMove);
        }
        public void SetCursorReleased(bool held){if(!_active||CursorReleased==held)return;CursorReleased=held;_manual?.SetInput(Vector3.zero,false);_manual?.ClearAttack();SetCaptured(!held);}
        private void OnGUI()
        {
            // ★ 修复（防Bug回归）：低帧率时按下/松开可能发生在同一帧，补收事件并锁存一次攻击。
            var evt=Event.current;var attack=ControlBindings.Get(ControlAction.Attack);
            if(_active && _acceptAttack && !GameInputGate.Blocked && Application.isFocused && (evt.type==EventType.MouseDown && evt.button==attackMouseButton || evt.type==EventType.KeyDown && evt.keyCode==attack))
                _manual?.QueueAttack();
        }
        private void SetCaptured(bool captured)
        {
            var desired = captured ? CursorLockMode.Locked : CursorLockMode.None;
            if (Cursor.lockState != desired)
            { Cursor.lockState = desired; if (captured) _captureFrame = Time.frameCount; }
            Cursor.visible = !captured;
        }
        // ★ 适配：暂停菜单释放鼠标但保留当前第一人称目标，返回时继续同一视角。
        public void Suspend(){_acceptAttack=false;_manual?.SetInput(Vector3.zero,false);_manual?.ClearAttack();SetCaptured(false);if(_crosshair!=null)_crosshair.SetActive(false);}
        public void End()
        {
            if (!_active) return;
            // ★ 修复（防Bug回归）：停用输入也清除待消费攻击，防止失焦/重新进入后补打一击。
            _active = false; _acceptAttack = false; _manual?.SetInput(Vector3.zero, false); _manual?.ClearAttack(); _jump?.Cancel();
            Cursor.lockState = _savedLock; Cursor.visible = _savedVisible;
            if (_crosshair != null) { _crosshair.SetActive(false); Destroy(_crosshair); _crosshair = null; }
        }
        private void OnApplicationFocus(bool focused)
        { if (_active && !focused) { SetCaptured(false); _acceptAttack = false; _manual?.SetInput(Vector3.zero, false); _manual?.ClearAttack(); } }
        private void OnDisable() => End();
    }
}
