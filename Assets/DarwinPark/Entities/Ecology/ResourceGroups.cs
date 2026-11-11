using System.Collections.Generic;
using UnityEngine;
namespace DarwinPark.Entities.Ecology
{
    // ★ 新增：活动圆、存放点与转换复用能量块的种群索引，不为每个候选遍历尸体。
    public static class ResourceGroups
    {
        private static readonly Dictionary<string,List<ResourceCoordinate>> Groups=new();private static readonly List<ResourceCoordinate> Buffer=new(),Empty=new();private static long _version=-1,_pose=-1;
        public static IReadOnlyList<ResourceCoordinate> Read(string group)
        {
            // Food读取会处理星球矩阵变化，版本随后再判断。
            long revision=ResourceCoordinates.FoodRevision;
            if(_version!=revision||_pose!=ResourceCoordinates.PoseRevision){Groups.Clear();ResourceCoordinates.Food(Buffer);foreach(var item in Buffer){string id=item.resource.State.group??"";if(!Groups.TryGetValue(id,out var list)){list=new();Groups.Add(id,list);}list.Add(item);}Buffer.Clear();_version=ResourceCoordinates.FoodRevision;_pose=ResourceCoordinates.PoseRevision;}
            return Groups.TryGetValue(group??"",out var resources)?resources:Empty;
        }
        public static bool Has(string group,ColonyResourceKind kind){foreach(var item in Read(group))if(item.resource.State.energy>0&&item.resource.State.kind==kind)return true;return false;}
        public static void Clear(){Groups.Clear();Buffer.Clear();_version=_pose=-1;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]private static void Reset()=>Clear();
    }
    public static class ActivityCircleCache
    {
        private sealed class Entry {public readonly List<ActivityCircle> circles=new();public PopulationBehavior leader;public Vector3 point;public float radius,ratio;public long food,pose;public bool queen;}
        private static readonly Dictionary<string,Entry> Cache=new();private static long _groups=-1;
        public static IReadOnlyList<ActivityCircle> Read(PopulationBehavior pop)
        {
            if(_groups!=PopulationGroupIndex.Revision){Cache.Clear();_groups=PopulationGroupIndex.Revision;}
            if(pop==null||!pop.Active)return System.Array.Empty<ActivityCircle>();var queen=AntColonyMember.QueenFor(pop);var leader=queen!=null?queen.Population:pop.Leader;if(leader==null)return System.Array.Empty<ActivityCircle>();
            float radius=queen!=null?queen.Radius:Mathf.Max(AntColonySettings.Current.minimumColonyRadius,leader.GetComponent<EntityGeneController>().GetAttribute("territory_radius"))*DarwinPark.Core.GameplayTuning.Current.colonyActivityRadiusMultiplier;
            string key=pop.GroupId??"";if(!Cache.TryGetValue(key,out var entry)){entry=new();Cache.Add(key,entry);}float ratio=DarwinPark.Core.GameplayTuning.Current.supplyActivityRadiusRatio;long food=ResourceCoordinates.FoodRevision;
            if(entry.leader==leader&&entry.point==leader.transform.position&&entry.radius==radius&&entry.ratio==ratio&&entry.food==food&&entry.pose==ResourceCoordinates.PoseRevision&&entry.queen==(queen!=null))return entry.circles;
            entry.leader=leader;entry.point=leader.transform.position;entry.radius=radius;entry.ratio=ratio;entry.food=food;entry.pose=ResourceCoordinates.PoseRevision;entry.queen=queen!=null;entry.circles.Clear();entry.circles.Add(new ActivityCircle("leader",entry.point,radius));
            if(queen!=null)foreach(var item in ResourceGroups.Read(pop.GroupId)){var resource=item.resource;if(resource.State.energy<=0||resource.State.kind!=ColonyResourceKind.Supply&&resource.State.kind!=ColonyResourceKind.Nursery)continue;if(Mathf.Approximately(radius,radius*ratio)&&Vector3.Distance(item.position,entry.point)<.01f)continue;entry.circles.Add(new ActivityCircle("resource-"+resource.State.id,item.position,radius*ratio));}
            return entry.circles;
        }
        public static void Clear(){Cache.Clear();_groups=-1;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]private static void Reset()=>Clear();
    }
}
