using System.Collections.Generic;
using DarwinPark.Genetics;
using DarwinPark.Planet;
using UnityEngine;

namespace DarwinPark.Entities.Ecology
{
    public readonly struct EntityCoordinate
    {
        public readonly DarwinEntity entity;
        public readonly Vector3 position, localPosition;
        public readonly long revision;
        public EntityCoordinate(DarwinEntity target, Vector3 point) : this(target, point, EcologyQueries.ToLocal(point), 0) { }
        internal EntityCoordinate(DarwinEntity target, Vector3 point, Vector3 local, long version)
        { entity=target; position=point; localPosition=local; revision=version; }
        // ✦ 新增：快照地理来自采样坐标，不能泄露目标随后移动的位置。
        public bool TryGeography(out PlanetCoordinate coordinate)
        {
            var planet=EcologyQueries.Planet;
            coordinate=planet!=null&&planet.Data!=null?PlanetCoordinate.AtLocal(planet.Data,localPosition):default;
            return planet!=null&&planet.Data!=null;
        }
    }
    // ✦ 修复（Bug回归）：所有实体共用一个增量索引，跨格才调整桶成员。
    public static class EntityCoordinateGrid
    {
        private sealed class Entry { public EntityCoordinate coordinate; public Vector3 point; public Vector3Int cell; }
        private static readonly Dictionary<DarwinEntity,Entry> Entries=new();
        private static readonly Dictionary<Vector3Int,List<Entry>> Cells=new();
        private static readonly Dictionary<Vector3Int,long> Versions=new();
        private static readonly Stack<List<Entry>> Pool=new();
        private static float _size;
        private static Transform _root;
        private static Matrix4x4 _matrix;
        private static Matrix4x4 _worldToLocal=Matrix4x4.identity;
        private static bool _poseDirty;
        private static Quaternion _inverse=Quaternion.identity;
        private static Vector3 _origin;
        private static long _stamp,_baseline;
        public static long CandidateVisits {get;private set;}
        public static long UpdatedRecords {get;private set;}
        public static long BucketMoves {get;private set;}
        public static int Builds {get;private set;}
        public static int Count {get{EntityNeighborhood.Synchronize();return Entries.Count;}}
        private static Vector3 Point(Vector3 world)=>_inverse*(world-_origin);
        private static Vector3Int Cell(Vector3 point)=>new(Mathf.FloorToInt(point.x/_size),Mathf.FloorToInt(point.y/_size),Mathf.FloorToInt(point.z/_size));
        private static void Dirty(Vector3Int cell)
        {
            // 空格保留有限版本，目标离开仍使旧查询失效；超过容量时用 epoch 统一失效。
            if(Versions.Count>=ObservationSettings.Current.coordinateRevisionCellLimit){Versions.Clear();_baseline=++_stamp;}
            Versions[cell]=++_stamp;
        }
        private static void Add(Entry entry)
        {
            entry.cell=Cell(entry.point);
            if(!Cells.TryGetValue(entry.cell,out var list)){list=Pool.Count>0?Pool.Pop():new List<Entry>();Cells.Add(entry.cell,list);}
            list.Add(entry);Dirty(entry.cell);BucketMoves++;
        }
        private static void Detach(Entry entry)
        {
            if(Cells.TryGetValue(entry.cell,out var list)){list.Remove(entry);if(list.Count==0){Cells.Remove(entry.cell);Pool.Push(list);}}
            Dirty(entry.cell);
        }
        internal static bool Configure()
        {
            var planet=EcologyQueries.Planet;var root=planet!=null?planet.transform:null;
            var matrix=root!=null?root.localToWorldMatrix:Matrix4x4.identity;float size=ObservationSettings.Current.coordinateCellSize;
            bool changed=matrix!=_matrix||root!=_root||size!=_size,rebuild=size!=_size||root!=_root;
            _poseDirty|=changed;
            _root=root;_matrix=matrix;_size=size;_worldToLocal=root!=null?root.worldToLocalMatrix:Matrix4x4.identity;_inverse=root!=null?Quaternion.Inverse(root.rotation):Quaternion.identity;_origin=root!=null?root.position:Vector3.zero;
            if(rebuild)
            {
                foreach(var list in Cells.Values){list.Clear();Pool.Push(list);}Cells.Clear();Versions.Clear();_baseline=++_stamp;Builds++;
                foreach(var entry in Entries.Values){entry.point=Point(entry.coordinate.position);Add(entry);}
            }
            return _poseDirty;
        }
        internal static void CompleteSynchronization()=>_poseDirty=false;
        internal static void Upsert(DarwinEntity entity,Vector3 world,bool alive)
        {
            if(!alive){Remove(entity);return;}var point=Point(world);var local=_worldToLocal.MultiplyPoint3x4(world);
            if(!Entries.TryGetValue(entity,out var entry)){entry=new Entry{point=point};Entries.Add(entity,entry);Add(entry);}
            else
            {
                if(entry.coordinate.position==world&&entry.coordinate.localPosition==local&&entry.point==point)return;
                if(entry.cell!=Cell(point)){Detach(entry);entry.point=point;Add(entry);}else{entry.point=point;Dirty(entry.cell);}
            }
            entry.coordinate=new EntityCoordinate(entity,world,local,++_stamp);UpdatedRecords++;
        }
        internal static void Remove(DarwinEntity entity)
        {if(!ReferenceEquals(entity,null)&&Entries.Remove(entity,out var entry))Detach(entry);}
        internal static void Touch(DarwinEntity entity)
        {if(entity!=null&&Entries.TryGetValue(entity,out var entry))Dirty(entry.cell);}
        public static bool TryGet(DarwinEntity entity,out EntityCoordinate coordinate)
        {
            coordinate=default;if(entity==null)return false;EntityNeighborhood.ObservePosition(entity.Genes);
            if(!Entries.TryGetValue(entity,out var entry))return false;coordinate=entry.coordinate;return true;
        }
        public static void CopyPositions(List<EntityCoordinate> result)
        {EntityNeighborhood.Synchronize();result.Clear();foreach(var entry in Entries.Values)result.Add(entry.coordinate);}
        private static double Volume(Vector3Int min,Vector3Int max)=>((double)max.x-min.x+1)*((double)max.y-min.y+1)*((double)max.z-min.z+1);
        public static long QueryRevision(Vector3 world,float radius)
        {
            EntityNeighborhood.Synchronize();if(float.IsInfinity(radius))return _stamp;
            var point=Point(world);var min=Cell(point-Vector3.one*radius);var max=Cell(point+Vector3.one*radius);
            if(Volume(min,max)>ObservationSettings.Current.maximumCellsPerQuery)return _stamp;
            long latest=_baseline;for(int x=min.x;x<=max.x;x++)for(int y=min.y;y<=max.y;y++)for(int z=min.z;z<=max.z;z++)
                if(Versions.TryGetValue(new Vector3Int(x,y,z),out long stamp))latest=System.Math.Max(latest,stamp);
            return latest;
        }
        private static void Append(Entry entry,Vector3 point,float squared,List<EntityCoordinate> coordinates,List<DarwinEntity> entities)
        {CandidateVisits++;if((entry.point-point).sqrMagnitude>squared)return;coordinates?.Add(entry.coordinate);entities?.Add(entry.coordinate.entity);}
        private static void QueryCore(Vector3 world,float radius,List<EntityCoordinate> coordinates,List<DarwinEntity> entities)
        {
            coordinates?.Clear();entities?.Clear();if(radius<=0||float.IsNaN(radius))return;
            EntityNeighborhood.Synchronize();var point=Point(world);float squared=radius*radius;bool unlimited=float.IsPositiveInfinity(radius);
            var min=unlimited?default:Cell(point-Vector3.one*radius);var max=unlimited?default:Cell(point+Vector3.one*radius);
            if(unlimited||Volume(min,max)>System.Math.Min(Cells.Count,ObservationSettings.Current.maximumCellsPerQuery))
            {foreach(var entry in Entries.Values)Append(entry,point,squared,coordinates,entities);return;}
            for(int x=min.x;x<=max.x;x++)for(int y=min.y;y<=max.y;y++)for(int z=min.z;z<=max.z;z++)
                if(Cells.TryGetValue(new Vector3Int(x,y,z),out var list))foreach(var entry in list)Append(entry,point,squared,coordinates,entities);
        }
        public static void Query(Vector3 point,float radius,List<EntityCoordinate> result)
        {using var measured=DarwinPark.Core.PerformanceTimings.Measure(DarwinPark.Core.PerformanceTimings.Area.CoordinateGrid);QueryCore(point,radius,result,null);}
        public static void QueryEntities(Vector3 point,float radius,List<DarwinEntity> result)=>QueryCore(point,radius,null,result);
        public static void Clear()
        {
            Entries.Clear();Cells.Clear();Versions.Clear();Pool.Clear();_size=0;_root=null;_matrix=default;_inverse=Quaternion.identity;_origin=default;_baseline=++_stamp;
            _worldToLocal=Matrix4x4.identity;_poseDirty=false;Builds=0;UpdatedRecords=BucketMoves=CandidateVisits=0;EntityNeighborhood.InvalidateCoordinates();
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]private static void Reset()=>Clear();
    }
}
