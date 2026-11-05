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
        private Collider _ground;

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
        }

        void FixedUpdate()
        {
            // ★ 适配：附着寄生体由宿主跟随控制，不能向运动学刚体施力或清速度。
            if(_rb==null||_rb.isKinematic)return;

            // ★ 新增：如果游戏暂停，强制清除速度并停止物理计算
            if (ServiceLocator.TryGet<SimulationClock>(out var clock) && !clock.IsRunning)
            {
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
                return;
            }

            if (planet == null) return;

            var genes = GetComponent<EntityGeneController>();
            // ★ 适配：休眠实体不重复施力、射线检测和改写摩擦；碰撞唤醒或有效行动恢复后自动走完整路径。
            if(StationaryBodyRest.RemainsAsleep(_rb,genes))return;
            float moveSpeed = genes != null ? genes.GetAttribute("moveSpeed") : 0f;
            // ★ 修复（防Bug回归）：原地面静摩擦为 1，会吞掉每步较小的漫游速度，导致有行动能力仍原地不动；仅移动时降低本实体材质摩擦。
            if(_surfaceMaterial!=null)
            {
                var motion=DarwinPark.Entities.Logic.LocomotionSettings.Load();var radial=(transform.position-planet.position).normalized;
                bool moving=moveSpeed>0&&Vector3.ProjectOnPlane(_rb.linearVelocity,radial).sqrMagnitude>motion.frictionMotionThreshold*motion.frictionMotionThreshold;
                _surfaceMaterial.staticFriction=_surfaceMaterial.dynamicFriction=moving?motion.movingFriction:motion.stationaryFriction;
                _surfaceMaterial.frictionCombine=PhysicsMaterialCombine.Minimum;
            }
            bool isInWater = IsUnderWater(transform.position);

            // ★ 修复（防Bug回归）：行动基因不等于游泳，不能为所有入水实体开启游泳光效。
            // 游泳光效由对应能力负责，通用引力不创建灯光。

            Vector3 dirToCenter = planet.position - transform.position;
            float distance = dirToCenter.magnitude;
            dirToCenter.Normalize();

            // ★ 修复：引力不能无限随高度衰减。旧版 (R/d)^2 在 600 距离只剩 1/4，
            // 被攻击顶飞的实体越飞越慢、看起来“卡在天上”。设置下限并在高空补一个向心加速度。
            float gravityMultiplier = Mathf.Clamp(Mathf.Pow(planetRadius / Mathf.Max(distance, planetRadius), 2f), 0.45f, 1f);
            // ★ 修复（防Bug回归）：取消无条件的水中减重，否则陆生实体长时间悬在水面且达不到溺水深度。
            float finalGravity = baseGravity;
            float highAltitudeAssist = 0f;
            if (!isInWater && distance > planetRadius + 60f)
                highAltitudeAssist = Mathf.Min(20f, (distance - planetRadius - 60f) * 0.25f);
            _rb.AddForce(dirToCenter * (finalGravity * gravityMultiplier + highAltitudeAssist), ForceMode.Acceleration);

            // ★ 修复（防Bug回归）：入水清除陆地行动惯性，保留向内下沉；海底由真实地形碰撞体承托。
            if (isInWater)
            {
                _rb.linearVelocity=dirToCenter*Mathf.Max(0,Vector3.Dot(_rb.linearVelocity,dirToCenter));
            }

            // ★ 修复（防Bug回归）：实体头顶不是地面；只用星球碰撞体检测着地，避免层叠成员互相充当地表法线。
            if(_ground==null)_ground=planet.GetComponent<MeshCollider>();
            if (_ground!=null&&_ground.Raycast(new Ray(transform.position,dirToCenter),out RaycastHit hit,20f))
            {
                float radius = _col != null ? _col.bounds.extents.y : 1f;
                if (hit.distance <= radius + 0.5f)
                {
                    if (moveSpeed <= 0f)
                    {
                        Vector3 velocity = _rb.linearVelocity;
                        float vn = Vector3.Dot(velocity, hit.normal);
                        Vector3 tangentVelocity = velocity - hit.normal * vn;
                        if (tangentVelocity.sqrMagnitude > 0.01f) _rb.linearVelocity -= tangentVelocity * 0.9f;
                    }

                    Quaternion targetRot = Quaternion.FromToRotation(transform.up, hit.normal) * transform.rotation;
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.fixedDeltaTime * 15f);
                    StationaryBodyRest.TryRest(_rb,genes,_col,hit,planet);
                }
                else
                {
                    Quaternion targetRot = Quaternion.FromToRotation(transform.up, -dirToCenter) * transform.rotation;
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.fixedDeltaTime * 5f);
                }
            }
        }

        private bool IsUnderWater(Vector3 pos)
        {
            var builder = planet?.GetComponent<DarwinPark.Planet.PlanetTerrainMeshBuilder>();
            if (builder == null) return false;
            // ★ 修复（防Bug回归）：水位必须使用地形生成后的真实海平面 Data.SeaLevel，
            // 旧码使用 seaLevelRatio 估算，海陆比例变化后会把陆地误判为水下。
            return builder.IsSubmerged(pos);
        }
        private void OnDestroy(){if(_surfaceMaterial!=null)Destroy(_surfaceMaterial);}
    }
}
