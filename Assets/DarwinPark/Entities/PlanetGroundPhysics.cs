using DarwinPark.Core;
using DarwinPark.Entities.Logic;
using UnityEngine;
namespace DarwinPark.Entities
{
    // ★ 新增：每物理步只计算一次径向/水位/地面及移动资格，独立保留真实重力、入水下沉和接触制动。
    public static class PlanetGroundPhysics
    {
        public static void Advance(PlanetGravity owner,Rigidbody body,Collider collider,PhysicsMaterial material,EntityGeneController genes,EntityGroundProbe probe)
        {
            using var performance=PerformanceTimings.Measure(PerformanceTimings.Area.Gravity);
            if(body==null||body.isKinematic)return;
            if(ServiceLocator.TryGet<SimulationClock>(out var clock)&&!clock.IsRunning)
            {EntityGroundProbe.Velocity(body,Vector3.zero);if(body.angularVelocity!=Vector3.zero)body.angularVelocity=Vector3.zero;return;}
            if(owner.planet==null||StationaryBodyRest.RemainsAsleep(body,genes))return;
            var ground=probe.Read(owner.planet);var inward=-ground.up;
            // 基础表达为0时所有移动修饰仍为0，不必为静止实体遍历群体、角色和附着组件。
            float moveSpeed=genes!=null&&genes.Evaluation!=null&&genes.Evaluation.Attributes.TryGetValue("moveSpeed",out float basic)&&basic>0?genes.GetAttribute("moveSpeed"):0;
            if(material!=null){var settings=LocomotionSettings.Load();bool moving=moveSpeed>0&&Vector3.ProjectOnPlane(body.linearVelocity,ground.up).sqrMagnitude>settings.frictionMotionThreshold*settings.frictionMotionThreshold;probe.Friction(material,moving?settings.movingFriction:settings.stationaryFriction);}
            float ratio=owner.planetRadius/Mathf.Max(ground.distance,owner.planetRadius);float multiplier=Mathf.Clamp(ratio*ratio,.45f,1),assist=!ground.underwater&&ground.distance>owner.planetRadius+60?Mathf.Min(20,(ground.distance-owner.planetRadius-60)*.25f):0;
            body.AddForce(inward*(owner.baseGravity*multiplier+assist),ForceMode.Acceleration);
            if(ground.underwater)EntityGroundProbe.Velocity(body,inward*Mathf.Max(0,Vector3.Dot(body.linearVelocity,inward)));
            // 与旧20米向内射线保持同一资格；穿地由共享安全网处理，不能把地下误当成已接触。
            if(!ground.hasSurface||ground.altitude<0||ground.altitude>20)return;
            float radius=collider!=null?collider.bounds.extents.y:1;
            if(ground.altitude<=radius+.5f)
            {
                if(moveSpeed<=0){var velocity=body.linearVelocity;var tangent=velocity-ground.normal*Vector3.Dot(velocity,ground.normal);if(tangent.sqrMagnitude>.01f)EntityGroundProbe.Velocity(body,velocity-tangent*.9f);}
                EntityGroundProbe.Align(body,ground.normal,15);StationaryBodyRest.TryRest(body,genes,collider,ground,owner.planet,moveSpeed);
            }
            else EntityGroundProbe.Align(body,ground.up,5);
        }
    }
}
