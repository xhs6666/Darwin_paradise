using DarwinPark.Core;
using DarwinPark.Planet;
using UnityEngine;
namespace DarwinPark.Entities
{
    public readonly struct EntityGroundSample
    {
        public readonly Vector3 position,center,up,point,normal;public readonly float distance,altitude;public readonly bool hasSurface,underwater;public readonly MeshCollider ground;
        public EntityGroundSample(Vector3 position,Vector3 center,Vector3 up,Vector3 point,Vector3 normal,float distance,bool hasSurface,bool underwater,MeshCollider ground)
        {this.position=position;this.center=center;this.up=up;this.point=point;this.normal=normal;this.distance=distance;this.hasSurface=hasSurface;this.underwater=underwater;this.ground=ground;altitude=hasSurface?Vector3.Dot(position-point,up):0;}
    }
    // ★ 新增：重力、地表安全网和操控穿地修正共享同一物理位置的地形样本，传送/重建/缩放立即失效。
    public sealed class EntityGroundProbe
    {
        private readonly GroundSurfacePatch _patch=new();private float _next;private Vector3 _samplePosition;
        private readonly Rigidbody _body;private readonly GravityProbeCache _legacy=new();private Transform _planet;private PlanetTerrainMeshBuilder _builder;private MeshCollider _ground;private PlanetTerrainData _data;private Mesh _mesh;private Matrix4x4 _matrix;private Vector3 _position;private float _stamp=float.NaN,_radius,_height,_water;private EntityGroundSample _sample;private bool _enabled,_direct;
        public static long CrossStepReused;
        public static long DirectSamples,Raycasts,ReusedSamples,RotationWrites,VelocityWrites,SkippedWrites,Steps;
        public EntityGroundProbe(Rigidbody body){_body=body;}
        public EntityGroundSample Read(Transform planet)
        {
            using var performance=PerformanceTimings.Measure(PerformanceTimings.Area.GroundQuery);
            Steps++;if(_planet!=planet||_builder==null||_ground==null){_planet=planet;_builder=planet!=null?planet.GetComponent<PlanetTerrainMeshBuilder>():null;_ground=planet!=null?planet.GetComponent<MeshCollider>():null;_stamp=float.NaN;}
            var position=_body.position;if(planet==null)return default;var matrix=planet.localToWorldMatrix;var data=_builder?.Data;var mesh=_ground!=null?_ground.sharedMesh:null;float radius=_builder!=null?_builder.radius:0,height=_builder!=null?_builder.heightScale:0,water=_builder!=null?_builder.WaterRadius:0;bool enabled=_ground!=null&&_ground.enabled,direct=SimulationOptimization.Current.directGroundQueries;
            if(_stamp==Time.fixedTime&&position==_position&&matrix==_matrix&&ReferenceEquals(data,_data)&&mesh==_mesh&&radius==_radius&&height==_height&&water==_water&&enabled==_enabled&&direct==_direct){ReusedSamples++;return _sample;}
            var center=planet.position;var offset=position-center;float distance=offset.magnitude;var up=distance>0?offset/distance:Vector3.up;bool found=false;Vector3 point=default,normal=up;
            // ★ 修复（防Bug回归）：原先只缓存同一FixedUpdate，0.2秒降频未实际生效；复用限于真实三角形内，并核对所有地形身份。
            var tuning=SimulationOptimization.Current;
            bool unchanged=matrix==_matrix&&ReferenceEquals(data,_data)&&mesh==_mesh&&radius==_radius&&height==_height&&water==_water&&enabled==_enabled&&direct==_direct;
            bool reuse=unchanged&&direct&&_sample.hasSurface&&Time.fixedTime<_next&&(position-_samplePosition).sqrMagnitude<=tuning.groundProbeMoveTolerance*tuning.groundProbeMoveTolerance&&Mathf.Abs(Vector3.Dot(_body.linearVelocity,up))<=tuning.groundProbeVerticalSpeed;
            if(reuse&&_patch.TryRead(center,up,out point,out normal)){found=true;ReusedSamples++;CrossStepReused++;}
            else if(direct&&RadialTerrainSurface.TrySample(_builder,_ground,position,out var surface)){DirectSamples++;found=true;point=surface.point;normal=surface.normal;_patch.Capture(_builder,surface.triangle);_next=Time.fixedTime+tuning.groundProbeSeconds;_samplePosition=position;}
            else
            {
                // 自定义/不支持的地形沿原射线规则；高空或穿地安全网另从球外探测，不把无命中当成接地。
                if(_ground!=null&&enabled){Raycasts++;found=_ground.Raycast(new Ray(center+up*(_ground.bounds.extents.magnitude+distance),-up),out var hit,_ground.bounds.extents.magnitude+distance*2);if(found){point=hit.point;normal=hit.normal;}}
            }
            _position=position;_matrix=matrix;_data=data;_mesh=mesh;_radius=radius;_height=height;_water=water;_enabled=enabled;_direct=direct;_stamp=Time.fixedTime;
            return _sample=new EntityGroundSample(position,center,up,point,normal,distance,found,_builder!=null?_builder.IsSubmerged(position):distance<water,_ground);
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
