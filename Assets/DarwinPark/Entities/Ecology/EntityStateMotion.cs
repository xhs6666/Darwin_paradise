using DarwinPark.Genetics;
using UnityEngine;

namespace DarwinPark.Entities.Ecology
{
    // ★ 新增：状态只选择意图，沿用原行动器的球面移动、攻击冷却和营养结算，避免重复写刚体。
    public static class EntityStateMotion
    {
        public static bool Directive(EntityGeneController genes,out Vector3 point,out DarwinEntity target,out float multiplier)
        {
            point=genes.transform.position;target=null;multiplier=1;
            var state=genes.GetComponent<EntityStateController>();if(state==null)return false;
            switch(state.Current.Behavior)
            {
                case EntityStateBehavior.TerritoryPatrol:
                    var territory=genes.GetComponent<TerritoryBehavior>();
                    return territory!=null&&territory.PatrolDestination(out point);
                case EntityStateBehavior.Hunt:
                    // ★ 适配：普通主动取食需要消化能力；防御/手动攻击与蚁群士兵仍走各自入口。
                    if(!NutritionTransfer.CanHuntForFood(genes))return false;
                    float range=EntityPerceptionHub.Range(genes,state.Current.targetRangeAttributeId);
                    if(PerceivedTargets.Valid(genes,state.DetectedTarget,false,out _,true,range))state.HuntTarget=state.DetectedTarget;
                    state.HuntTarget=PerceivedTargets.Nearest(genes,false,state.HuntTarget,out point,true,range);
                    target=state.HuntTarget;
                    if(target==null)return false;
                    return true;
                default:return false;
            }
        }
    }
}
