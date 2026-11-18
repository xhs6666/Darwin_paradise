using UnityEngine;
using DarwinPark.Core;
using DarwinPark.Planet;

namespace DarwinPark.Entities
{
    /// <summary>
    /// ★ 新增：实体地表约束（防“攻击飞上天/卡在天上”）。
    /// 为什么需要独立脚本：攻击追击、碰撞、地形陡坡都可能给实体一个向外的速度；
    /// 引力和追击逻辑一旦在某个高度衰减/失效，实体就会永久漂在空中。
    /// 这里每 FixedUpdate 做一次硬约束：超过允许离地高度就削掉向外速度并把实体拉回，
    /// 长时间卡在高空则直接救回地表。属于安全网，不依赖任何基因逻辑。
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [DefaultExecutionOrder(100)]
    public class EntitySurfaceAnchor : MonoBehaviour
    {
        [Header("约束")]
        [Tooltip("允许离开地表的正常高度（跳跃/出生下落用）")]
        public float maxAltitude = 12f;
        [Tooltip("超过该高度后，向外速度被清零")]
        public float hardAltitude = 20f;
        [Tooltip("高空滞留多久后强制救回地表")]
        public float rescueDelay = 2f;
        [Tooltip("超过该高度且滞留超时，直接传送回地表")]
        public float rescueAltitude = 45f;
        [Tooltip("向下拉回的最大加速度")]
        public float pullAcceleration = 40f;

        private Rigidbody _rb;
        private PlanetTerrainMeshBuilder _builder;
        private float _airTime;private PlanetGravity _gravity;private EntityGroundProbe _grounding;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _gravity=GetComponent<PlanetGravity>();_grounding=_gravity!=null?_gravity.Grounding:new EntityGroundProbe(_rb);
            FindPlanet();
        }

        private void FindPlanet()
        {
            if (_builder != null) return;
            var planet = GameObject.Find("Planet");
            if (planet != null) _builder = planet.GetComponent<PlanetTerrainMeshBuilder>();
        }

        private void FixedUpdate()
        {
            // ★ 适配：只在主动性能采样期间计时，定位完整场景的实际消耗。
            using var performance=DarwinPark.Core.PerformanceTimings.Measure(DarwinPark.Core.PerformanceTimings.Area.GroundAnchor);
            if (ServiceLocator.TryGet<SimulationClock>(out var clock) && !clock.IsRunning) return;
            if (_rb == null || _rb.isKinematic) return;
            // ★ 适配：已接触地表的静止刚体无需每个物理步重新采样高度；碰撞/行动唤醒后仍保留高空救援。
            if(StationaryBodyRest.IsResting(_rb))return;
            FindPlanet();
            if (_builder == null || _builder.Data == null) return;

            // ★ 适配：共享重力已定位的真实地面，不为同一实体再次执行双线性高度采样。
            PlanetGroundPhysics.AdvanceAnchor(this,_rb,_grounding.ReadForAnchor(_builder.transform),ref _airTime);
        }
    }
}
