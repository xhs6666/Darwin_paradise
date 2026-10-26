using DarwinPark.Core;
using DarwinPark.Entities.Logic;
using UnityEngine;

namespace DarwinPark.Entities
{
    /// <summary>★ 新增：玩家行动输入的独立物理适配；AI 让出移动，仍保留重力和地表安全约束。</summary>
    [RequireComponent(typeof(EntityGeneController), typeof(Rigidbody))]
    [DefaultExecutionOrder(50)]
    public sealed class EntityManualControl : MonoBehaviour
    {
        public bool IsControlled { get; private set; }
        public bool AttackHeld { get; private set; }
        public Vector3 MoveDirection { get; private set; }
        private Rigidbody _body;
        private EntityGeneController _genes;
        private Transform _planet;
        private EntityJumpMotor _jump;
        private Ray? _aimRay;
        private bool _attackQueued;
        public float aimRayDistance=200f;
        public float aimRadius=.35f;
        public void QueueAttack(){if(CanMove && IsControlled)_attackQueued=true;}
        public bool ConsumeAttack(){bool pending=_attackQueued;_attackQueued=false;return pending;}
        public void ClearAttack(){_attackQueued=false;AttackHeld=false;}
        public void SetAim(Ray ray){_aimRay=ray;}
        public DarwinEntity GetAttackTarget()=>_aimRay.HasValue?EntityAttackAim.Pick(gameObject,_aimRay.Value,aimRayDistance,aimRadius):null;
        public bool CanMove => _genes != null && _genes.GetAttribute("moveSpeed") > 0 &&
            _genes.Evaluation != null && _genes.Evaluation.Genes.Exists(g => g.IsActive && _genes.GetLogic(g.Gene) is LocomotionGeneLogic);
        private void Awake() { _body = GetComponent<Rigidbody>(); _genes = GetComponent<EntityGeneController>(); }
        public void BeginControl(Transform planet) { _planet = planet; _jump = GetComponent<EntityJumpMotor>(); IsControlled = true; SetInput(Vector3.zero, false); }
        public void SetInput(Vector3 direction, bool attack)
        {
            MoveDirection = Vector3.ClampMagnitude(direction, 1); AttackHeld = attack && CanMove;
        }
        public void Release()
        {
            SetInput(Vector3.zero, false);ClearAttack(); IsControlled = false; _aimRay=null;
            _jump?.Cancel();
            if (_body != null && !_body.isKinematic && _planet != null)
            {
                Vector3 up = (transform.position - _planet.position).normalized;
                _body.linearVelocity = up * Mathf.Min(0, Vector3.Dot(_body.linearVelocity,up));
            }
        }
        private void FixedUpdate()
        {
            if (!IsControlled || _planet == null || _body == null || _body.isKinematic) return;
            float scale = ServiceLocator.TryGet<SimulationClock>(out var clock) ? clock.TimeScale : 1;
            if (scale <= 0) return;
            Vector3 up = (transform.position - _planet.position).normalized;
            // ★ 修复（防Bug回归）：陆地行动基因不提供游泳能力，入水后保留下沉而停止切向推进。
            bool underwater=GetComponent<DarwinPark.Entities.Vitals.EntityEnvironmentHazard>()?.IsSubmerged == true;
            Vector3 direction = CanMove && !underwater ? Vector3.ProjectOnPlane(MoveDirection,up).normalized * MoveDirection.magnitude : Vector3.zero;
            // ★ 适配：保留经过接地验证的跳跃上升速度，其余向外冲量仍被防飞天约束清除。
            if (_jump == null) _jump = GetComponent<EntityJumpMotor>();
            if(underwater) _jump?.Cancel();
            float radial = Vector3.Dot(_body.linearVelocity,up);
            _body.linearVelocity = direction * (_genes.GetAttribute("moveSpeed") * scale) +
                up * (_jump != null ? _jump.RadialVelocity(up,radial) : Mathf.Min(0,radial));
        }
        private void OnDisable() => Release();
    }
}
