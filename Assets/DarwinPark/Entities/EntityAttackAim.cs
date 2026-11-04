using DarwinPark.Core;
using DarwinPark.Entities.Ecology;
using UnityEngine;

namespace DarwinPark.Entities
{
    // ★ 修复（防Bug回归）：仅扩展攻击选择框，不改变物理碰撞体；扩大球形扫掠会先碰地面，让近身实体反而无法选中。
    public static class EntityAttackAim
    {
        public static DarwinEntity Pick(GameObject self,Ray ray,float distance,float radius=0)
        {
            DarwinEntity best=null;float nearest=distance;
            foreach(var entity in EntityRegistry.All)
            {
                if(entity==null||entity.gameObject==self||!EcologyQueries.Alive(entity))continue;
                foreach(var collider in entity.GetComponentsInChildren<Collider>())
                {
                    if(!collider.enabled||collider.isTrigger||!collider.gameObject.activeInHierarchy)continue;
                    // ★ 修复（防Bug回归）：相机落在扩大的选择框内时，朝反方向也会相交；先排除准星后方实体。
                    if(Vector3.Dot(collider.bounds.center-ray.origin,ray.direction)<=0)continue;
                    var bounds=collider.bounds;
                    bounds.extents=bounds.extents*GameplayTuning.Current.manualHitboxScale+Vector3.one*Mathf.Max(0,radius);
                    if(!bounds.IntersectRay(ray,out float along)||along>nearest)continue;
                    // 对真实表面检查视线，仍禁止穿过地形、墙壁或其他实体攻击。
                    Vector3 point=collider.ClosestPoint(ray.GetPoint(Mathf.Max(0,along)));
                    Vector3 toward=point-ray.origin;bool blocked=false;
                    if(toward.sqrMagnitude>.0001f)foreach(var hit in Physics.RaycastAll(ray.origin,toward.normalized,toward.magnitude,~0,QueryTriggerInteraction.Ignore))
                    {
                        var owner=hit.collider.GetComponentInParent<DarwinEntity>();
                        if(owner==entity||owner!=null&&owner.gameObject==self)continue;
                        blocked=true;break;
                    }
                    if(blocked)continue;nearest=along;best=entity;
                }
            }
            return best;
        }
    }
}
