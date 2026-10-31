using DarwinPark.Entities.Stats;
using UnityEngine;

namespace DarwinPark.Entities.Ecology
{
    // ★ 修复（防Bug回归）：进食与寻路共用资格和饥饿判断，避免有补给却一直运输、追猎或停在王后旧位置。
    public static class ColonyFeeding
    {
        public static bool WantsFood(AntColonyMember member)
        {
            if(member==null||!member.Active||member.GetComponent<EggIncubation>()?.Incubating==true)return false;
            var stats=member.GetComponent<EntityStats>();var settings=AntColonySettings.Current;
            if(stats==null||!stats.TryGet("nutrient",out float food))return false;
            if(member.Role!=AntRole.Queen)return food<settings.memberFeedThreshold;
            stats.TryGet("storedNutrient",out float reserve);
            return food<settings.queenFeedThreshold||reserve<settings.queenReserveTarget;
        }
        public static ColonyResource Find(AntColonyMember member,bool nearbyOnly)
        {
            if(!WantsFood(member))return null;
            var kind=member.Role==AntRole.Queen||member.Young?ColonyResourceKind.Nursery:ColonyResourceKind.Supply;
            ColonyResource nearest=null;float best=float.PositiveInfinity;Vector3 point=member.transform.position;
            foreach(var resource in ColonyResource.All)
            {
                // ★ 适配：新基因吐出的通用块可供各分工吸收，原育幼和成年补给的限制保持有效。
                if(resource.State.kind!=kind&&resource.State.kind!=ColonyResourceKind.Energy||!ColonyResourceOwnership.Available(resource,member)||AvailableEnergy(resource,member)<=0)continue;
                float distance=(resource.transform.position-point).sqrMagnitude;
                if(distance>=best||nearbyOnly&&EcologyQueries.Distance(resource.transform.position,point)>AntColonySettings.Current.feedRadius)continue;
                best=distance;nearest=resource;
            }
            return nearest;
        }
        public static float AvailableEnergy(ColonyResource resource,AntColonyMember member)
        {
            if(resource.State.kind!=ColonyResourceKind.Nursery||!member.Young||member.Role==AntRole.Queen)return resource.State.energy;
            var queen=member.Queen;if(queen==null)return resource.State.energy;
            var stats=queen.GetComponent<EntityStats>();stats.TryGet("nutrient",out float food);stats.TryGet("storedNutrient",out float reserve);
            // ★ 修复（防Bug回归）：幼体比减速王后先抵达时，不能吃光最后一块育幼补给；只预留王后脱离饥饿所缺的量，不额外预占繁殖储备。
            return Mathf.Max(0,resource.State.energy-Mathf.Max(0,AntColonySettings.Current.queenFeedThreshold-food-reserve));
        }
        public static bool Destination(AntColonyMember member,out Vector3 point)
        {
            var resource=Find(member,false);point=resource!=null?resource.transform.position:member.transform.position;
            if(resource==null)return false;
            member.Activity=ColonyActivity.Feed;return true;
        }
    }
}
