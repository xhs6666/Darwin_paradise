using System;
using System.IO;
using DarwinPark.Core;
using DarwinPark.Entities.Stats;

namespace DarwinPark.Entities.Ecology
{
    [Serializable] public sealed class EntityTaskPlanSave
    {
        public bool feeding;
        public string supply;
    }
    // ★ 新增：补给是可以完成的计划，而不是每个寻路/运输子步独立切换的瞬时状态。
    public static class EntityTaskPlan
    {
        public static int Priority(ColonyActivity activity)=>activity==ColonyActivity.Feed?100:activity==ColonyActivity.Founding?110:activity==ColonyActivity.Transport?70:activity==ColonyActivity.Reproduce?60:activity==ColonyActivity.Collect?50:activity==ColonyActivity.Hunt?40:activity==ColonyActivity.Patrol?20:10;
        public static bool Feeding(AntColonyMember member)
        {
            if(member==null||!member.Active||member.GetComponent<EggIncubation>()?.Incubating==true)return false;
            var plan=member.State.plan??(member.State.plan=new EntityTaskPlanSave());
            var stats=member.GetComponent<EntityStats>();if(stats==null||!stats.TryGet("nutrient",out var food))return false;
            stats.TryGet("storedNutrient",out var reserve);
            var settings=AntColonySettings.Current;var tuning=GameplayTuning.Current;
            bool queen=member.Role==AntRole.Queen;
            bool hungry=food<(queen?settings.queenFeedThreshold:settings.memberFeedThreshold)||queen&&reserve<settings.queenReserveTarget;
            bool satisfied=food>=Math.Min(food+stats.CapacityRemaining("nutrient"),Math.Max(tuning.memberFeedResume,queen?settings.queenFeedThreshold:settings.memberFeedThreshold))&&(!queen||reserve>=Math.Min(reserve+stats.CapacityRemaining("storedNutrient"),settings.queenReserveTarget+tuning.queenReserveBuffer));
            // ★ 修复（防Bug回归）：跨过开始线仍保留补给计划，吃饱后才恢复原货物/巡逻，货物不丢失。
            if(plan.feeding&&satisfied){plan.feeding=false;plan.supply=null;}
            else if(!plan.feeding&&hungry)plan.feeding=true;
            return plan.feeding;
        }
        public static void Validate(EntityTaskPlanSave plan)
        {
            if(plan!=null&&!string.IsNullOrEmpty(plan.supply)&&!Guid.TryParseExact(plan.supply,"N",out _))throw new InvalidDataException("实体补给计划的资源标识无效");
        }
    }
}
