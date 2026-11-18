using UnityEngine;
using UnityEngine.EventSystems;

namespace DarwinPark.Core
{
    public class OrbitCamera : MonoBehaviour
    {
        public Transform target;
        public float distance = 800f;
        public float maxDistance = 3000f;
        [Tooltip("最小距离必须大于“星球半径 + 最大地形高度”，否则相机会钻进山峰")]
        public float minDistance = 305f;
        [Header("近距离观察")]
        public float surfaceClearance = 3f;
        // ★ 适配：独立海面生成后下发真实半径，海床不能成为轨道相机的最近边界。
        [HideInInspector] public float minimumSurfaceRadius;

        [Header("聚焦")]
        [Tooltip("聚焦实体时，相机在实体地表之上额外保持的高度")]
        public float focusAltitude = 20f;

        [Header("速度控制")]
        public float baseRotateSpeed = 300f; // 基础旋转速度
        public float zoomSpeed = 2000f;      // 缩放速度
        [Header("左键拖动")]
        public float dragSpeedMultiplier=.2f, shiftDragMultiplier=4f, dragThresholdPixels=6f;
        private bool _leftDragStarted;
        private Vector2 _leftDragOrigin;
        [Header("Alt + 右键观察方向")]
        public float lookDegreesPerPixel=0.22f, autoStraightenAltitude=150f, straightenSpeed=6f;
        private bool _looking,_lookAdjusted;
        private Vector2 _lookPointer;
        private Quaternion _orbitRotation,_lookOffset=Quaternion.identity;
        public bool HasLookOffset=>_lookAdjusted;

        private float x = 45f;
        private float y = 25f;
        private float targetDistance;
        private Quaternion targetRotation;
        private Vector3 targetPosition;
        private bool _isFocusing = false;

        public static bool IsDraggingUI = false;
        // ★ 适配：实体跟随组件接管镜头时，保留轨道状态但停止本组件写相机位置。
        public bool EntityViewActive { get; set; }

        // ★ 新增：小地图直接改变轨道朝向并同步缓存，保留缩放距离，下一帧不会跳回拖动前的位置。
        public bool SetOrbitDirection(Vector3 outward)
        {
            if(target==null||EntityViewActive||outward.sqrMagnitude<1e-8f||!float.IsFinite(outward.x)||!float.IsFinite(outward.y)||!float.IsFinite(outward.z))return false;
            // ★ 修复（防Bug回归）：沿用当前镜头的上方向，不在极点强制重建世界向上造成翻转或死锁。
            var forward=-outward.normalized;var up=Vector3.ProjectOnPlane(transform.up,forward);
            if(up.sqrMagnitude<1e-8f)up=Vector3.ProjectOnPlane(transform.right,forward);
            return SetOrbitRotation(Quaternion.LookRotation(forward,up));
        }
        public bool SetOrbitRotation(Quaternion rotation)
        {
            float norm=Quaternion.Dot(rotation,rotation);
            if(target==null||EntityViewActive||!float.IsFinite(norm)||norm<1e-8f)return false;
            ClearLook();transform.rotation=rotation.normalized;SyncAngles();
            distance=targetDistance=Mathf.Max(Vector3.Distance(transform.position,target.position),minDistance);
            _isFocusing=false;_leftDragStarted=false;UpdateCameraPos(true);KeepAboveSurface();return true;
        }
        // ★ 修复（防Bug回归）：以当前镜头局部轴连续旋转，不把俯仰夹在±85度；到达南北极后仍能继续或反向拖动。
        public bool RotateOrbit(float yawDegrees,float pitchDegrees)
        {
            if(target==null||EntityViewActive||!float.IsFinite(yawDegrees)||!float.IsFinite(pitchDegrees))return false;
            _isFocusing=false;
            // ★ 新增：轨道拖动与原地观察分别保存，转动观察方向不会改变相机所在的地表位置。
            if(_lookAdjusted){_orbitRotation=(_orbitRotation*Quaternion.Euler(pitchDegrees,yawDegrees,0)).normalized;transform.rotation=_orbitRotation*_lookOffset;}
            else transform.rotation=(transform.rotation*Quaternion.Euler(pitchDegrees,yawDegrees,0)).normalized;
            SyncAngles();UpdateCameraPos(true);KeepAboveSurface();return true;
        }
        private void SyncAngles(){x=transform.rotation.eulerAngles.y;y=Mathf.DeltaAngle(0,transform.rotation.eulerAngles.x);}

        // ★ 适配：读档同步轨道缓存，恢复位置后不会在下一帧跳回旧镜头。
        public void RestoreView(Vector3 position,Quaternion rotation,float savedDistance)
        {
            ClearLook();transform.SetPositionAndRotation(position,rotation);distance=targetDistance=savedDistance;x=rotation.eulerAngles.y;y=Mathf.DeltaAngle(0,rotation.eulerAngles.x);_isFocusing=false;
            // ★ 修复（防Bug回归）：保存时可能处于原地观察，读档仍按实际径向位置恢复轨道，不能把偏转的视线当作轨道位置。
            if(target!=null&&(position-target.position).sqrMagnitude>1e-8f)
            {var forward=(target.position-position).normalized;var up=Vector3.ProjectOnPlane(rotation*Vector3.up,forward);if(up.sqrMagnitude<1e-8f)up=Vector3.ProjectOnPlane(rotation*Vector3.right,forward);_orbitRotation=Quaternion.LookRotation(forward,up);_lookOffset=Quaternion.Inverse(_orbitRotation)*rotation;_lookAdjusted=Quaternion.Angle(_lookOffset,Quaternion.identity)>.05f;}
        }
        private void ClearLook(){_looking=_lookAdjusted=false;_lookOffset=Quaternion.identity;}
        public bool RotateLook(float yawDegrees,float pitchDegrees)
        {
            if(target==null||EntityViewActive||!float.IsFinite(yawDegrees)||!float.IsFinite(pitchDegrees))return false;
            if(!_lookAdjusted){_orbitRotation=transform.rotation;_lookAdjusted=true;}
            _isFocusing=false;_lookOffset=(_lookOffset*Quaternion.Euler(pitchDegrees,yawDegrees,0)).normalized;transform.rotation=_orbitRotation*_lookOffset;return true;
        }
        public void StraightenLook(float deltaTime)
        {
            if(!_lookAdjusted)return;
            _lookOffset=Quaternion.Slerp(_lookOffset,Quaternion.identity,1-Mathf.Exp(-straightenSpeed*Mathf.Max(0,deltaTime)));
            transform.rotation=_orbitRotation*_lookOffset;
            if(Quaternion.Angle(_lookOffset,Quaternion.identity)<.1f){transform.rotation=_orbitRotation;ClearLook();}
        }
        void Start()
        {
            if (target == null) target = GameObject.Find("Planet")?.transform;
            if(targetDistance<=0)transform.rotation=Quaternion.Euler(y,x,0);
            targetDistance = distance;
            UpdateCameraPos(true);

            // ★ 核心修复：强制相机能看到极远处的星空
            var cam = GetComponent<Camera>();
            if (cam != null)
            {
                cam.farClipPlane = 10000f; // 提高到 10000
            }
        }

        void LateUpdate()
        {
            // ★ 适配：覆盖菜单拥有输入时不处理世界操作。
            if (DarwinPark.Core.GameInputGate.Blocked||EntityViewActive){_looking=_leftDragStarted=false;return;}
            if (target == null) return;

            // ★ 修复：鼠标在 UI 上时不再旋转/缩放，右键收藏条目不会带动星球。
            bool pointerOverUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            // ★ 新增：仅世界内按下的 Alt+右键启动原地观察；松键、失焦或覆盖面板立即释放。
            bool alt=Input.GetKey(KeyCode.LeftAlt)||Input.GetKey(KeyCode.RightAlt);
            if(Input.GetMouseButtonDown(1)&&alt&&!pointerOverUI&&!IsDraggingUI){_looking=true;_lookPointer=Input.mousePosition;_leftDragStarted=false;}
            if(!alt||!Input.GetMouseButton(1)||IsDraggingUI)_looking=false;
            if(_looking&&!pointerOverUI){Vector2 pointer=Input.mousePosition;Vector2 delta=pointer-_lookPointer;_lookPointer=pointer;RotateLook(delta.x*lookDegreesPerPixel,-delta.y*lookDegreesPerPixel);}

            // ★ 修复（防Bug回归）：仅从世界开始的左键拖动旋转；点击和 UI 拖动不抢占相机。
            // ★ 适配：RTS左键属于框选，改用中键旋转，避免选框同时拖动相机。
            int dragButton=WorldAuthority.RtsMode?2:0;
            if(Input.GetMouseButtonDown(dragButton))
            {_leftDragStarted=!IsDraggingUI && !pointerOverUI;_leftDragOrigin=Input.mousePosition;}
            if(Input.GetMouseButtonUp(dragButton) || IsDraggingUI) _leftDragStarted=false;
            if (!_looking && _leftDragStarted && !pointerOverUI && Input.GetMouseButton(dragButton) &&
                Vector2.Distance(_leftDragOrigin,(Vector2)Input.mousePosition)>dragThresholdPixels)
            {
                _isFocusing = false; // 打断聚焦动画

                // ★ 核心修复：根据当前距离计算灵敏度
                // 拉到最近(minDistance)时，灵敏度只有 5%；拉到最远时，灵敏度 100%
                float distRatio = Mathf.InverseLerp(minDistance, maxDistance, distance);
                float sensitivity = Mathf.Lerp(0.05f, 1.0f, distRatio);

                float multiplier=dragSpeedMultiplier*((Input.GetKey(KeyCode.LeftShift)||Input.GetKey(KeyCode.RightShift))?shiftDragMultiplier:1);
                RotateOrbit(Input.GetAxis("Mouse X")*baseRotateSpeed*sensitivity*.02f*multiplier,
                    -Input.GetAxis("Mouse Y")*baseRotateSpeed*sensitivity*.02f*multiplier);
            }

            // ★ 修复（防Bug回归）：投放/拖拽期间同样屏蔽缩放，避免瞄准地表时相机被滚轮带走。
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (!IsDraggingUI && !pointerOverUI && Mathf.Abs(scroll) > 0.001f)
            {
                _isFocusing = false;
                targetDistance -= scroll * zoomSpeed;
                targetDistance = Mathf.Clamp(targetDistance, minDistance, maxDistance);
            }

            // 平滑过渡逻辑
            if (_isFocusing)
            {
                // 聚焦飞行逻辑
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 5f);
                // ★ 修复（防Bug回归）：对侧聚焦不能沿穿过星球的直线飞行，按旋转圆弧和半径插值。
                float focusRadius = Mathf.Lerp(Vector3.Distance(transform.position,target.position),
                    Vector3.Distance(targetPosition,target.position),Time.deltaTime*5f);
                transform.position = target.position + transform.rotation * Vector3.back * focusRadius;

                Vector3 euler = transform.rotation.eulerAngles;
                x = euler.y;
                y = euler.x > 180 ? euler.x - 360 : euler.x;
                distance = Vector3.Distance(transform.position, target.position);
                targetDistance = distance;

                if (Vector3.Distance(transform.position, targetPosition) < 0.5f) _isFocusing = false;
            }
            else
            {
                distance = Mathf.Lerp(distance, targetDistance, Time.deltaTime * 8f);
                if(!_looking&&distance>=Mathf.Max(minDistance,minimumSurfaceRadius)+autoStraightenAltitude)StraightenLook(Time.unscaledDeltaTime);
                UpdateCameraPos(false);
            }
            // ★ 修复（防Bug回归）：允许贴近地面观察，但按实际地形射线避免钻进山峰。
            KeepAboveSurface();
        }

        private void KeepAboveSurface()
        {
            // ★ 适配：镜头地表检测独立计时，不与实体接地混算。
            using var performance=PerformanceTimings.Measure(PerformanceTimings.Area.CameraGround);
            if (target == null) return;
            var collider = target.GetComponent<MeshCollider>();
            Vector3 direction = (transform.position - target.position).normalized;
            if(direction.sqrMagnitude<.5f) direction=-(transform.forward);
            float safe = Mathf.Max(minDistance,minimumSurfaceRadius+surfaceClearance);
            // ★ 修复（防Bug回归）：探测起点必须在整个碰撞体外，且检查实际位置而非过渡中的缓存 distance。
            if(collider != null)
            {
                float outsideRadius=Mathf.Max(maxDistance,collider.bounds.extents.magnitude+surfaceClearance);
                Vector3 outside=target.position+direction*outsideRadius;
                if(collider.Raycast(new Ray(outside,-direction),out var hit,outsideRadius))
                    safe=Mathf.Max(safe,Vector3.Distance(hit.point,target.position)+surfaceClearance);
            }
            // ★ 修复（防Bug回归）：轨道写回的长度有浮点舍入，不能每次旋转都累加到距离缓存；明显的外部位移仍按实际位置恢复。
            float actual=Vector3.Distance(transform.position,target.position);
            if(Mathf.Abs(actual-distance)>Mathf.Max(1,distance)*1e-6f)distance=actual;
            distance=Mathf.Max(distance,safe);
            targetDistance=Mathf.Max(targetDistance,safe);
            transform.position=target.position+direction*distance;
        }

        private void UpdateCameraPos(bool immediate)
        {
            if (target == null) return;
            // ★ 修复（防Bug回归）：真实四元数姿态是轨道状态，不能每帧从欧拉角重建并丢失过极点后的方向。
            Quaternion rotation = _lookAdjusted?_orbitRotation:transform.rotation;
            Vector3 position = rotation * new Vector3(0.0f, 0.0f, -distance) + target.position;

            transform.rotation = _lookAdjusted?rotation*_lookOffset:rotation;
            transform.position = position;
        }

        // ★ 修复（防Bug回归）：退出操控需要同步轨道角度和距离，否则下一帧会被旧轨道状态拉回原处。
        public void ReturnToEntity(Vector3 worldPosition)
        {
            FocusOn(worldPosition);
            if(target == null) return;
            transform.SetPositionAndRotation(targetPosition,targetRotation);
            Vector3 euler=targetRotation.eulerAngles;
            x=euler.y; y=euler.x>180?euler.x-360:euler.x;
            distance=targetDistance=Vector3.Distance(targetPosition,target.position);
            _isFocusing=false;
            KeepAboveSurface();
        }

        // 平滑聚焦到目标实体
        public void FocusOn(Vector3 targetWorldPos)
        {
            ClearLook();
            if (target == null) target = GameObject.Find("Planet")?.transform;
            if (target == null) return;

            Vector3 center = target.position;
            Vector3 dirFromCenter = (targetWorldPos - center);
            if (dirFromCenter.sqrMagnitude < 1e-6f) return;
            dirFromCenter.Normalize();

            // ★ 修复（防Bug回归）：旧版把相机放到目标点的反方向，而且距离固定 180，
            // 小于星球半径 300，会直接钻进星球内部，表现为“定位不准/看不到实体”。
            // 现在沿实体所在球面法线向外放相机，并保证距离大于地表半径。
            Vector3 up = Mathf.Abs(Vector3.Dot(dirFromCenter, Vector3.up)) > 0.99f
                ? Vector3.right
                : Vector3.up;

            float surfaceRadius = Vector3.Distance(targetWorldPos, center);
            float targetDist = Mathf.Clamp(surfaceRadius + focusAltitude, minDistance, maxDistance);

            targetRotation = Quaternion.LookRotation(-dirFromCenter, up);
            targetPosition = center + dirFromCenter * targetDist;
            targetDistance = targetDist;

            _isFocusing = true;
        }
        private void OnApplicationFocus(bool focused){if(!focused)_looking=_leftDragStarted=false;}
    }
}
