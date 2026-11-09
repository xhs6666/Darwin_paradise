using UnityEngine;
namespace DarwinPark.Entities
{
    // ★ 新增：安全网复用真实三角形地表；接地时不再重复采样高度，高空拉回仍逐物理步累计。
    public static class SurfaceAnchorPhysics
    {
        public static void Advance(EntitySurfaceAnchor settings,Rigidbody body,EntityGroundSample surface,ref float airTime)
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
    }
}
