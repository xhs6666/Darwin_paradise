using UnityEngine;

namespace DarwinPark.Entities
{
    /// <summary>★ 新增：手动攻击只取准星射线第一个可见实体，排除自身，地形或其他实体会遮挡。</summary>
    public static class EntityAttackAim
    {
        public static DarwinEntity Pick(GameObject self,Ray ray,float distance)
        {
            RaycastHit? nearest=null;
            foreach(var hit in Physics.RaycastAll(ray,distance,~0,QueryTriggerInteraction.Ignore))
            {
                var entity=hit.collider.GetComponentInParent<DarwinEntity>();
                if(entity!=null && entity.gameObject==self)continue;
                if(!nearest.HasValue || hit.distance<nearest.Value.distance)nearest=hit;
            }
            return nearest?.collider.GetComponentInParent<DarwinEntity>();
        }
    }
}
