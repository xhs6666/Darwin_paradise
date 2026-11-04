using System.Collections.Generic;
using DarwinPark.Core;
using UnityEngine;

namespace DarwinPark.Entities.Ecology
{
    public readonly struct ActivityCircle
    {
        public readonly string Id;public readonly Vector3 Center;public readonly float Radius;
        public ActivityCircle(string id,Vector3 center,float radius){Id=id;Center=center;Radius=radius;}
    }
    // ★ 修复（防Bug回归）：活动范围使用王后及补给点球面圆的并集，巡猎、领地和图层共用同一判定。
    public static class ColonyActivityRange
    {
        public static IEnumerable<ActivityCircle> Circles(PopulationBehavior population)
        {
            if(population==null||!population.Active)yield break;
            var queen=AntColonyMember.QueenFor(population);
            var leader=queen!=null?queen.Population:population.Leader;
            if(leader==null)yield break;
            float radius=queen!=null?queen.Radius:Mathf.Max(AntColonySettings.Current.minimumColonyRadius,leader.GetComponent<EntityGeneController>().GetAttribute("territory_radius"))*GameplayTuning.Current.colonyActivityRadiusMultiplier;
            yield return new ActivityCircle("leader",leader.transform.position,radius);
            if(queen==null)yield break;
            float supplyRadius=radius*GameplayTuning.Current.supplyActivityRadiusRatio;
            // ★ 修复（防Bug回归）：旧档里预先生成的空点不再扩张范围，耗尽的补给也立即失去活动圆。
            foreach(var resource in ColonyResource.All)
            {
                if(resource.State.group!=population.GroupId||resource.State.kind!=ColonyResourceKind.Supply&&resource.State.kind!=ColonyResourceKind.Nursery)continue;
                bool duplicate=Mathf.Approximately(radius,supplyRadius)&&Vector3.Distance(resource.transform.position,queen.transform.position)<.01f;
                if(!duplicate)yield return new ActivityCircle("resource-"+resource.State.id,resource.transform.position,supplyRadius);
            }
        }
        public static bool Contains(PopulationBehavior population,Vector3 position)
        {
            foreach(var circle in Circles(population))if(EcologyQueries.Distance(circle.Center,position)<=circle.Radius)return true;
            return false;
        }
        public static Vector3 Boundary(ActivityCircle circle,float angle,float ratio=1)
        {
            var up=EcologyQueries.Up(circle.Center);var axis=Vector3.Cross(up,Vector3.forward).normalized;if(axis.sqrMagnitude<.1f)axis=Vector3.Cross(up,Vector3.right).normalized;
            var tangent=Quaternion.AngleAxis(angle,up)*axis;var planet=EcologyQueries.Planet;
            if(planet==null)return circle.Center+tangent*circle.Radius*ratio;
            float radians=circle.Radius*ratio/Mathf.Max(planet.radius,planet.WaterRadius);
            return EcologyQueries.Surface(planet.transform.position+(up*Mathf.Cos(radians)+tangent*Mathf.Sin(radians))*Vector3.Distance(circle.Center,planet.transform.position));
        }
        public static Vector3 NearestCenter(PopulationBehavior population,Vector3 position)
        {
            float best=float.PositiveInfinity;Vector3 result=position;
            foreach(var circle in Circles(population)){float gap=EcologyQueries.Distance(circle.Center,position)-circle.Radius;if(gap<best){best=gap;result=circle.Center;}}
            return result;
        }
        public static ActivityCircle PatrolCircle(AntColonyMember member)
        {
            // 固定出生顺序分配巡逻中心，补给点不再全部无人巡逻。
            var circles=new List<ActivityCircle>(Circles(member.Population));
            return circles.Count==0?new ActivityCircle("leader",member.Center,member.Radius):circles[(int)(member.Population.JoinOrder%circles.Count)];
        }
    }
}
