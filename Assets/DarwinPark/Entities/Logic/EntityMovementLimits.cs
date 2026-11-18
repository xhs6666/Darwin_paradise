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
            // ★ 修复（防Bug回归）：演化方向决定形态，不可凭“游泳”方向禁用陆地速度或跳跃；实际能力只读取属性。
            if(id=="moveSpeed")
            {
                bool wet=Simulation.ActiveSimulationBatch.Read<Vitals.EntityEnvironmentHazard>(genes)?.IsSubmerged==true;
                if(!wet)return Speed(value);
                float swimming=genes.GetAttribute("swimSpeed");
                if(swimming<=0)
                    swimming=Simulation.ActiveSimulationBatch.Read<EntityManualControl>(genes)?.SwimmingSpeed??0;
                return Speed(swimming);
            }
            if(id!="jumpAbility")return value;
            var settings=LocomotionSettings.Load();var stats=genes.GetComponent<EntityStats>();
            float size=stats!=null&&stats.TryGet("size",out float actual)?actual:1;
            return Mathf.Clamp(value/Mathf.Pow(Mathf.Max(settings.minimumMovementSize,size),settings.jumpSizeExponent),0,settings.maximumJumpSpeed);
        }
    }
}
