using UnityEngine;

namespace DarwinPark.Entities
{
    /// <summary>★ 新增：手动攻击只取准星射线第一个可见实体，排除自身，地形或其他实体会遮挡。</summary>
    public static class EntityAttackAim
    {
        public static DarwinEntity Pick(GameObject self,Ray ray,float distance,float radius=0)
        {
            RaycastHit? nearest=null;
            // ★ 修复（防Bug回归）：近身小球的中心常低于准星细射线，使用小范围瞄准容差，仍取第一遮挡且排除自身。
            var hits=radius>0?Physics.SphereCastAll(ray,radius,distance,~0,QueryTriggerInteraction.Ignore)
                :Physics.RaycastAll(ray,distance,~0,QueryTriggerInteraction.Ignore);
            foreach(var hit in hits)
            {
                var entity=hit.collider.GetComponentInParent<DarwinEntity>();
                if(entity!=null && entity.gameObject==self)continue;
                if(!nearest.HasValue || hit.distance<nearest.Value.distance)nearest=hit;
            }
            return nearest?.collider.GetComponentInParent<DarwinEntity>();
        }
    }
}
