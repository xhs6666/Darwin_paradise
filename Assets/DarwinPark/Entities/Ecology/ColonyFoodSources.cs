using System.Collections.Generic;
using DarwinPark.Core;
using DarwinPark.Entities.Stats;
using UnityEngine;

namespace DarwinPark.Entities.Ecology
{
    // ★ 修复（防Bug回归）：只索引能量块，避免每名饥饿成员反复扫数千尸体；育幼/成年是优先用途，不能成为饿死时的硬限制。
    public static class ColonyFoodSources
    {
        private static readonly List<ColonyResource> Foods=new();
        private static readonly List<ResourceCoordinate> Candidates=new();
        public static int Count=>Foods.Count;
        public static long CandidateVisits {get;private set;}
        public static void Register(ColonyResource resource)
        {if(resource!=null&&resource.State!=null&&resource.State.kind!=ColonyResourceKind.Corpse&&!Foods.Contains(resource))Foods.Add(resource);}
        public static void Remove(ColonyResource resource)=>Foods.Remove(resource);
        public static void Clear(){Foods.Clear();CandidateVisits=0;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]private static void Reset()=>Clear();
        private static int Rank(ColonyResource resource,AntColonyMember member)
        {
            if(resource==null||!resource.isActiveAndEnabled||resource.State.energy<=0||resource.State.kind==ColonyResourceKind.Corpse)return int.MaxValue;
            var preferred=member.Role==AntRole.Queen||member.Young?ColonyResourceKind.Nursery:ColonyResourceKind.Supply;
            return resource.State.kind==preferred||resource.State.kind==ColonyResourceKind.Energy?0:GameplayTuning.Current.colonyShareBackupFood?1:int.MaxValue;
        }
        public static bool Usable(ColonyResource resource,AntColonyMember member)
            =>Rank(resource,member)!=int.MaxValue&&ColonyResourceOwnership.Available(resource,member)&&ColonyFeeding.AvailableEnergy(resource,member)>0;
        public static bool Urgent(AntColonyMember member)=>member.GetComponent<EntityStats>().TryGet("nutrient",out var food)&&food<GameplayTuning.Current.colonyEmergencyFeedThreshold;
        // ★ 适配：只复用当前帧搜索结果，不缓存实际可消费能量。
        public static ColonyResource Nearest(AntColonyMember member,bool nearbyOnly)=>ColonyFoodQueryCache.Nearest(member,nearbyOnly);
        internal static ColonyResource Search(AntColonyMember member,bool nearbyOnly)
        {
            ColonyResource best=null;int rank=int.MaxValue;float distance=float.PositiveInfinity;Vector3 point=member.transform.position;
            // ★ 修复（防Bug回归）：同一次同步搜索中年龄/用途和王后保留量不变化，不能为每个候选重复解析组件。
            var preferred=member.Role==AntRole.Queen||member.Young?ColonyResourceKind.Nursery:ColonyResourceKind.Supply;bool share=GameplayTuning.Current.colonyShareBackupFood;float protectedEnergy=0;
            var queen=member.Role==AntRole.Queen?null:member.Queen;if(queen!=null){var stats=queen.GetComponent<EntityStats>();stats.TryGet("nutrient",out float food);stats.TryGet("storedNutrient",out float reserve);protectedEnergy=Mathf.Max(0,AntColonySettings.Current.queenFeedThreshold-food-reserve);}
            // ★ 适配：近食范围走坐标格，远食只读全局食物分类坐标，不接触尸体桶。
            ColonyFoodCandidates.Fill(member,nearbyOnly,Candidates);
            foreach(var coordinate in Candidates)
            {
                var resource=coordinate.resource;
                CandidateVisits++;if(resource==null||!resource.isActiveAndEnabled||resource.State.energy<=0||resource.State.kind==ColonyResourceKind.Corpse)continue;int priority=resource.State.kind==preferred||resource.State.kind==ColonyResourceKind.Energy?0:share?1:int.MaxValue;if(priority==int.MaxValue)continue;
                float squared=(coordinate.position-point).sqrMagnitude;
                // ★ 修复（防Bug回归）：远处按可吃的最近块选择，不能为专用类别绕过近粮；同一进食范围内才优先专用用途。
                if(nearbyOnly?(priority>rank||priority==rank&&squared>=distance):(squared>distance||squared==distance&&priority>=rank))continue;
                if(nearbyOnly&&EcologyQueries.Distance(resource.transform.position,point)>AntColonySettings.Current.feedRadius||!ColonyResourceOwnership.Available(resource,member)||resource.State.energy-(resource.State.kind==ColonyResourceKind.Nursery?protectedEnergy:0)<=0)continue;
                if(!nearbyOnly&&!LandReachability.CanReach(member.Genes,resource))continue;
                best=resource;rank=priority;distance=squared;
            }
            return best;
        }
    }
}
