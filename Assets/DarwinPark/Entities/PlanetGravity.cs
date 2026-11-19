using DarwinPark.Core;
using UnityEngine;

namespace DarwinPark.Entities
{
    [RequireComponent(typeof(Rigidbody))]
    public class PlanetGravity : MonoBehaviour
    {
        public Transform planet;
        public float baseGravity = 15f;
        public float planetRadius = 300f;

        private Rigidbody _rb;
        private Collider _col;
        private PhysicsMaterial _surfaceMaterial;
        // ★ 适配：组件/地形与接地结果由独立缓存管理。
        private EntityGroundProbe _grounding;private EntityGeneController _genes;
        internal EntityManualControl ManualControl;
        internal bool ShoreBlocked;
        internal int BatchIndex = -1;
        internal bool HasDynamicBody => _rb != null && !_rb.isKinematic;
        public EntityGroundProbe Grounding=>_grounding??=new EntityGroundProbe(GetComponent<Rigidbody>());

        void Start()
        {
            if (planet == null) planet = GameObject.Find("Planet")?.transform;
            _rb = GetComponent<Rigidbody>();
            _col = GetComponent<Collider>();
            _rb.useGravity = false;
            _rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;

            // ★ 修复（防Bug回归）：通用引力从未启用游泳光效，不为每个静止实体额外创建GameObject和Light。

            if (_col != null)
            {
                PhysicsMaterial mat = new PhysicsMaterial("PlanetHighFriction");
                mat.dynamicFriction = 1.0f;
                mat.staticFriction = 1.0f;
                mat.bounciness = 0f;
                _col.material = mat;
                _surfaceMaterial=mat;
            }
            PlanetGravityBatch.Register(this);
        }

        private void OnEnable()
        {
            if (_rb != null) PlanetGravityBatch.Register(this);
        }

        private void OnDisable() => PlanetGravityBatch.Unregister(this);

        // ★ 适配：手动 Physics.Simulate 的入口在外部驱动物理步前显式调用。
        public void AdvancePhysicsStep()
        {
            bool paused = ServiceLocator.TryGet<SimulationClock>(out var clock) && !clock.IsRunning;
            AdvancePhysics(paused);
        }

        internal void AdvancePhysics(bool paused, PlanetGroundFrame? frame = null)
        {
            if (_genes == null) _genes = OptionalComponent.Read<EntityGeneController>(this);
            PlanetGroundPhysics.Advance(this, _rb, _col, _surfaceMaterial, _genes, Grounding, paused, frame);
        }

        private void OnDestroy(){if(_surfaceMaterial!=null)Destroy(_surfaceMaterial);}
    }
}
