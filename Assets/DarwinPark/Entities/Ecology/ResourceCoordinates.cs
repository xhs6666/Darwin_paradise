using System.Collections.Generic;
using DarwinPark.Core;
using UnityEngine;

namespace DarwinPark.Entities.Ecology
{
    public readonly struct ResourceCoordinate
    {
        public readonly ColonyResource resource;public readonly Vector3 position;
        public ResourceCoordinate(ColonyResource value,Vector3 point){resource=value;position=point;}
    }
    // ★ 新增：尸体和能量块统一登记坐标、分类桶；出生/移除/搬运更新，查询不再遍历场景对象。
    public static class ResourceCoordinates
    {
        private sealed class Entry {public ColonyResource resource;public Vector3 point;public Vector3Int cell,surfaceCell;public ColonyResourceKind kind;public string group;}
        private static readonly Dictionary<ColonyResource,Entry> Entries=new();
        private static readonly Dictionary<(Vector3Int,ColonyResourceKind,bool),List<Entry>> Cells=new();
        private static readonly Dictionary<ColonyResourceKind,List<Entry>> Kinds=new();
        private static readonly Stack<List<Entry>> Pool=new();private static readonly List<ColonyResource> Refresh=new();
        private static float _size,_surfaceRadius;private static Matrix4x4 _planet;private static Transform _root;
        private static long _foodRevision;
        public static long FoodRevision {get{Ensure();return _foodRevision;}}
        public static long Revision {get;private set;}public static long CandidateVisits {get;private set;}
        public static int Count=>Entries.Count;
        private static Vector3Int Cell(Vector3 p)=>new(Mathf.FloorToInt(p.x/_size),Mathf.FloorToInt(p.y/_size),Mathf.FloorToInt(p.z/_size));
        private static void Ensure()
        {
            var root=EcologyQueries.Planet!=null?EcologyQueries.Planet.transform:null;var matrix=root!=null?root.localToWorldMatrix:Matrix4x4.identity;float size=SimulationOptimization.Current.resourceCellSize;
            float surface=EcologyQueries.Planet!=null?Mathf.Max(EcologyQueries.Planet.radius,EcologyQueries.Planet.WaterRadius):0;
            if(_size==size&&_root==root&&_planet==matrix&&_surfaceRadius==surface)return;
            _surfaceRadius=surface;
            _size=size;_root=root;_planet=matrix;Refresh.Clear();foreach(var resource in Entries.Keys)Refresh.Add(resource);
            foreach(var list in Cells.Values){list.Clear();Pool.Push(list);}Cells.Clear();Kinds.Clear();Entries.Clear();
            foreach(var resource in Refresh)if(resource!=null&&resource.isActiveAndEnabled)Insert(resource);Refresh.Clear();Revision++;
        }
        private static Vector3 SurfacePoint(Vector3 point)
        {var planet=EcologyQueries.Planet;return planet==null?point:planet.transform.position+(point-planet.transform.position).normalized*Mathf.Max(planet.radius,planet.WaterRadius);}
        private static void Insert(ColonyResource resource,Entry reuse=null)
        {
            var entry=reuse??new Entry();entry.resource=resource;entry.point=resource.transform.position;entry.kind=resource.State.kind;entry.group=resource.State.group;entry.cell=Cell(entry.point);entry.surfaceCell=Cell(SurfacePoint(entry.point));Entries.Add(resource,entry);if(entry.kind!=ColonyResourceKind.Corpse)_foodRevision++;
            for(int i=0;i<2;i++){var key=(i==0?entry.cell:entry.surfaceCell,entry.kind,i==1);if(!Cells.TryGetValue(key,out var bucket)){bucket=Pool.Count>0?Pool.Pop():new();Cells.Add(key,bucket);}bucket.Add(entry);}
            if(!Kinds.TryGetValue(entry.kind,out var kind)){kind=new();Kinds.Add(entry.kind,kind);}kind.Add(entry);
        }
        public static void Observe(ColonyResource resource)
        {
            if(resource==null||resource.State==null)return;Ensure();
            if(Entries.TryGetValue(resource,out var old)&&old.point==resource.transform.position&&old.kind==resource.State.kind&&old.group==resource.State.group)return;
            Remove(resource);if(resource.isActiveAndEnabled&&resource.State.energy>0)Insert(resource,old);Revision++;
        }
        public static void Remove(ColonyResource resource)
        {
            if(ReferenceEquals(resource,null)||!Entries.TryGetValue(resource,out var entry))return;
            for(int i=0;i<2;i++){var key=(i==0?entry.cell:entry.surfaceCell,entry.kind,i==1);if(Cells.TryGetValue(key,out var bucket)){bucket.Remove(entry);if(bucket.Count==0){Cells.Remove(key);Pool.Push(bucket);}}}
            if(Kinds.TryGetValue(entry.kind,out var kind))kind.Remove(entry);Entries.Remove(resource);Revision++;if(entry.kind!=ColonyResourceKind.Corpse)_foodRevision++;
        }
        // ★ 适配：活体仍保留原3D搜索半径；资源球面查询使用投影格，避免高山同方向资源漏检。
        public static float BroadRadius(Vector3 point,float radius)=>radius;
        public static void Query(Vector3 point,float radius,bool surfaceDistance,bool corpses,List<ResourceCoordinate> result)
        {
            using var performance=PerformanceTimings.Measure(PerformanceTimings.Area.ResourceSearch);
            result.Clear();Ensure();float broad=radius;var center=surfaceDistance?SurfacePoint(point):point;var min=Cell(center-Vector3.one*broad);var max=Cell(center+Vector3.one*broad);
            double volume=((double)max.x-min.x+1)*((double)max.y-min.y+1)*((double)max.z-min.z+1);
            for(int k=0;k<4;k++)
            {
                var kind=(ColonyResourceKind)k;if((kind==ColonyResourceKind.Corpse)!=corpses)continue;
                if(volume>SimulationOptimization.Current.maximumResourceCells){if(Kinds.TryGetValue(kind,out var all))foreach(var entry in all)Add(entry,point,radius,surfaceDistance,result);continue;}
                for(int x=min.x;x<=max.x;x++)for(int y=min.y;y<=max.y;y++)for(int z=min.z;z<=max.z;z++)if(Cells.TryGetValue((new Vector3Int(x,y,z),kind,surfaceDistance),out var bucket))foreach(var entry in bucket)Add(entry,point,radius,surfaceDistance,result);
            }
        }
        private static void Add(Entry entry,Vector3 point,float radius,bool surface,List<ResourceCoordinate> result)
        {CandidateVisits++;if(entry.resource==null||!entry.resource.isActiveAndEnabled||entry.resource.State.energy<=0)return;if(float.IsPositiveInfinity(radius)||(surface?EcologyQueries.Distance(point,entry.point)<=radius:(point-entry.point).sqrMagnitude<=radius*radius))result.Add(new(entry.resource,entry.point));}
        public static void Food(List<ResourceCoordinate> result)
        {using var performance=PerformanceTimings.Measure(PerformanceTimings.Area.ResourceSearch);result.Clear();Ensure();foreach(var pair in Kinds)if(pair.Key!=ColonyResourceKind.Corpse)foreach(var entry in pair.Value){CandidateVisits++;if(entry.resource!=null&&entry.resource.isActiveAndEnabled&&entry.resource.State.energy>0)result.Add(new(entry.resource,entry.point));}}
        public static void Clear(){ResourceGroups.Clear();ActivityCircleCache.Clear();Entries.Clear();Cells.Clear();Kinds.Clear();Pool.Clear();Refresh.Clear();_root=null;_size=0;Revision++;_foodRevision++;CandidateVisits=0;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]private static void Reset()=>Clear();
    }
}
