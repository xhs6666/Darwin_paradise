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
        private Light _swimLight;

        void Start()
        {
            if (planet == null) planet = GameObject.Find("Planet")?.transform;
            _rb = GetComponent<Rigidbody>();
            _col = GetComponent<Collider>();
            _rb.useGravity = false;
            _rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;

            // ★ 初始化水下发光组件（默认关闭）
            var lightGO = new GameObject("SwimLight");
            lightGO.transform.SetParent(transform);
            lightGO.transform.localPosition = Vector3.zero;
            _swimLight = lightGO.AddComponent<Light>();
            _swimLight.type = LightType.Point;
            _swimLight.range = 10f;
            _swimLight.intensity = 2f;
            _swimLight.color = new Color(0.3f, 0.8f, 1f); // 青色光
            _swimLight.enabled = false;

            if (_col != null)
            {
                PhysicsMaterial mat = new PhysicsMaterial("PlanetHighFriction");
                mat.dynamicFriction = 1.0f;
                mat.staticFriction = 1.0f;
                mat.bounciness = 0f;
                _col.material = mat;
            }
        }

        void FixedUpdate()
        {

            // ★ 新增：如果游戏暂停，强制清除速度并停止物理计算
            if (ServiceLocator.TryGet<SimulationClock>(out var clock) && !clock.IsRunning)
            {
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
                return;
            }

            if (planet == null) return;

            var genes = GetComponent<EntityGeneController>();
            float moveSpeed = genes != null ? genes.GetAttribute("moveSpeed") : 0f;
            bool isInWater = IsUnderWater(transform.position);

            // 只要有行动基因，在水里就发光
            _swimLight.enabled = isInWater && moveSpeed > 0;

            Vector3 dirToCenter = planet.position - transform.position;
            float distance = dirToCenter.magnitude;
            dirToCenter.Normalize();

            // ★ 游泳逻辑：如果在水下，重力减小
            // ★ 修复：引力不能无限随高度衰减。旧版 (R/d)^2 在 600 距离只剩 1/4，
            // 被攻击顶飞的实体越飞越慢、看起来“卡在天上”。设置下限并在高空补一个向心加速度。
            float gravityMultiplier = Mathf.Clamp(Mathf.Pow(planetRadius / Mathf.Max(distance, planetRadius), 2f), 0.45f, 1f);
            float finalGravity = isInWater ? baseGravity * 0.1f : baseGravity; // 水中重力变小
            float highAltitudeAssist = 0f;
            if (!isInWater && distance > planetRadius + 60f)
                highAltitudeAssist = Mathf.Min(20f, (distance - planetRadius - 60f) * 0.25f);
            _rb.AddForce(dirToCenter * (finalGravity * gravityMultiplier + highAltitudeAssist), ForceMode.Acceleration);

            // 防止掉出海底
            if (isInWater)
            {
                float localDist = (transform.position - planet.position).magnitude;
                var builder = planet.GetComponent<DarwinPark.Planet.PlanetTerrainMeshBuilder>();
                if (builder != null)
                {
                    float seaFloor = builder.radius; // 最底部
                    if (localDist < seaFloor + 2f)
                    {
                        _rb.linearVelocity = Vector3.zero; // 停在海底
                    }
                }
            }

            if (Physics.Raycast(transform.position, dirToCenter, out RaycastHit hit, 20f))
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
    }
}