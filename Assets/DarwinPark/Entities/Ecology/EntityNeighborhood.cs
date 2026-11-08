using System.Collections.Generic;
using DarwinPark.Entities.Stats;
using UnityEngine;

namespace DarwinPark.Entities.Ecology
{
    // ★ 新增：全体实体共享一份帧内空间快照；邻居循环只读数据，不重复访问Unity组件、基因和Transform。
    public static class EntityNeighborhood
    {
        private sealed class Entry
        {
            public DarwinEntity entity;public EntityGeneController genes;public EntityStats stats;
            public Vector3 position;public Vector3Int cell;public bool alive,producer;
            public long competitionVersion=-1;public float competition;
        }
        private static readonly Dictionary<EntityGeneController,Entry> Entries=new Dictionary<EntityGeneController,Entry>();
        private static readonly Dictionary<Vector3Int,List<Entry>> Cells=new Dictionary<Vector3Int,List<Entry>>();
        private static readonly Dictionary<Vector3Int,long> CellVersions=new();
        private static long _membership=-1,_change;
        private static readonly Stack<List<Entry>> Pool=new Stack<List<Entry>>();
        private static int _frame=-1;private static long _version,_positionVersion;private static float _cellSize;private static EcologyBalanceSettings _settings;
        public static int BuildCount {get;private set;}
        public static long PositionVersion{get{Ensure();return _positionVersion;}}
        private static Vector3Int Cell(Vector3 position)=>new Vector3Int(Mathf.FloorToInt(position.x/_cellSize),Mathf.FloorToInt(position.y/_cellSize),Mathf.FloorToInt(position.z/_cellSize));
        private static void AddCell(Entry entry)
        {
            entry.cell=Cell(entry.position);
            if(!Cells.TryGetValue(entry.cell,out var list)){list=Pool.Count>0?Pool.Pop():new List<Entry>();Cells.Add(entry.cell,list);}list.Add(entry);
        }
        private static void Dirty(Vector3Int cell)
        {
            long stamp=++_change;for(int x=-1;x<=1;x++)for(int y=-1;y<=1;y++)for(int z=-1;z<=1;z++)CellVersions[cell+new Vector3Int(x,y,z)]=stamp;
        }
        private static void RemoveCell(Entry entry)
        {if(Cells.TryGetValue(entry.cell,out var bucket)){bucket.Remove(entry);if(bucket.Count==0){Cells.Remove(entry.cell);Pool.Push(bucket);}}}
        private static void Refresh(Entry entry)
        {
            var previous=entry.position;bool alive=entry.alive,producer=entry.producer;var oldCell=entry.cell;
            entry.position=entry.entity.transform.position;if(entry.stats==null)entry.stats=entry.entity.GetComponent<EntityStats>();
            entry.alive=entry.entity.isActiveAndEnabled&&entry.stats!=null&&entry.stats.TryGet("health",out var hp)&&hp>0;
            entry.producer=entry.alive&&ProducerClassification.IsProducer(entry.genes);
            if(previous!=entry.position||alive!=entry.alive)_positionVersion++;
            if(previous!=entry.position||producer!=entry.producer||alive!=entry.alive){entry.competitionVersion=-1;_version++;if(producer||entry.producer){Dirty(oldCell);Dirty(Cell(entry.position));}}
            if(oldCell!=Cell(entry.position)){RemoveCell(entry);AddCell(entry);}
        }
        private static void Ensure()
        {
            if(_frame==Time.frameCount&&ReferenceEquals(_settings,EcologyBalanceSettings.Current))return;
            using var performance=DarwinPark.Core.PerformanceTimings.Measure(DarwinPark.Core.PerformanceTimings.Area.Neighborhood);
            // ★ 修复（防Bug回归）：静止桶跨帧保留，表达分类按版本缓存；不再每帧全部移出/加入空间桶。
            if(!ReferenceEquals(_settings,EcologyBalanceSettings.Current))
            {
                foreach(var list in Cells.Values){list.Clear();Pool.Push(list);}Cells.Clear();CellVersions.Clear();_settings=EcologyBalanceSettings.Current;_cellSize=_settings.competitionRadius;
                foreach(var entry in Entries.Values){entry.competitionVersion=-1;AddCell(entry);}_version++;_change++;
            }
            if(_membership!=EntityRegistry.MembershipRevision)
            {
                foreach(var entity in EntityRegistry.All)if(entity!=null&&entity.Genes!=null&&!Entries.ContainsKey(entity.Genes))
                {var entry=new Entry{entity=entity,genes=entity.Genes,position=entity.transform.position};Entries.Add(entity.Genes,entry);AddCell(entry);Refresh(entry);Dirty(entry.cell);}
                _membership=EntityRegistry.MembershipRevision;
            }
            foreach(var entry in Entries.Values)Refresh(entry);
            _frame=Time.frameCount;BuildCount++;
        }
        // ★ 适配：出生/停用改变注册表后，下次查询重建；初始化未结束时不提前锁定生产者分类。
        public static void MembershipChanged(DarwinEntity entity,bool removed)
        {
            // ★ 修复（防Bug回归）：离开世界后可能不再查询，注销时就释放桶内引用，不能等下一次重建才放开旧实体。
            if(removed&&entity!=null&&entity.Genes!=null&&Entries.TryGetValue(entity.Genes,out var entry))
            {if(entry.producer)Dirty(entry.cell);RemoveCell(entry);Entries.Remove(entity.Genes);ProducerClassification.Remove(entity.Genes);}
            _frame=-1;_version++;_positionVersion++;
        }
        // ★ 适配：在重算完成、生命归零/恢复以及模拟前同步，帧内表达变化和移动不等待下一次全表重建。
        public static void Changed(EntityGeneController genes)
        {
            if(genes==null||_frame!=Time.frameCount||!Entries.TryGetValue(genes,out var entry))return;
            Refresh(entry);
        }
        public static void ObservePosition(EntityGeneController genes)
        {if(_frame==Time.frameCount&&Entries.TryGetValue(genes,out var entry)&&entry.position!=genes.transform.position)Changed(genes);}
        public static float Competition(EntityGeneController genes)
        {
            Ensure();ObservePosition(genes);
            if(!Entries.TryGetValue(genes,out var self))return 1;
            long version=DarwinPark.Core.SimulationOptimization.Current.localCompetition?(CellVersions.TryGetValue(self.cell,out var local)?local:0):_version;
            if(self.competitionVersion==version)return self.competition;
            int count=0;var key=self.cell;float radius2=_cellSize*_cellSize;
            for(int x=-1;x<=1;x++)for(int y=-1;y<=1;y++)for(int z=-1;z<=1;z++)
                if(Cells.TryGetValue(key+new Vector3Int(x,y,z),out var list))foreach(var other in list)
                    if(other!=self&&other.producer&&(other.position-self.position).sqrMagnitude<=radius2)count++;
            self.competition=1+count*EcologyBalanceSettings.Current.competitionPerNeighbor;self.competitionVersion=version;return self.competition;
        }
        public static float ProductionCompetition(EntityGeneController genes)
        {Ensure();return Entries.TryGetValue(genes,out var entry)&&entry.producer?Competition(genes):1;}
        // ★ 适配：感知复用唯一坐标表，不为每个观察者重新扫描所有Transform。
        public static bool TryPosition(DarwinEntity entity,out Vector3 point)
        {Ensure();point=default;if(entity==null||entity.Genes==null||!Entries.TryGetValue(entity.Genes,out var entry)||!entry.alive)return false;ObservePosition(entity.Genes);point=entry.position;return true;}
        public static void CopyPositions(List<EntityCoordinate> result)
        {Ensure();result.Clear();foreach(var entry in Entries.Values)if(entry.alive)result.Add(new EntityCoordinate(entry.entity,entry.position));}
        // ★ 新增：感知/食物压力复用同一索引；大半径改扫紧凑记录，避免遍历几万个空格子。
        public static void Query(Vector3 position,float radius,List<DarwinEntity> result)
        {
            result.Clear();if(radius<=0)return;Ensure();float radius2=radius*radius;
            var min=Cell(position-Vector3.one*radius);var max=Cell(position+Vector3.one*radius);
            long volume=(long)(max.x-min.x+1)*(max.y-min.y+1)*(max.z-min.z+1);
            if(volume>Cells.Count)
            {foreach(var entry in Entries.Values)if(entry.alive&&(entry.position-position).sqrMagnitude<=radius2)result.Add(entry.entity);return;}
            for(int x=min.x;x<=max.x;x++)for(int y=min.y;y<=max.y;y++)for(int z=min.z;z<=max.z;z++)
                if(Cells.TryGetValue(new Vector3Int(x,y,z),out var list))foreach(var entry in list)
                    if(entry.alive&&(entry.position-position).sqrMagnitude<=radius2)result.Add(entry.entity);
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Clear(){Entries.Clear();Cells.Clear();Pool.Clear();CellVersions.Clear();ProducerClassification.Clear();_membership=-1;_frame=-1;_version++;_positionVersion++;BuildCount=0;_settings=null;}
    }
}
