using DarwinPark.Entities.Stats;
using UnityEngine;

namespace DarwinPark.Entities.Logic
{
    // ★ 新增：AI、玩家与实际跳跃共用体型和上限规则，不能只限制面板数值而让刚体无限加速。
    public static class EntityMovementLimits
    {
        public static float Speed(float value)=>Mathf.Clamp(value,0,LocomotionSettings.Load().maximumMoveSpeed);
        public static float Attribute(EntityGeneController genes,string id,float value)
        {
            if(id=="moveSpeed")return Speed(value);
            if(id!="jumpAbility")return value;
            var settings=LocomotionSettings.Load();var stats=genes.GetComponent<EntityStats>();
            float size=stats!=null&&stats.TryGet("size",out float actual)?actual:1;
            return Mathf.Clamp(value/Mathf.Pow(Mathf.Max(settings.minimumMovementSize,size),settings.jumpSizeExponent),0,settings.maximumJumpSpeed);
        }
    }
}
