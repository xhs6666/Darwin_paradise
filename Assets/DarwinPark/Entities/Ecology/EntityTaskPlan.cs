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
        // ★ 适配：顺序任务随既有计划一同保存，旧档缺省为空。
        public System.Collections.Generic.List<PlannedEntityTask> tasks=new();
    }
    // ★ 新增：补给是可以完成的计划，而不是每个寻路/运输子步独立切换的瞬时状态。
    public static class EntityTaskPlan
    {
        // ★ 修复（防Bug回归）：补给也高于迁居，维持生命后才能继续建群。
        public static int Priority(ColonyActivity activity)=>activity==ColonyActivity.Feed?120:activity==ColonyActivity.Founding?110:activity==ColonyActivity.Transport?70:activity==ColonyActivity.Reproduce?60:activity==ColonyActivity.Collect?50:activity==ColonyActivity.Hunt?40:activity==ColonyActivity.Patrol?20:10;
        public static bool Feeding(AntColonyMember member)=>ColonyFeedDecision.Read(member);
        // ★ 适配：纯判断仅由版本缓存的唯一入口调用，插队和恢复逻辑保持原语义。
        internal static bool EvaluateFeeding(AntColonyMember member,EntityStats stats)
        {
            // ★ 修复（防Bug回归）：无孵化组件是正常情况，TryGet避免编辑器为高频空GetComponent生成诊断字符串。
            var plan=member.State.plan??(member.State.plan=new EntityTaskPlanSave());
            if(!stats.TryGet("nutrient",out var food))return false;
            stats.TryGet("storedNutrient",out var reserve);
            var settings=AntColonySettings.Current;var tuning=GameplayTuning.Current;
            bool queen=member.Role==AntRole.Queen;
            bool hungry=food<(queen?settings.queenFeedThreshold:settings.memberFeedThreshold)||queen&&reserve<settings.queenReserveTarget;
            bool satisfied=food>=Math.Min(food+stats.CapacityRemaining("nutrient"),Math.Max(tuning.memberFeedResume,queen?settings.queenFeedThreshold:settings.memberFeedThreshold))&&(!queen||reserve>=Math.Min(reserve+stats.CapacityRemaining("storedNutrient"),settings.queenReserveTarget+tuning.queenReserveBuffer));
            // ★ 修复（防Bug回归）：跨过开始线仍保留补给计划，吃饱后才恢复原货物/巡逻，货物不丢失。
            if(plan.feeding&&satisfied){plan.feeding=false;plan.supply=null;}
            else if(!plan.feeding&&hungry)plan.feeding=true;
            // ★ 适配：只在补给开关变化时插队，不能每次查询重排正在执行的计划。
            bool queued=plan.tasks!=null&&plan.tasks.Count>0&&plan.tasks[0].kind==PlannedTaskKind.Feed;
            if(queued!=plan.feeding)WorkerTaskQueue.Feeding(member,plan.feeding);
            return plan.feeding;
        }
        public static void Validate(EntityTaskPlanSave plan)
        {
            WorkerTaskQueue.Validate(plan?.tasks);
            if(plan!=null&&!string.IsNullOrEmpty(plan.supply)&&!Guid.TryParseExact(plan.supply,"N",out _))throw new InvalidDataException("实体补给计划的资源标识无效");
        }
    }
}
