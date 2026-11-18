using System.Runtime.CompilerServices;
using DarwinPark.Planet;
using UnityEngine;
namespace DarwinPark.Entities.Ecology
{
    // ★ 新增：目标选择复用地表连通区，拒绝隔海食物/猎物，避免任务永远锁在无法抵达的目标。
    public static class LandReachability
    {
        private sealed class Entry {public LandNavigationGraph graph;public Vector3 point;public Matrix4x4 matrix;public int region;}
        private static ConditionalWeakTable<Object,Entry> _entries=new();
        private static int Region(Object owner,Vector3 point,PlanetTerrainMeshBuilder planet)
        {var graph=planet.Data.Navigation;var matrix=planet.transform.worldToLocalMatrix;var entry=_entries.GetOrCreateValue(owner);if(entry.graph!=graph||entry.point!=point||entry.matrix!=matrix){entry.graph=graph;entry.point=point;entry.matrix=matrix;int cell=graph.Closest(matrix.MultiplyPoint3x4(point).normalized);entry.region=cell<0?0:graph.Regions[cell];}return entry.region;}
        public static bool CanReach(EntityGeneController genes,ColonyResource resource)
        {var planet=EcologyQueries.Planet;if(planet?.Data?.Navigation==null||genes.GetAttribute("swimSpeed")>0||genes.Evaluation==null||!genes.Evaluation.Attributes.TryGetValue(EcologyBalanceSettings.Current.movementAttribute,out var speed)||speed<=0)return true;int a=Region(genes,genes.transform.position,planet);return a>0&&a==Region(resource,resource.transform.position,planet);}
        public static bool CanReach(EntityGeneController genes,DarwinEntity target)
        {var planet=EcologyQueries.Planet;if(planet?.Data?.Navigation==null||genes.GetAttribute("swimSpeed")>0)return true;int a=Region(genes,genes.transform.position,planet);return a>0&&a==Region(target,target.transform.position,planet);}
        // ✦ 修复（Bug回归）：感知选目标时使用已观察坐标，不从可达性判断旁路读取目标的新位置。
        public static bool CanReach(EntityGeneController genes,DarwinEntity target,Vector3 observedPosition)
        {var planet=EcologyQueries.Planet;if(planet?.Data?.Navigation==null||genes.GetAttribute("swimSpeed")>0)return true;int a=Region(genes,genes.transform.position,planet);return a>0&&a==Region(target,observedPosition,planet);}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]private static void Reset()=>_entries=new();
    }
}
