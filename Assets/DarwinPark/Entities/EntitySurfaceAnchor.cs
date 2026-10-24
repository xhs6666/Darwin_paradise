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
        private float _airTime;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
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
            if (ServiceLocator.TryGet<SimulationClock>(out var clock) && !clock.IsRunning) return;
            if (_rb == null || _rb.isKinematic) return;
            FindPlanet();
            if (_builder == null || _builder.Data == null) return;

            Vector3 center = _builder.transform.position;
            Vector3 worldOffset = transform.position - center;
            float distance = worldOffset.magnitude;
            if (distance < 1f) return;
            Vector3 radial = worldOffset / distance;
            // 地形数据在星球本地空间；星球旋转时用本地方向采样，避免高度查错。
            Vector3 localDir = _builder.transform.InverseTransformDirection(radial).normalized;

            // 水下实体不套用地表高度约束：海面到海床本来就超过 maxAltitude，
            // 否则会把正在游泳/下沉的实体硬拽到海底。溺水由 EntityEnvironmentHazard 处理。
            if (distance < _builder.WaterRadius)
            {
                _airTime = 0f;
                return;
            }

            float surfaceHeight = PlanetTerrainSampler.SampleHeight(_builder.Data, localDir);
            float surfaceRadius = _builder.radius + surfaceHeight * _builder.heightScale;
            float altitude = distance - surfaceRadius;

            // 掉进地形内部：推回地表，避免卡在山体里。
            if (altitude < -2f)
            {
                _rb.position = center + radial * (surfaceRadius + 0.5f);
                _rb.linearVelocity = Vector3.ProjectOnPlane(_rb.linearVelocity, radial);
                _airTime = 0f;
                return;
            }

            if (altitude <= maxAltitude)
            {
                _airTime = 0f;
                return;
            }

            _airTime += Time.fixedDeltaTime;

            Vector3 velocity = _rb.linearVelocity;
            float outward = Vector3.Dot(velocity, radial);
            if (altitude > hardAltitude && outward > 0f)
                velocity -= radial * outward;
            _rb.linearVelocity = velocity;

            if (altitude > maxAltitude)
            {
                float excess = altitude - maxAltitude;
                float pull = Mathf.Min(pullAcceleration, 4f + excess * 8f);
                // 只改径向分量，保留切向移动，表现上仍然在“跑”，只是不再升高。
                Vector3 radialVelocity = radial * Vector3.Dot(_rb.linearVelocity, radial);
                if (Vector3.Dot(radialVelocity, radial) > 0f)
                    _rb.linearVelocity -= radialVelocity;
                _rb.AddForce(-radial * pull, ForceMode.Acceleration);
            }

            // ★ 修复（防Bug回归）：12~45 米区间同样可能卡住；超时后恢复，极高处则立即救援。
            if (_airTime >= rescueDelay || altitude > rescueAltitude)
            {
                _rb.position = center + radial * (surfaceRadius + 1f);
                _rb.linearVelocity = Vector3.ProjectOnPlane(_rb.linearVelocity, radial) * 0.5f;
                _airTime = 0f;
            }
        }
    }
}
