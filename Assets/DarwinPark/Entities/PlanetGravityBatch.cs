using System.Collections.Generic;
using DarwinPark.Core;
using DarwinPark.Planet;
using UnityEngine;

namespace DarwinPark.Entities
{
    // ★ 新增：一个物理回调按星球共享该步的不变数据，刚体接触及手控仍由原有物理入口处理。
    internal sealed class PlanetGravityBatch : MonoBehaviour
    {
        private static PlanetGravityBatch _instance;
        private readonly List<PlanetGravity> _bodies = new();
        private readonly Dictionary<Transform, PlanetGroundFrame> _frames = new();

        internal static void Register(PlanetGravity body)
        {
            if (_instance == null)
                _instance = new GameObject(nameof(PlanetGravityBatch)).AddComponent<PlanetGravityBatch>();
            if (body.BatchIndex >= 0 && body.BatchIndex < _instance._bodies.Count &&
                ReferenceEquals(_instance._bodies[body.BatchIndex], body)) return;
            body.BatchIndex = _instance._bodies.Count;
            _instance._bodies.Add(body);
        }

        internal static void Unregister(PlanetGravity body)
        {
            int index = body.BatchIndex;
            body.BatchIndex = -1;
            if (_instance == null) return;
            var bodies = _instance._bodies;
            if (index < 0 || index >= bodies.Count || !ReferenceEquals(bodies[index], body)) return;
            int last = bodies.Count - 1;
            if (index != last)
            {
                bodies[index] = bodies[last];
                bodies[index].BatchIndex = index;
            }
            bodies.RemoveAt(last);
        }

        private void FixedUpdate()
        {
            if (Physics.simulationMode == SimulationMode.Script) return;
            using var performance = PerformanceTimings.Measure(PerformanceTimings.Area.GravityBatch);
            _frames.Clear();
            var options = SimulationOptimization.Current;
            bool paused = ServiceLocator.TryGet<SimulationClock>(out var clock) && !clock.IsRunning;
            for (int index = 0; index < _bodies.Count; index++)
            {
                var body = _bodies[index];
                if (!body.HasDynamicBody) continue;
                var planet = body.planet;
                if (!options.sharePlanetPhysicsFrame || paused || planet == null)
                {
                    body.AdvancePhysics(paused);
                    continue;
                }

                if (!_frames.TryGetValue(planet, out var frame))
                {
                    frame = new PlanetGroundFrame(planet, options);
                    _frames.Add(planet, frame);
                }
                body.AdvancePhysics(paused, frame);
            }
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => _instance = null;
    }

    // ★ 新增：同一批次的坐标矩阵、物理网格和配置共用一次 Unity 读取，批次外仍可现场取样。
    internal readonly struct PlanetGroundFrame
    {
        internal readonly Transform planet;
        internal readonly PlanetTerrainMeshBuilder builder;
        internal readonly PlanetTerrainData data;
        internal readonly MeshCollider ground;
        internal readonly Mesh mesh;
        internal readonly Matrix4x4 toWorld, toLocal;
        internal readonly Quaternion rotation;
        internal readonly Vector3 center;
        internal readonly float radius, height, water, boundsRadius, fixedTime, localRadiusScale;
        internal readonly bool enabled, direct;
        internal readonly SimulationOptimization options;

        internal PlanetGroundFrame(Transform planet, SimulationOptimization options)
        {
            this.planet = planet;
            this.options = options;
            fixedTime = Time.fixedTime;
            builder = OptionalComponent.Read<PlanetTerrainMeshBuilder>(planet);
            ground = OptionalComponent.Read<MeshCollider>(planet);
            data = builder != null ? builder.Data : null;
            mesh = ground != null ? ground.sharedMesh : null;
            enabled = ground != null && ground.enabled;
            toWorld = planet.localToWorldMatrix;
            toLocal = planet.worldToLocalMatrix;
            rotation = planet.rotation;
            center = planet.position;
            radius = builder != null ? builder.radius : 0;
            height = builder != null ? builder.heightScale : 0;
            water = builder != null ? builder.WaterRadius : 0;
            boundsRadius = enabled ? ground.bounds.extents.magnitude : 0;
            var scale = planet.lossyScale;
            direct = options.directGroundQueries && enabled && data != null && mesh == builder.GeneratedMesh &&
                scale.x > 0 && Mathf.Abs(scale.x - scale.y) <= .00001f && Mathf.Abs(scale.x - scale.z) <= .00001f;
            // ★ 适配：使用 Gram 矩阵最大绝对行和限制逆变换的伸长，即使父对象链带来斜切也不会缩小身体范围。
            localRadiusScale=options.certifiedLandMotion&&direct?MaximumScale(toLocal):0;
        }

        private static float MaximumScale(in Matrix4x4 matrix)
        {
            var x=matrix.MultiplyVector(Vector3.right);
            var y=matrix.MultiplyVector(Vector3.up);
            var z=matrix.MultiplyVector(Vector3.forward);
            float xy=Mathf.Abs(Vector3.Dot(x,y)),xz=Mathf.Abs(Vector3.Dot(x,z)),yz=Mathf.Abs(Vector3.Dot(y,z));
            return Mathf.Sqrt(Mathf.Max(x.sqrMagnitude+xy+xz,
                Mathf.Max(y.sqrMagnitude+xy+yz,z.sqrMagnitude+xz+yz)))*1.00001f;
        }
    }
}
