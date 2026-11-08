using DarwinPark.Entities.Stats;
using UnityEngine;

namespace DarwinPark.Entities.Ecology
{
    // ★ 修复（防Bug回归）：进食与寻路共用资格和饥饿判断，避免有补给却一直运输、追猎或停在王后旧位置。
    public static class ColonyFeeding
    {
        // ★ 适配：所有消费和寻路入口读取同一份持久化计划。
        public static bool WantsFood(AntColonyMember member)=>EntityTaskPlan.Feeding(member);
        public static ColonyResource Find(AntColonyMember member,bool nearbyOnly)
        {
            // ★ 适配：寻路与进食搜索单独计时，实际卡顿时可判断是否被资源数量放大。
            using var performance=DarwinPark.Core.PerformanceTimings.Measure(DarwinPark.Core.PerformanceTimings.Area.Feeding);
            if(!WantsFood(member))return null;
            var locked=ColonyResource.Find(member.State.plan.supply);
            bool valid=ColonyFoodSources.Usable(locked,member);
            // ★ 修复（防Bug回归）：远处锁定目标不能阻止吃身边的块；进食半径内优先实际补给，离开半径才继续原计划。
            if(valid&&EcologyQueries.Distance(locked.transform.position,member.transform.position)<=AntColonySettings.Current.feedRadius)return locked;
            var local=ColonyFoodSources.Nearest(member,true);
            if(local!=null){if(!nearbyOnly)member.State.plan.supply=local.State.id;return local;}
            if(valid&&(nearbyOnly||!ColonyFoodSources.Urgent(member)))return nearbyOnly?null:locked;
            // ★ 修复（防Bug回归）：饱食跌到紧急线后允许改走最近可吃块，不能为保持远处旧计划而饿死。
            member.State.plan.supply=null;
            if(nearbyOnly)return null;
            var nearest=ColonyFoodSources.Nearest(member,false);
            if(nearest!=null)member.State.plan.supply=nearest.State.id;return nearest;
        }
        public static float AvailableEnergy(ColonyResource resource,AntColonyMember member)
        {
            if(resource.State.kind!=ColonyResourceKind.Nursery||member.Role==AntRole.Queen)return resource.State.energy;
            var queen=member.Queen;if(queen==null)return resource.State.energy;
            var stats=queen.GetComponent<EntityStats>();stats.TryGet("nutrient",out float food);stats.TryGet("storedNutrient",out float reserve);
            // ★ 修复（防Bug回归）：幼体或借用育幼块的成年成员不能吃光减速王后的最低口粮；不预占繁殖储备。
            return Mathf.Max(0,resource.State.energy-Mathf.Max(0,AntColonySettings.Current.queenFeedThreshold-food-reserve));
        }
        public static bool Destination(AntColonyMember member,out Vector3 point)
        {
            var resource=Find(member,false);point=resource!=null?resource.transform.position:member.transform.position;
            if(resource==null)return false;
            // ★ 修复（防Bug回归）：进入真实进食半径就停下，远处成员分散到资源周围，不能全部追到王后/能量块的中心。
            point=EcologyQueries.Distance(member.transform.position,point)<=AntColonySettings.Current.feedRadius?member.transform.position:ColonyFormation.Home(member,point,AntColonySettings.Current.feedRadius*.65f);
            member.Activity=ColonyActivity.Feed;return true;
        }
    }
}
