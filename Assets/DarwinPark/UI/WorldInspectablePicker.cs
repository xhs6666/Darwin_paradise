using DarwinPark.Entities;
using DarwinPark.Entities.Ecology;
using UnityEngine;

namespace DarwinPark.UI
{
    // ★ 新增：无物理碰撞的尸体/能量块用射线与原始几何求交；不为拾取给全地图资源增加物理开销。
    public static class WorldInspectablePicker
    {
        public static bool Pick(Ray ray,float maximum,out DarwinEntity entity,out ColonyResource resource)
        {
            entity=null;resource=null;ray.direction=ray.direction.normalized;float nearest=maximum;
            if(Physics.Raycast(ray,out var hit,maximum,~0,QueryTriggerInteraction.Ignore)){nearest=hit.distance;entity=hit.collider.GetComponentInParent<DarwinEntity>();}
            foreach(var candidate in ColonyResource.All)
            {
                var origin=candidate.transform.InverseTransformPoint(ray.origin);var direction=candidate.transform.InverseTransformVector(ray.direction);
                float distance=candidate.State.kind==ColonyResourceKind.Corpse?Sphere(origin,direction):Box(origin,direction);
                if(distance<0||distance>=nearest)continue;nearest=distance;resource=candidate;entity=null;
            }
            return entity!=null||resource!=null;
        }
        private static float Sphere(Vector3 origin,Vector3 direction)
        {
            float a=Vector3.Dot(direction,direction);if(a<=0)return -1;float b=Vector3.Dot(origin,direction),c=origin.sqrMagnitude-.25f,d=b*b-a*c;if(d<0)return -1;
            float near=(-b-Mathf.Sqrt(d))/a,far=(-b+Mathf.Sqrt(d))/a;return near>=0?near:far>=0?far:-1;
        }
        private static float Box(Vector3 origin,Vector3 direction)
        {
            float near=0,far=float.PositiveInfinity;
            for(int axis=0;axis<3;axis++)
            {
                if(Mathf.Abs(direction[axis])<Mathf.Epsilon){if(origin[axis]<-.5f||origin[axis]>.5f)return -1;continue;}
                float a=(-.5f-origin[axis])/direction[axis],b=(.5f-origin[axis])/direction[axis];near=Mathf.Max(near,Mathf.Min(a,b));far=Mathf.Min(far,Mathf.Max(a,b));if(near>far)return -1;
            }
            return far>=0?near:-1;
        }
    }
}
