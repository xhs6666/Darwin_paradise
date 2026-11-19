using DarwinPark.Core;
using DarwinPark.Planet;
using UnityEngine;
namespace DarwinPark.Entities
{
    public readonly struct EntityGroundSample
    {
        public readonly Vector3 position,center,up,point,normal;public readonly float distance,altitude,immersion;public readonly bool hasSurface,underwater;public readonly MeshCollider ground;
        public EntityGroundSample(Vector3 position,Vector3 center,Vector3 up,Vector3 point,Vector3 normal,float distance,bool hasSurface,bool underwater,MeshCollider ground,float immersion=0)
        {this.position=position;this.center=center;this.up=up;this.point=point;this.normal=normal;this.distance=distance;this.hasSurface=hasSurface;this.underwater=underwater;this.ground=ground;this.immersion=immersion;altitude=hasSurface?Vector3.Dot(position-point,up):0;}
    }
    // ★ 新增：重力、地表安全网和操控穿地修正共享同一物理位置的地形样本，传送/重建/缩放立即失效。
    public sealed class EntityGroundProbe
    {
        private readonly GroundSurfacePatch _patch=new();private float _next;private Vector3 _samplePosition;
        private readonly Rigidbody _body;private readonly GravityProbeCache _legacy=new();private Transform _planet;private PlanetTerrainMeshBuilder _builder;private MeshCollider _ground;private PlanetTerrainData _data;private Mesh _mesh;private Matrix4x4 _matrix;private Vector3 _position;private float _stamp=float.NaN,_radius,_height,_water;private EntityGroundSample _sample;private bool _enabled,_direct;
        public static long CrossStepReused;
        public static long DirectSamples,Raycasts,ReusedSamples,RotationWrites,VelocityWrites,SkippedWrites,Steps;
        internal PlanetTerrainMeshBuilder Builder => _builder;
        private float _shoreClearance=float.PositiveInfinity;
        internal bool NearWater(float distance)=>_sample.underwater||_shoreClearance<=distance;
        public EntityGroundProbe(Rigidbody body){_body=body;}
        internal EntityGroundSample ReadForAnchor(Transform planet)
        {
            // Late physics callers share this step's sample, but teleports and terrain changes invalidate it.
            if(_stamp==Time.fixedTime&&_planet==planet&&_body.position.Equals(_position)&&
                _builder!=null&&ReferenceEquals(_builder.Data,_data)&&_ground!=null&&
                _ground.sharedMesh==_mesh&&_ground.enabled==_enabled&&planet.localToWorldMatrix==_matrix)
            {ReusedSamples++;return _sample;}
            return Read(planet);
        }
        public EntityGroundSample Read(Transform planet)
            => planet == null ? default : Read(new PlanetGroundFrame(planet,SimulationOptimization.Current));

        internal EntityGroundSample Read(in PlanetGroundFrame frame)
        {
            using var performance=PerformanceTimings.Measure(PerformanceTimings.Area.GroundQuery);
            Steps++;
            var position=_body.position;
            bool unchanged=ReferenceEquals(_planet,frame.planet)&&ReferenceEquals(_builder,frame.builder)&&
                ReferenceEquals(_ground,frame.ground)&&ReferenceEquals(frame.data,_data)&&ReferenceEquals(frame.mesh,_mesh)&&
                frame.toWorld.Equals(_matrix)&&frame.radius==_radius&&frame.height==_height&&frame.water==_water&&
                frame.enabled==_enabled&&frame.direct==_direct;
            if(unchanged&&_stamp==frame.fixedTime&&position.Equals(_position))
            {
                ReusedSamples++;
                return _sample;
            }

            var offset=position-frame.center;
            float distance=offset.magnitude;
            Vector3 up=distance>0?offset/distance:Vector3.up;
            var local=frame.toLocal.MultiplyPoint3x4(position);
            float localRadius=local.magnitude;
            Vector3 direction=localRadius>1e-6f?local/localRadius:Vector3.up;
            float localWater=frame.data!=null
                ? frame.data.Navigation?.WaterRadius(direction)??PlanetCoordinate.WaterSurfaceRadius(frame.data,direction)
                : 0;
            bool found=false;
            Vector3 point=default,normal=up;
            var tuning=frame.options;
            // ★ 适配：近乎不动的身体继续复用当前三角形；每个物理步共享同一网格及坐标变换。
            bool reuse=unchanged&&frame.direct&&_sample.hasSurface&&frame.fixedTime<_next&&
                (position-_samplePosition).sqrMagnitude<=tuning.groundProbeMoveTolerance*tuning.groundProbeMoveTolerance&&
                Mathf.Abs(Vector3.Dot(_body.linearVelocity,up))<=tuning.groundProbeVerticalSpeed;
            if(reuse&&_patch.TryRead(frame.center,up,out point,out normal))
            {
                found=true;
                ReusedSamples++;
                CrossStepReused++;
            }
            else if(frame.direct&&RadialTerrainSurface.TrySampleLocal(frame.data,local,out var surface))
            {
                found=true;
                DirectSamples++;
                point=frame.toWorld.MultiplyPoint3x4(surface.point);
                normal=(frame.rotation*surface.normal).normalized;
                _patch.Capture(frame.data,frame.toWorld,frame.center,surface.triangle);
                _next=frame.fixedTime+tuning.groundProbeSeconds;
                _samplePosition=position;
            }
            else
            {
                _patch.Clear();
                // ★ 适配：自定义网格、非均匀缩放与无效三角形继续经过原射线路径。
                if(frame.enabled)
                {
                    Raycasts++;
                    var ray=new Ray(frame.center+up*(frame.boundsRadius+distance),-up);
                    found=frame.ground.Raycast(ray,out var hit,frame.boundsRadius+distance*2);
                    point=found?hit.point:default;
                    normal=found?hit.normal:up;
                }
            }

            _planet=frame.planet;
            _builder=frame.builder;
            _ground=frame.ground;
            _position=position;
            _matrix=frame.toWorld;
            _data=frame.data;
            _mesh=frame.mesh;
            _radius=frame.radius;
            _height=frame.height;
            _water=frame.water;
            _enabled=frame.enabled;
            _direct=frame.direct;
            _stamp=frame.fixedTime;
            float immersion=frame.data!=null?localWater-localRadius:frame.water-distance;
            _shoreClearance=found&&frame.data!=null
                ? frame.toLocal.MultiplyPoint3x4(point).magnitude-localWater
                : float.PositiveInfinity;
            return _sample=new EntityGroundSample(position,frame.center,up,point,normal,distance,found,immersion>0,frame.ground,immersion);
        }
        // ★ 适配：沿用原摩擦变更缓存，不再由多个组件各写一次相同材质。
        public void Friction(PhysicsMaterial material,float value)=>_legacy.Friction(material,value);
        public static void Velocity(Rigidbody body,Vector3 value)
        {float epsilon=SimulationOptimization.Current.groundVelocityEpsilon;var current=body.linearVelocity;if(value.sqrMagnitude==0?current.sqrMagnitude==0:(current-value).sqrMagnitude<=epsilon*epsilon){SkippedWrites++;return;}body.linearVelocity=value;VelocityWrites++;}
        public static void Align(Rigidbody body,Vector3 normal,float rate)
        {
            var rotation=body.rotation;var up=rotation*Vector3.up;float angle=SimulationOptimization.Current.groundNormalAngleDegrees;
            if(Vector3.Dot(up,normal)>=Mathf.Cos(angle*Mathf.Deg2Rad)){SkippedWrites++;return;}
            var target=Quaternion.FromToRotation(up,normal)*rotation;body.MoveRotation(Quaternion.Slerp(rotation,target,Time.fixedDeltaTime*rate));RotationWrites++;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]private static void Reset(){CrossStepReused=DirectSamples=Raycasts=ReusedSamples=RotationWrites=VelocityWrites=SkippedWrites=Steps=0;}
    }
}
