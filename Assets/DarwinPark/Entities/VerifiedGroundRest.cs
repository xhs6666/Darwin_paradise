using UnityEngine;
namespace DarwinPark.Entities
{
    // ★ 修复（防Bug回归）：径向落点可能离斜坡真正接触点较远；只有实际星球表面接触才能休眠，不接受延长平面或其他实体。
    public static class VerifiedGroundRest
    {
        public static bool Touching(Collider body,Collider ground,Vector3 point,Vector3 normal,float slack)
        {
            if((body.ClosestPoint(point)-point).sqrMagnitude<=slack*slack)return true;
            var bounds=body.bounds;EntityGroundProbe.Raycasts++;
            return ground.Raycast(new Ray(bounds.center,-normal),out var actual,bounds.extents.magnitude+slack)&&(body.ClosestPoint(actual.point)-actual.point).sqrMagnitude<=slack*slack;
        }
    }
}
