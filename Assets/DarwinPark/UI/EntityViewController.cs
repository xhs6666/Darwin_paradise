using DarwinPark.Core;
using DarwinPark.Entities;
using DarwinPark.Entities.Stats;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DarwinPark.UI
{
    /// <summary>★ 新增：双击实体进入第三人称视角；输入、跟随和退出恢复独立于轨道相机。</summary>
    [DefaultExecutionOrder(200)]
    [RequireComponent(typeof(EntityPossessionInput))]
    public sealed class EntityViewController : MonoBehaviour
    {
        public static EntityViewController Instance { get; private set; }
        public DarwinEntity Target { get; private set; }
        public float doubleClickInterval = .35f;
        public float followDistance = 8, followHeight = 4, aimHeight = .5f;
        public float shoulderOffset=1.2f, aimForwardOffset=2f;
        public float minFollowDistance = 3, maxFollowDistance = 25, cameraClearance = .5f;
        public KeyCode exitKey = KeyCode.Escape;
        public LayerMask pickMask = ~0;
        private Camera _camera;
        private OrbitCamera _orbit;
        private EntityManualControl _manual;
        private Vector3 _savedPosition, _forward;
        private Quaternion _savedRotation;
        private Transform _planet;
        private MeshCollider _terrain;
        private float _distance, _height, _lastClickTime = -10;
        private DarwinEntity _lastClicked;
        private bool _active;
        private GameObject _bar;
        private Text _hint;
        private EntityPossessionInput _input;
        private void Awake() { Instance = this; _input = GetComponent<EntityPossessionInput>(); }
        private void Update()
        {
            if (_active && (Target == null || !Target.isActiveAndEnabled ||
                (Target.GetComponent<EntityStats>() is EntityStats stats && stats.TryGet("health",out float hp) && hp <= 0)))
            { ExitView(); return; }
            bool typing = IsTyping();
            if (_active && Input.GetKeyDown(exitKey)) { ExitView(); return; }
            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            bool placing = OrbitCamera.IsDraggingUI || (EntityPlacementTool.Instance != null && EntityPlacementTool.Instance.IsPlacing);
            if (!_active && !typing && !overUI && !placing && Input.GetMouseButtonDown(0))
            {
                var camera = Camera.main;
                if (camera != null && Physics.Raycast(camera.ScreenPointToRay(Input.mousePosition),out var hit,camera.farClipPlane,pickMask,QueryTriggerInteraction.Ignore))
                    RegisterClick(hit.collider.GetComponentInParent<DarwinEntity>(),Time.unscaledTime);
                else RegisterClick(null,Time.unscaledTime);
            }
            if (!_active) return;
            // ★ 适配：捕获鼠标后左键只攻击，准星不参与 UI 射线，世界双击拾取不再抢占。
            bool blocked = typing || placing;
            Vector3 up = (Target.transform.position-_planet.position).normalized;
            _forward = Vector3.ProjectOnPlane(_forward,up).normalized;
            if (_forward.sqrMagnitude < .01f) _forward = Vector3.ProjectOnPlane(Vector3.forward,up).normalized;
            _input.Tick(blocked,up,ref _forward,ref _height,ref _distance,minFollowDistance,maxFollowDistance);
            if (_hint != null) _hint.text = Target.DisplayName + (_manual.CanMove ? "  WASD 移动 · 左键攻击 · 空格跳跃 · 鼠标转向 · Esc 返回" : "  观察模式（无有效行动基因）· Esc 返回");
        }
        // ★ 新增：同一实体的双击才切换，UI/空白点击不会沿用上一次命中。
        public void RegisterClick(DarwinEntity entity, float time)
        {
            bool twice = entity != null && entity == _lastClicked && time-_lastClickTime <= doubleClickInterval && time >= _lastClickTime;
            _lastClicked = entity; _lastClickTime = time;
            if (twice) { _lastClicked = null; EnterView(entity); }
        }
        public bool EnterView(DarwinEntity entity)
        {
            if (entity == null || !entity.isActiveAndEnabled) return false;
            var camera = Camera.main; var orbit = camera != null ? camera.GetComponent<OrbitCamera>() : null;
            if (orbit == null || orbit.target == null) { Log.Error(Log.Tag.UI,"实体视角需要主相机与星球轨道目标"); return false; }
            if (entity.GetComponent<Rigidbody>() == null || entity.GetComponent<EntityGeneController>() == null) return false;
            if (_active) ExitView();
            EntityPlacementTool.Instance?.CancelPlacement();
            _camera = camera; _orbit = orbit; _planet = orbit.target; _terrain = _planet.GetComponent<MeshCollider>();
            _savedPosition = camera.transform.position; _savedRotation = camera.transform.rotation;
            _forward = Vector3.ProjectOnPlane(camera.transform.up,(entity.transform.position-_planet.position).normalized).normalized;
            _distance = followDistance; _height = followHeight;
            Target = entity; _manual = entity.GetComponent<EntityManualControl>() ?? entity.gameObject.AddComponent<EntityManualControl>();
            _manual.BeginControl(_planet); _active = true; _orbit.EntityViewActive = true;
            // ★ 适配：清理旧输入框焦点，进入时立即锁鼠标并生成准星。
            EventSystem.current?.SetSelectedGameObject(null);
            _input.Begin(_manual,_planet,_height,_distance);
            BuildBar(); return true;
        }
        private void LateUpdate()
        {
            if (!_active || Target == null || _camera == null || _planet == null) return;
            Vector3 up = (Target.transform.position-_planet.position).normalized;
            Vector3 forward = Vector3.ProjectOnPlane(_forward,up).normalized;
            // ★ 修复（防Bug回归）：镜头偏肩、准星前移，避免自身模型长期挡在准星中央。
            Vector3 aim = Target.transform.position+up*aimHeight+forward*aimForwardOffset;
            Vector3 desired = Target.transform.position-forward*_distance+up*_height+Vector3.Cross(up,forward)*shoulderOffset;
            // ★ 修复（防Bug回归）：跟随镜头也要对真实地形做遮挡检测，不能钻入山体。
            Vector3 offset = desired-aim;
            if (_terrain != null && _terrain.Raycast(new Ray(aim,offset.normalized),out var hit,offset.magnitude))
                desired = hit.point+hit.normal*cameraClearance;
            _camera.transform.SetPositionAndRotation(desired,Quaternion.LookRotation(aim-desired,up));
        }
        public void ExitView()
        {
            if (!_active) return;
            _active = false;
            _input?.End();
            if (_manual != null) _manual.Release();
            if (_orbit != null) _orbit.EntityViewActive = false;
            // ★ 修复（防Bug回归）：操控后实体已经离开出生处，退出应对准其当前位置，不能恢复旧镜头而丢失实体。
            if (_orbit != null && Target != null) _orbit.ReturnToEntity(Target.transform.position);
            else if (_camera != null) _camera.transform.SetPositionAndRotation(_savedPosition,_savedRotation);
            Target = null; _manual = null; _lastClicked = null;
            if (_bar != null) { _bar.SetActive(false); Destroy(_bar); _bar = null; }
        }
        private void BuildBar()
        {
            if (!ServiceLocator.TryGet<UIRoot>(out var root) || root.MainCanvas == null) return;
            _bar = UIFactory.NewRect("EntityViewBar",root.MainCanvas.transform,typeof(Image));
            _bar.GetComponent<Image>().color = new Color(.04f,.09f,.14f,.9f);
            _bar.GetComponent<Image>().raycastTarget = false;
            var rt = _bar.GetComponent<RectTransform>(); rt.anchorMin = rt.anchorMax = new Vector2(.5f,0);
            rt.pivot = new Vector2(.5f,0); rt.anchoredPosition = new Vector2(0,220); rt.sizeDelta = new Vector2(690,32);
            _hint = UIFactory.NewText("Hint",_bar.transform,"实体视角",13);
            UIFactory.Stretch(_hint.rectTransform,8,2); _hint.rectTransform.offsetMax = new Vector2(-95,-2);
            var exit = UIFactory.NewButton("Exit",_bar.transform,"返回星球",ExitView,new Vector2(85,26));
            exit.GetComponentInChildren<Text>().fontSize = 13;
            var ert = exit.GetComponent<RectTransform>(); ert.anchorMin = ert.anchorMax = new Vector2(1,.5f);
            ert.pivot = new Vector2(1,.5f); ert.anchoredPosition = new Vector2(-3,0);
        }
        private static bool IsTyping()
        {
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            return selected != null && selected.GetComponentInParent<InputField>() is InputField input && input.isFocused;
        }
        private void OnDisable() => ExitView();
        private void OnDestroy() { ExitView(); if (Instance == this) Instance = null; }
    }
}
