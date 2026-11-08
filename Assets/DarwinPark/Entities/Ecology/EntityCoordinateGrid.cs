using System.Collections.Generic;
using DarwinPark.Genetics;
using UnityEngine;
namespace DarwinPark.Entities.Ecology
{
    public readonly struct EntityCoordinate
    {
        public readonly DarwinEntity entity;public readonly Vector3 position;
        public EntityCoordinate(DarwinEntity target,Vector3 point){entity=target;position=point;}
    }
    // ★ 新增：复用唯一坐标快照构建适合视野大小的粗格路由，避免营养小格查询在大视野时退回每观察者全表扫描。
    public static class EntityCoordinateGrid
    {
        private static readonly List<EntityCoordinate> Positions=new();private static readonly Dictionary<Vector3Int,List<EntityCoordinate>> Cells=new();private static readonly Stack<List<EntityCoordinate>> Pool=new();
        private static long _version=-1;private static float _size;
        public static long CandidateVisits {get;private set;}public static int Builds {get;private set;}
        private static Vector3Int Cell(Vector3 point)=>new(Mathf.FloorToInt(point.x/_size),Mathf.FloorToInt(point.y/_size),Mathf.FloorToInt(point.z/_size));
        private static void Ensure()
        {
            long version=EntityNeighborhood.PositionVersion;float size=ObservationSettings.Current.coordinateCellSize;
            if(_version==version&&_size==size)return;
            foreach(var values in Cells.Values){values.Clear();Pool.Push(values);}Cells.Clear();_size=size;EntityNeighborhood.CopyPositions(Positions);
            foreach(var position in Positions){var cell=Cell(position.position);if(!Cells.TryGetValue(cell,out var list)){list=Pool.Count>0?Pool.Pop():new List<EntityCoordinate>();Cells.Add(cell,list);}list.Add(position);}
            _version=EntityNeighborhood.PositionVersion;Builds++;
        }
        public static void Query(Vector3 point,float radius,List<EntityCoordinate> result)
        {
            result.Clear();if(radius<=0)return;Ensure();var min=Cell(point-Vector3.one*radius);var max=Cell(point+Vector3.one*radius);double volume=((double)max.x-min.x+1)*((double)max.y-min.y+1)*((double)max.z-min.z+1);float squared=radius*radius;
            // 极大视野才扫描紧凑坐标；正常视野只走周围粗格，既不做逐目标Unity组件查询，也不广播全地图。
            if(volume>ObservationSettings.Current.maximumCellsPerQuery){foreach(var item in Positions){CandidateVisits++;if((item.position-point).sqrMagnitude<=squared)result.Add(item);}return;}
            for(int x=min.x;x<=max.x;x++)for(int y=min.y;y<=max.y;y++)for(int z=min.z;z<=max.z;z++)if(Cells.TryGetValue(new Vector3Int(x,y,z),out var list))foreach(var item in list){CandidateVisits++;if((item.position-point).sqrMagnitude<=squared)result.Add(item);}
        }
        public static void Clear(){Positions.Clear();Cells.Clear();Pool.Clear();_version=-1;Builds=0;CandidateVisits=0;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]private static void Reset()=>Clear();
    }
}
