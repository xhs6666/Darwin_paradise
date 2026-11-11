using System.Runtime.CompilerServices;
using DarwinPark.Core;
using DarwinPark.Entities.Stats;
using UnityEngine;
namespace DarwinPark.Entities.Ecology
{
    // ★ 修复（防Bug回归）：同资格且小幅移动时按决策间隔复用坐标搜索；资源新增/搬运/归属/储粮变化立即失效，实际进食仍逐次扣量。
    public static class ColonyFoodQueryCache
    {
        private sealed class Choice
        {
            public float next;public int frame=-1;public long foodRevision,groups,capacity;public Vector3 point;public string group;public AntRole role;public bool young,urgent,share;public float queenReserve,feedRadius,queenLine,ownerRange;public ColonyResource value;public object tuning,settings;
        }
        private sealed class Entry {public readonly Choice near=new(),far=new();public AntColonyMember queen;public EntityStats queenStats;}
        private static ConditionalWeakTable<AntColonyMember,Entry> _entries=new();private static long _capacity;
        public static long Hits{get;private set;}public static long Searches{get;private set;}
        public static void AddedEnergy()=>_capacity++;
        public static ColonyResource Nearest(AntColonyMember member,bool nearby)
        {
            var entry=_entries.GetOrCreateValue(member);var queen=member.Queen;if(entry.queen!=queen){entry.queen=queen;entry.queenStats=queen!=null?queen.GetComponent<EntityStats>():null;}
            float queenReserve=0;if(member.Role!=AntRole.Queen&&entry.queenStats!=null){entry.queenStats.TryGet("nutrient",out float food);entry.queenStats.TryGet("storedNutrient",out float reserve);queenReserve=Mathf.Max(0,AntColonySettings.Current.queenFeedThreshold-food-reserve);}
            var cached=nearby?entry.near:entry.far;var point=ResourceCoordinates.RelativePoint(member.transform.position);bool young=member.Young,urgent=ColonyFoodSources.Urgent(member);var tuning=GameplayTuning.Current;var settings=AntColonySettings.Current;long revision=ResourceCoordinates.FoodRevision,groups=PopulationGroupIndex.Revision;
            bool key=(cached.frame==Time.frameCount||Time.unscaledTime<cached.next)&&cached.foodRevision==revision&&cached.groups==groups&&cached.capacity==_capacity&&(cached.point-point).sqrMagnitude<=SimulationOptimization.Current.foodSearchMoveTolerance*SimulationOptimization.Current.foodSearchMoveTolerance&&cached.group==member.GroupId&&cached.role==member.Role&&cached.young==young&&cached.urgent==urgent&&cached.queenReserve==queenReserve&&cached.feedRadius==settings.feedRadius&&cached.queenLine==settings.queenFeedThreshold&&cached.ownerRange==settings.transportSearchRadius&&cached.share==tuning.colonyShareBackupFood&&ReferenceEquals(cached.tuning,tuning)&&ReferenceEquals(cached.settings,settings);
            if(key&&(ReferenceEquals(cached.value,null)||cached.value!=null&&ColonyFoodSources.Usable(cached.value,member)&&(!nearby||EcologyQueries.Distance(cached.value.transform.position,member.transform.position)<=settings.feedRadius))){Hits++;return cached.value;}
            cached.value=ColonyFoodSources.Search(member,nearby);Searches++;cached.frame=Time.frameCount;cached.next=Time.unscaledTime+SimulationOptimization.Current.decisionSeconds;cached.foodRevision=revision;cached.groups=groups;cached.capacity=_capacity;cached.point=point;cached.group=member.GroupId;cached.role=member.Role;cached.young=young;cached.urgent=urgent;cached.queenReserve=queenReserve;cached.feedRadius=settings.feedRadius;cached.queenLine=settings.queenFeedThreshold;cached.ownerRange=settings.transportSearchRadius;cached.share=tuning.colonyShareBackupFood;cached.tuning=tuning;cached.settings=settings;return cached.value;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]private static void Reset(){_entries=new();_capacity=0;Hits=Searches=0;}
    }
}
