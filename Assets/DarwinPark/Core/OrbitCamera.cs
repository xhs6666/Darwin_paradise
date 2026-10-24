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

        [Header("聚焦")]
        [Tooltip("聚焦实体时，相机在实体地表之上额外保持的高度")]
        public float focusAltitude = 20f;

        [Header("速度控制")]
        public float baseRotateSpeed = 300f; // 基础旋转速度
        public float zoomSpeed = 2000f;      // 缩放速度

        private float x = 45f;
        private float y = 25f;
        private float targetDistance;
        private Quaternion targetRotation;
        private Vector3 targetPosition;
        private bool _isFocusing = false;

        public static bool IsDraggingUI = false;

        void Start()
        {
            if (target == null) target = GameObject.Find("Planet")?.transform;
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
            if (target == null) return;

            // ★ 修复：鼠标在 UI 上时不再旋转/缩放，右键收藏条目不会带动星球。
            bool pointerOverUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

            // 鼠标拖拽旋转
            if (!IsDraggingUI && !pointerOverUI && Input.GetMouseButton(1))
            {
                _isFocusing = false; // 打断聚焦动画

                // ★ 核心修复：根据当前距离计算灵敏度
                // 拉到最近(minDistance)时，灵敏度只有 5%；拉到最远时，灵敏度 100%
                float distRatio = Mathf.InverseLerp(minDistance, maxDistance, distance);
                float sensitivity = Mathf.Lerp(0.05f, 1.0f, distRatio);

                x += Input.GetAxis("Mouse X") * baseRotateSpeed * sensitivity * 0.02f;
                y -= Input.GetAxis("Mouse Y") * baseRotateSpeed * sensitivity * 0.02f;
                y = Mathf.Clamp(y, -85f, 85f);
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
                transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * 5f);

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
                UpdateCameraPos(false);
            }
            // ★ 修复（防Bug回归）：允许贴近地面观察，但按实际地形射线避免钻进山峰。
            KeepAboveSurface();
        }

        private void KeepAboveSurface()
        {
            var collider = target.GetComponent<MeshCollider>();
            if (collider == null) return;
            Vector3 direction = (transform.position - target.position).normalized;
            Vector3 outside = target.position + direction * maxDistance;
            if (collider.Raycast(new Ray(outside, -direction), out var hit, maxDistance))
            {
                float safe = Vector3.Distance(hit.point, target.position) + surfaceClearance;
                if (distance < safe)
                {
                    distance = safe; targetDistance = Mathf.Max(targetDistance, safe);
                    transform.position = target.position + direction * safe;
                }
            }
        }

        private void UpdateCameraPos(bool immediate)
        {
            if (target == null) return;
            Quaternion rotation = Quaternion.Euler(y, x, 0);
            Vector3 position = rotation * new Vector3(0.0f, 0.0f, -distance) + target.position;

            transform.rotation = rotation;
            transform.position = position;
        }

        // 平滑聚焦到目标实体
        public void FocusOn(Vector3 targetWorldPos)
        {
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
    }
}
