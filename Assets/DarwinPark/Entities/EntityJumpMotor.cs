using DarwinPark.Core;
using UnityEngine;

namespace DarwinPark.Entities
{
    /// <summary>★ 新增：只允许真实地形接地时跳跃，独立管理合法的向外速度，避免旧防飞天约束截断跳跃。</summary>
    [RequireComponent(typeof(Rigidbody), typeof(Collider))]
    [DefaultExecutionOrder(60)]
    public sealed class EntityJumpMotor : MonoBehaviour
    {
        public float jumpSpeed = 6f, groundTolerance = .18f;
        [Range(0f, 1f)] public float minimumGroundDot = .45f;
        public bool IsJumping { get; private set; }
        private Rigidbody _body;
        private Collider _collider;
        private Transform _planet;
        private MeshCollider _ground;
        private EntityManualControl _manual;
        private void Awake() { _body = GetComponent<Rigidbody>(); _collider = GetComponent<Collider>(); _manual = GetComponent<EntityManualControl>(); }
        public void Bind(Transform planet)
        { _planet = planet; _ground = planet != null ? planet.GetComponent<MeshCollider>() : null; IsJumping = false; }
        public bool TryJump(bool allowed)
        {
            if (!allowed || IsJumping || _body.isKinematic || _planet == null || _ground == null) return false;
            // ★ 修复（防Bug回归）：海底接地不能成为陆地跳跃/连续上浮的发力点。
            if(GetComponent<DarwinPark.Entities.Vitals.EntityEnvironmentHazard>()?.IsSubmerged == true) return false;
            float scale = ServiceLocator.TryGet<SimulationClock>(out var clock) ? clock.TimeScale : 1;
            if (scale <= 0) return false;
            Vector3 up = (transform.position - _planet.position).normalized;
            var bounds = _collider.bounds;
            float support = Vector3.Dot(bounds.extents, new Vector3(Mathf.Abs(up.x), Mathf.Abs(up.y), Mathf.Abs(up.z)));
            // 只检测星球碰撞体，实体或触发器不能成为空中二段跳的落脚点。
            if (!_ground.Raycast(new Ray(bounds.center, -up), out var hit, support + groundTolerance) ||
                Vector3.Dot(hit.normal, up) < minimumGroundDot) return false;
            // ★ 修复（防Bug回归）：现有星球重力使用物理秒，跳跃冲量保持同一单位，倍率不能把小人射过高空约束。
            _body.linearVelocity = Vector3.ProjectOnPlane(_body.linearVelocity, up) + up * jumpSpeed;
            IsJumping = true;
            return true;
        }
        public float RadialVelocity(Vector3 up, float velocity)
        {
            if (IsJumping && velocity <= 0) IsJumping = false;
            return IsJumping ? velocity : Mathf.Min(0, velocity);
        }
        public void Cancel() { IsJumping = false; }
        private void FixedUpdate()
        {
            if (_manual == null || !_manual.IsControlled || _body.isKinematic) return;
            if (ServiceLocator.TryGet<SimulationClock>(out var clock) && !clock.IsRunning) return;
            ResolveGroundContact();
        }
        // ★ 修复（防Bug回归）：密集曲面上的切向速度控制可能覆盖接触解算，用真实碰撞表面兜底防止操控实体穿地溺水。
        public bool ResolveGroundContact()
        {
            if (_planet == null || _ground == null) return false;
            Vector3 up = (_body.position - _planet.position).normalized;
            float outer = _ground.bounds.extents.magnitude + groundTolerance;
            if (!_ground.Raycast(new Ray(_planet.position + up * outer, -up), out var hit, outer * 2)) return false;
            var bounds = _collider.bounds;
            float support = Vector3.Dot(bounds.extents, new Vector3(Mathf.Abs(up.x), Mathf.Abs(up.y), Mathf.Abs(up.z)));
            float gap = Vector3.Dot(bounds.center-hit.point,up)-support;
            if (gap >= 0) return false;
            _body.position += up * (-gap + Physics.defaultContactOffset);
            float radial = Vector3.Dot(_body.linearVelocity,up);
            if (radial < 0) { _body.linearVelocity -= up * radial; IsJumping = false; }
            return true;
        }
        private void OnDisable() => Cancel();
    }
}
