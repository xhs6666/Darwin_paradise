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
        private sealed class Entry {public ColonyResource resource;public Vector3 point,direction;public Vector3Int cell,surfaceCell;public ColonyResourceKind kind;public string group;}
        private static readonly Dictionary<ColonyResource,Entry> Entries=new();
        private static readonly Dictionary<(Vector3Int,ColonyResourceKind,bool),List<Entry>> Cells=new();
        private static readonly Dictionary<ColonyResourceKind,List<Entry>> Kinds=new();
        private static readonly Stack<List<Entry>> Pool=new();private static readonly List<ColonyResource> Refresh=new();
        private static float _size,_surfaceRadius;private static Matrix4x4 _planet;private static Transform _root;
        // ★ 修复（防Bug回归）：索引随星球旋转的刚性坐标系保存；自转只换读出姿态，不重建数千资源桶。
        private static Quaternion _rotation=Quaternion.identity,_inverse=Quaternion.identity;private static Vector3 _origin;private static Matrix4x4 _shape;
        public static long PoseRevision {get{Ensure();return _poseRevision;}}private static long _poseRevision;
        public static long Rebuilds {get;private set;}
        public static Vector3 RelativePoint(Vector3 world){Ensure();return IndexPoint(world);}
        private static Vector3 IndexPoint(Vector3 world)=>_root!=null?_inverse*(world-_origin):world;
        private static Vector3 WorldPoint(Vector3 point)=>_root!=null?_rotation*point+_origin:point;
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
            var rotation=root!=null?root.rotation:Quaternion.identity;var inverse=Quaternion.Inverse(rotation);var shape=Matrix4x4.Rotate(inverse)*matrix;shape.m03=shape.m13=shape.m23=0;
            bool rigid=_size==size&&_root==root&&_surfaceRadius==surface&&_shape==shape;
            _rotation=rotation;_inverse=inverse;_origin=root!=null?root.position:Vector3.zero;_shape=shape;_poseRevision++;
            _surfaceRadius=surface;_size=size;_root=root;_planet=matrix;
            if(rigid){Revision++;return;}
            Rebuilds++;_foodRevision++;Refresh.Clear();foreach(var resource in Entries.Keys)Refresh.Add(resource);
            foreach(var list in Cells.Values){list.Clear();Pool.Push(list);}Cells.Clear();Kinds.Clear();Entries.Clear();
            foreach(var resource in Refresh)if(resource!=null&&resource.isActiveAndEnabled)Insert(resource);Refresh.Clear();Revision++;
        }
        private static Vector3 SurfacePoint(Vector3 point)
        =>_root==null?point:point.normalized*_surfaceRadius;
        private static void Insert(ColonyResource resource,Entry reuse=null)
        {
            var entry=reuse??new Entry();entry.resource=resource;entry.point=IndexPoint(resource.transform.position);entry.direction=entry.point.normalized;entry.kind=resource.State.kind;entry.group=resource.State.group;entry.cell=Cell(entry.point);entry.surfaceCell=Cell(SurfacePoint(entry.point));Entries.Add(resource,entry);if(entry.kind!=ColonyResourceKind.Corpse)_foodRevision++;
            for(int i=0;i<2;i++){var key=(i==0?entry.cell:entry.surfaceCell,entry.kind,i==1);if(!Cells.TryGetValue(key,out var bucket)){bucket=Pool.Count>0?Pool.Pop():new();Cells.Add(key,bucket);}bucket.Add(entry);}
            if(!Kinds.TryGetValue(entry.kind,out var kind)){kind=new();Kinds.Add(entry.kind,kind);}kind.Add(entry);
        }
        public static void Observe(ColonyResource resource)
        {
            if(resource==null||resource.State==null)return;Ensure();
            if(Entries.TryGetValue(resource,out var old)&&old.point==IndexPoint(resource.transform.position)&&old.kind==resource.State.kind&&old.group==resource.State.group)return;
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
            result.Clear();Ensure();point=IndexPoint(point);float broad=radius;bool unlimited=float.IsPositiveInfinity(radius);var center=surfaceDistance?SurfacePoint(point):point;var min=unlimited?default:Cell(center-Vector3.one*broad);var max=unlimited?default:Cell(center+Vector3.one*broad);
            // ★ 修复（防Bug回归）：同一次搜索共用球心/半径和单位方向，不能对每个候选反复读取星球Transform并归一化两次。
            bool spherical=surfaceDistance&&_root!=null;var direction=spherical?point.normalized:Vector3.zero;
            double volume=unlimited?double.PositiveInfinity:((double)max.x-min.x+1)*((double)max.y-min.y+1)*((double)max.z-min.z+1);
            for(int k=0;k<4;k++)
            {
                var kind=(ColonyResourceKind)k;if((kind==ColonyResourceKind.Corpse)!=corpses)continue;
                if(!Kinds.TryGetValue(kind,out var all)||all.Count==0)continue;
                // 稀疏食物不足百个时不查上千个空格；两种路径仍使用相同的精确距离判断。
                if(volume>SimulationOptimization.Current.maximumResourceCells||volume>all.Count){foreach(var entry in all)Add(entry,point,direction,radius,spherical,result);continue;}
                for(int x=min.x;x<=max.x;x++)for(int y=min.y;y<=max.y;y++)for(int z=min.z;z<=max.z;z++)if(Cells.TryGetValue((new Vector3Int(x,y,z),kind,surfaceDistance),out var bucket))foreach(var entry in bucket)Add(entry,point,direction,radius,spherical,result);
            }
        }
        private static void Add(Entry entry,Vector3 point,Vector3 direction,float radius,bool spherical,List<ResourceCoordinate> result)
        {CandidateVisits++;if(entry.resource==null||!entry.resource.isActiveAndEnabled||entry.resource.State.energy<=0)return;if(float.IsPositiveInfinity(radius)||(spherical?Mathf.Acos(Mathf.Clamp(Vector3.Dot(direction,entry.direction),-1,1))*_surfaceRadius<=radius:(point-entry.point).sqrMagnitude<=radius*radius))result.Add(new(entry.resource,WorldPoint(entry.point)));}
        // ★ 新增：分类追加用于远食候选，不接触其他活群的专用食物桶。
        public static void AppendKind(ColonyResourceKind kind,List<ResourceCoordinate> result)
        {Ensure();if(Kinds.TryGetValue(kind,out var list))foreach(var entry in list){CandidateVisits++;if(entry.resource!=null&&entry.resource.isActiveAndEnabled&&entry.resource.State.energy>0)result.Add(new(entry.resource,WorldPoint(entry.point)));}}
        public static void Food(List<ResourceCoordinate> result)
        {using var performance=PerformanceTimings.Measure(PerformanceTimings.Area.ResourceSearch);result.Clear();Ensure();foreach(var pair in Kinds)if(pair.Key!=ColonyResourceKind.Corpse)foreach(var entry in pair.Value){CandidateVisits++;if(entry.resource!=null&&entry.resource.isActiveAndEnabled&&entry.resource.State.energy>0)result.Add(new(entry.resource,WorldPoint(entry.point)));}}
        public static void Clear(){ResourceGroups.Clear();ActivityCircleCache.Clear();Entries.Clear();Cells.Clear();Kinds.Clear();Pool.Clear();Refresh.Clear();_root=null;_size=0;_rotation=_inverse=Quaternion.identity;_origin=Vector3.zero;_shape=default;Rebuilds=0;_poseRevision++;Revision++;_foodRevision++;CandidateVisits=0;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]private static void Reset()=>Clear();
    }
}
