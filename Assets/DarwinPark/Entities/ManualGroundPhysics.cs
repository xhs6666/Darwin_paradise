using UnityEngine;
namespace DarwinPark.Entities
{
    // ★ 新增：操控实体的穿地修正与引力共用真实接触面，不重复从球外发射长射线。
    public static class ManualGroundPhysics
    {
        public static bool Resolve(Rigidbody body,Collider collider,EntityGroundSample sample,out bool cancelJump)
        {
            cancelJump=false;if(!sample.hasSurface||collider==null)return false;var bounds=collider.bounds;var up=sample.up;
            float support=Vector3.Dot(bounds.extents,new Vector3(Mathf.Abs(up.x),Mathf.Abs(up.y),Mathf.Abs(up.z))),gap=Vector3.Dot(bounds.center-sample.point,up)-support;if(gap>=0)return false;
            body.position+=up*(-gap+Physics.defaultContactOffset);float radial=Vector3.Dot(body.linearVelocity,up);
            if(radial<0){EntityGroundProbe.Velocity(body,body.linearVelocity-up*radial);cancelJump=true;}return true;
        }
    }
}
