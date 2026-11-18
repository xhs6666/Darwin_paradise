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
            Vector3? contactExtents=null;
            // 基础表达为0时所有移动修饰仍为0，不必为静止实体遍历群体、角色和附着组件。
            float moveSpeed=GroundMotionSnapshot.Speed(genes);
            if(material!=null){var settings=LocomotionSettings.Load();bool moving=moveSpeed>0&&Vector3.ProjectOnPlane(body.linearVelocity,ground.up).sqrMagnitude>settings.frictionMotionThreshold*settings.frictionMotionThreshold;probe.Friction(material,moving?settings.movingFriction:settings.stationaryFriction);}
            float ratio=owner.planetRadius/Mathf.Max(ground.distance,owner.planetRadius);float multiplier=Mathf.Clamp(ratio*ratio,.45f,1),assist=!ground.underwater&&ground.distance>owner.planetRadius+60?Mathf.Min(20,(ground.distance-owner.planetRadius-60)*.25f):0;
            // ★ 新增：游泳维持入水深度并保留切向推进，不能被旧的溺水下沉路径清掉全部行动速度。
            // ★ 修复（防Bug回归）：同一物理步只解析一次游泳表达，供入水与岸边处理共用。
            float swimSpeed=genes!=null?genes.GetAttribute("swimSpeed"):0;
            if(swimSpeed<=0&&owner.ManualControl!=null)swimSpeed=owner.ManualControl.SwimmingSpeed;
            owner.ShoreBlocked=false;
            bool swimming=ground.underwater&&swimSpeed>0;
            if(swimming)
            {
                float radialSpeed=owner.ManualControl!=null?Mathf.Clamp((ground.immersion-.28f)*2.8f,-.75f,.75f):0;
                EntityGroundProbe.Velocity(body,Vector3.ProjectOnPlane(body.linearVelocity,ground.up)+ground.up*radialSpeed);
            }
            else body.AddForce(inward*(owner.baseGravity*multiplier+assist),ForceMode.Acceleration);
            if(ground.underwater&&!swimming)EntityGroundProbe.Velocity(body,inward*Mathf.Max(0,Vector3.Dot(body.linearVelocity,inward)));
            else if(genes != null && swimSpeed <= 0)
            {
                // ✦ 修复（Bug回归）：最终物理速度也检查海岸，碰撞推挤不能绕过寻路守卫。
                var velocity = body.linearVelocity;
                var tangent = Vector3.ProjectOnPlane(velocity, ground.up);
                if(tangent.sqrMagnitude>=1e-8f)
                {
                    if(collider!=null)contactExtents=collider.bounds.extents;
                    var safe=DarwinPark.Planet.LandRoutePlan.SafeVelocity(probe.Builder,ground.position,tangent,contactExtents?.magnitude??0);
                    owner.ShoreBlocked=safe.sqrMagnitude==0;
                    EntityGroundProbe.Velocity(body,safe+ground.up*Vector3.Dot(velocity,ground.up));
                }

            }
            // 与旧20米向内射线保持同一资格；穿地由共享安全网处理，不能把地下误当成已接触。
            if(!ground.hasSurface||ground.altitude<0||ground.altitude>20)return;
            float radius=contactExtents?.y??(collider!=null?collider.bounds.extents.y:1);
            if(ground.altitude<=radius+.5f)
            {
                if(moveSpeed<=0){var velocity=body.linearVelocity;var tangent=velocity-ground.normal*Vector3.Dot(velocity,ground.normal);if(tangent.sqrMagnitude>.01f)EntityGroundProbe.Velocity(body,velocity-tangent*.9f);}
                EntityGroundProbe.Align(body,ground.normal,15);StationaryBodyRest.TryRest(body,genes,collider,ground,owner.planet,moveSpeed);
            }
            else EntityGroundProbe.Align(body,ground.up,5);
        }

        // ★ 新增：操控实体的穿地修正与引力共用真实接触面，不重复从球外发射长射线。
        public static bool ResolvePenetration(Rigidbody body,Collider collider,EntityGroundSample sample,out bool cancelJump)
        {
            cancelJump=false;if(!sample.hasSurface||collider==null)return false;var bounds=collider.bounds;var up=sample.up;
            float support=Vector3.Dot(bounds.extents,new Vector3(Mathf.Abs(up.x),Mathf.Abs(up.y),Mathf.Abs(up.z))),gap=Vector3.Dot(bounds.center-sample.point,up)-support;if(gap>=0)return false;
            body.position+=up*(-gap+Physics.defaultContactOffset);float radial=Vector3.Dot(body.linearVelocity,up);
            if(radial<0){EntityGroundProbe.Velocity(body,body.linearVelocity-up*radial);cancelJump=true;}return true;
        }

        // ★ 新增：安全网复用真实三角形地表；接地时不再重复采样高度，高空拉回仍逐物理步累计。
        public static void AdvanceAnchor(EntitySurfaceAnchor settings,Rigidbody body,EntityGroundSample surface,ref float airTime)
        {
            if(!surface.hasSurface||surface.distance<1)return;if(surface.underwater){airTime=0;return;}
            float altitude=surface.altitude;var up=surface.up;
            if(altitude< -2){body.position=surface.point+up*.5f;EntityGroundProbe.Velocity(body,Vector3.ProjectOnPlane(body.linearVelocity,up));airTime=0;return;}
            if(altitude<=settings.maxAltitude){airTime=0;return;}
            airTime+=Time.fixedDeltaTime;var velocity=body.linearVelocity;float outward=Vector3.Dot(velocity,up);
            if(outward>0)EntityGroundProbe.Velocity(body,velocity-up*outward);
            float pull=Mathf.Min(settings.pullAcceleration,4+(altitude-settings.maxAltitude)*8);body.AddForce(-up*pull,ForceMode.Acceleration);
            if(airTime>=settings.rescueDelay||altitude>settings.rescueAltitude){body.position=surface.point+up;EntityGroundProbe.Velocity(body,Vector3.ProjectOnPlane(body.linearVelocity,up)*.5f);airTime=0;}
        }

        // ★ 修复（防Bug回归）：径向落点可能离斜坡真正接触点较远；只有实际星球表面接触才能休眠，不接受延长平面或其他实体。
        public static bool VerifyRestContact(Collider body,Collider ground,Vector3 point,Vector3 normal,float slack)
        {
            if((body.ClosestPoint(point)-point).sqrMagnitude<=slack*slack)return true;
            var bounds=body.bounds;EntityGroundProbe.Raycasts++;
            return ground.Raycast(new Ray(bounds.center,-normal),out var actual,bounds.extents.magnitude+slack)&&(body.ClosestPoint(actual.point)-actual.point).sqrMagnitude<=slack*slack;
        }
    }
}
