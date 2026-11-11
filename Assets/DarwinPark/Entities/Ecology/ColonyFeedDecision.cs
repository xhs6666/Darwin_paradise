using System;
using System.Runtime.CompilerServices;
using DarwinPark.Core;
using DarwinPark.Entities.Stats;
using UnityEngine;

namespace DarwinPark.Entities.Ecology
{
    // ★ 新增：同帧同属性版本只评估一次补给插队；扣粮、改上限、角色/基因/卵状态变化立即重算。
    public static class ColonyFeedDecision
    {
        private sealed class Entry
        {
            public EntityStats stats; public EntityTaskPlanSave plan; public int frame = -1, genes, options;
            public long values, groups; public string role; public bool feeding, queued, egg, result;
        }
        private static ConditionalWeakTable<AntColonyMember, Entry> _entries = new();
        public static long Computations { get; private set; }
        public static long Hits { get; private set; }
        private static bool Queued(EntityTaskPlanSave plan) => plan?.tasks != null && plan.tasks.Count > 0 && plan.tasks[0].kind == PlannedTaskKind.Feed;
        public static bool Read(AntColonyMember member)
        {
            using var timing=PerformanceTimings.Measure(PerformanceTimings.Area.FeedDecision);
            if (member == null) return false;
            var entry = _entries.GetOrCreateValue(member);
            if (entry.stats == null) entry.stats = OptionalComponent.Read<EntityStats>(member);
            var stats = entry.stats; if (stats == null) return false;
            var settings = AntColonySettings.Current; var tuning = GameplayTuning.Current;
            int options = HashCode.Combine(settings.memberFeedThreshold, settings.queenFeedThreshold, settings.queenReserveTarget, tuning.memberFeedResume, tuning.queenReserveBuffer);
            bool egg = OptionalComponent.Read<EggIncubation>(member)?.Incubating == true;
            var plan = member.State.plan;
            if (entry.frame == Time.frameCount && entry.values == stats.ValueRevision && entry.genes == member.Genes.LogicRevision && entry.groups == PopulationGroupIndex.Revision && entry.role == member.State.role && entry.options == options && entry.egg == egg && ReferenceEquals(entry.plan, plan) && entry.feeding == (plan?.feeding ?? false) && entry.queued == Queued(plan))
            { Hits++; return entry.result; }
            Computations++;
            bool result = member.Active && !egg && EntityTaskPlan.EvaluateFeeding(member, stats);
            entry.frame = Time.frameCount; entry.values = stats.ValueRevision; entry.genes = member.Genes.LogicRevision;
            entry.groups = PopulationGroupIndex.Revision; entry.role = member.State.role; entry.options = options; entry.egg = egg;
            entry.plan = member.State.plan; entry.feeding = entry.plan?.feeding ?? false; entry.queued = Queued(entry.plan); entry.result = result;
            return result;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() { _entries = new(); Computations = Hits = 0; }
    }
}
