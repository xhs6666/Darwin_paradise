using DarwinPark.Genetics;
using DarwinPark.Core;
using DarwinPark.Entities.Simulation;
using DarwinPark.Entities.Ecology;
using DarwinPark.Planet;
using UnityEngine;
namespace DarwinPark.Entities.Logic
{
    // ★ 新增：行动执行中枢指令，集中处理运动模式、路径、转向与物理速度；不自行选择食物或感知目标。
    public class LocomotionGeneLogic : IGeneLogic
    {
        private EntityGeneController _controller;private Rigidbody _rb;private Collider _collider;private PlanetGravity _gravity;private PlanetTerrainMeshBuilder _planet;private LocomotionSettings _settings;private readonly LandRoutePlan _landRoute=new();
        public virtual void Initialize(EntityGeneController controller,GeneInstance gene){_controller=controller;_rb=controller.GetComponent<Rigidbody>();_collider=controller.GetComponent<Collider>();_gravity=controller.GetComponent<PlanetGravity>();_planet=Object.FindFirstObjectByType<PlanetTerrainMeshBuilder>();_settings=LocomotionSettings.Load();}
        public void OnUpdate(float dt){}
        internal Vector3 WanderDestination(Vector3 heading,float distance)
        {
            Vector3 current=_rb!=null?_rb.position:_controller.transform.position;
            if(_planet==null)return current;
            var transform=_planet.transform;
            var radial=(current-transform.position).normalized;
            var tangent=Vector3.ProjectOnPlane(heading,radial).normalized;
            var graph=_planet.Data?.Navigation;
            if(graph==null||_controller.GetAttribute("swimSpeed")>0)return current+tangent*distance;
            var start=PlanetTerrainSampler.WorldToLocalDirection(transform,current);
            float reach=Mathf.Min(distance,graph.Settings.waypointArrival*3.5f);
            for(int attempt=0;attempt<5;attempt++)
            {
                var offset=Quaternion.AngleAxis(attempt*137.5f,radial)*tangent*(reach*(1-attempt*.15f));
                var goal=PlanetTerrainSampler.WorldToLocalDirection(transform,current+offset);
                if(graph.Segment(start,goal))
                    return transform.TransformPoint(goal*PlanetCoordinate.SurfaceRadius(graph.Terrain,goal));
            }
            return current;
        }
        public void Move(Vector3 position, float speed, float dt)
        {
            if(_rb==null)_rb=_controller.GetComponent<Rigidbody>();if(_planet==null)_planet=Object.FindFirstObjectByType<PlanetTerrainMeshBuilder>();if(_rb==null||_planet==null)return;
            // ★ 适配：同一物理位置不重复写刚体或执行邻居避让，累计步长保持转向平滑度。
            if(!Simulation.ActionMotionBatch.Consume(_controller,ref dt))return;
            bool landOnly = _controller.GetAttribute("swimSpeed") <= 0;
            Vector3 current=_rb.position;
            if(landOnly&&!_landRoute.Resolve(_planet,current,position,out position))
            {
                var up=(current-_planet.transform.position).normalized;
                EntityGroundProbe.Velocity(_rb,up*Mathf.Min(0,Vector3.Dot(_rb.linearVelocity,up)));
                return;
            }
            Vector3 radial = (current-_planet.transform.position).normalized;
            Vector3 toward = Vector3.ProjectOnPlane(position-current, radial);
            // ★ 适配：行动使用时钟实际交付倍率，过载补算限速不造成物理时间分离。
            float scale = ServiceLocator.TryGet<SimulationClock>(out var clock) ? clock.MotionTimeScale : 1;
            // ★ 修复（防Bug回归）：物理仍按真实步长运行，切向速度按倍率转换；限制到目标剩余距离避免高速越过。
            float physicalSpeed = Mathf.Min(EntityMovementLimits.Speed(speed) * scale, toward.magnitude / Mathf.Max(Time.deltaTime, .001f));
            Vector3 desired = toward.normalized * physicalSpeed;
            if(toward.sqrMagnitude>1e-8f)_rb.MoveRotation(Quaternion.RotateTowards(_rb.rotation,Quaternion.LookRotation(toward.normalized,radial),_settings.turnDegreesPerSecond*dt));
            // ★ 适配：同群成员保持身体间距，重叠列也向侧面走开，不向王后头顶施加吸附或垂直速度。
            desired=ColonyFormation.Avoid(_controller,desired,EntityMovementLimits.Speed(speed)*scale);
            Vector3 velocity = _rb.linearVelocity;
            float inward = Mathf.Min(0,Vector3.Dot(velocity,radial));
            Vector3 tangent = Vector3.Lerp(Vector3.ProjectOnPlane(velocity,radial),desired,1-Mathf.Exp(-_settings.steering*dt));
            // ★ 修复（防Bug回归）：群体避让和惯性也不能绕过岸边检查。
            if(landOnly&&(_gravity==null||!_gravity.isActiveAndEnabled||_gravity.planet!=_planet.transform))
            {
                var safe=LandRoutePlan.SafeVelocity(_planet,current,tangent,_collider!=null?_collider.bounds.extents.magnitude:0);
                if(tangent.sqrMagnitude>1e-8f&&safe.sqrMagnitude==0)_landRoute.Invalidate();
                tangent=safe;
            }
            else if(landOnly&&_gravity.ShoreBlocked)_landRoute.Invalidate();
            _rb.linearVelocity = Vector3.ClampMagnitude(tangent,_settings.maximumMoveSpeed*scale) + radial * inward;
        }
        public virtual string GetTooltipInfo()=>"游泳状态可手动入水推进。自主游泳需表达游泳基因。陆地速度与跳跃分别由属性决定。中枢负责自主行为。";
    }
}
