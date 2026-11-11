using System.Collections.Generic;
using UnityEngine;

namespace DarwinPark.Entities.Ecology
{
    // ★ 修复（防Bug回归）：远食仅包含同群、通用块和附近无主粮；不能为每名工匠扫描所有其他活群的专用粮。
    public static class ColonyFoodCandidates
    {
        private static readonly List<ResourceCoordinate> Nearby = new();
        public static void Fill(AntColonyMember member, bool nearbyOnly, List<ResourceCoordinate> result)
        {
            using var timing=DarwinPark.Core.PerformanceTimings.Measure(DarwinPark.Core.PerformanceTimings.Area.FoodCandidates);
            var point = member.transform.position;
            if (nearbyOnly) { ResourceCoordinates.Query(point, AntColonySettings.Current.feedRadius, true, false, result); return; }
            result.Clear();
            foreach (var item in ResourceGroups.Read(member.GroupId))
                if (item.resource != null && item.resource.State.kind != ColonyResourceKind.Energy) result.Add(item);
            ResourceCoordinates.AppendKind(ColonyResourceKind.Energy, result);
            ResourceCoordinates.Query(point, AntColonySettings.Current.transportSearchRadius, true, false, Nearby);
            foreach (var item in Nearby)
                if (item.resource.State.kind != ColonyResourceKind.Energy && item.resource.State.group != member.GroupId && !ColonyResourceOwnership.HasOwner(item.resource.State.group)) result.Add(item);
            Nearby.Clear();
        }
    }
}
