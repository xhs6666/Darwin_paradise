using System.Collections.Generic;
using DarwinPark.Entities.Stats;
using UnityEngine;

namespace DarwinPark.Entities.Ecology
{
    // ✦ 修复（Bug回归）：邻居、竞争与感知共用 EntityCoordinateGrid；这里只缓存存活/生产者等语义。
    public static class EntityNeighborhood
    {
        private sealed class Entry
        {
            public DarwinEntity entity; public EntityGeneController genes; public EntityStats stats; public Transform transform;
            public Vector3 position; public bool alive,producer,refreshing;
            public long competitionVersion=-1; public float competition;
            public List<DarwinEntity> neighbors;
        }
        private static readonly Dictionary<EntityGeneController,Entry> Entries=new();
        private static readonly List<Entry> Initializing=new();
        private static long _membership=-1,_version;
        private static int _frame=-1;
        private static bool _syncing;
        public static int BuildCount {get;private set;}
        public static long PositionVersion {get{Synchronize();return _version;}}
        internal static void InvalidateCoordinates(){_frame=-1;_version++;}
        private static void Refresh(Entry entry)
        {
            if(entry.refreshing)return;entry.refreshing=true;
            try
            {
                var position=entry.transform.position;
                if(entry.stats==null)entry.stats=entry.entity.GetComponent<EntityStats>();
                bool alive=entry.entity.isActiveAndEnabled&&entry.stats!=null&&entry.stats.TryGet("health",out var hp)&&hp>0;
                bool producer=alive&&ProducerClassification.IsProducer(entry.genes);
                bool semantics=producer!=entry.producer||alive!=entry.alive;
                if(position!=entry.position||semantics){entry.competitionVersion=-1;_version++;}
                entry.position=position;entry.alive=alive;entry.producer=producer;
                EntityCoordinateGrid.Upsert(entry.entity,position,alive);
                if(semantics)EntityCoordinateGrid.Touch(entry.entity);
            }
            finally{entry.refreshing=false;}
        }
        private static void Add(DarwinEntity entity)
        {
            if(entity==null||entity.Genes==null||Entries.ContainsKey(entity.Genes))return;
            var entry=new Entry{entity=entity,genes=entity.Genes,transform=entity.transform};
            Entries.Add(entity.Genes,entry);Refresh(entry);if(entry.stats==null)Initializing.Add(entry);
        }
        public static void Synchronize()
        {
            if(_syncing)return;
            bool poseChanged=EntityCoordinateGrid.Configure();
            if(_frame==Time.frameCount&&_membership==EntityRegistry.MembershipRevision&&!poseChanged&&Initializing.Count==0)return;
            using var measured=DarwinPark.Core.PerformanceTimings.Measure(DarwinPark.Core.PerformanceTimings.Area.Neighborhood);
            _syncing=true;
            try
            {
                // Awake 登记可能早于 Stats 安装；只补采待初始化者，不因一次出生再扫描所有实体。
                if(_frame==Time.frameCount&&_membership==EntityRegistry.MembershipRevision&&!poseChanged)
                {
                    for(int i=Initializing.Count-1;i>=0;i--){Refresh(Initializing[i]);if(Initializing[i].stats!=null)Initializing.RemoveAt(i);}
                    return;
                }
                if(_membership!=EntityRegistry.MembershipRevision)
                {foreach(var entity in EntityRegistry.All)Add(entity);_membership=EntityRegistry.MembershipRevision;}
                // PhysX/传送可绕过 AI；每帧一次统一采集保证兼容，不再为每个查询全量重建空间桶。
                foreach(var entry in Entries.Values)Refresh(entry);
                for(int i=Initializing.Count-1;i>=0;i--)if(Initializing[i].stats!=null)Initializing.RemoveAt(i);
                if(poseChanged)_version++;
                EntityCoordinateGrid.CompleteSynchronization();
                _frame=Time.frameCount;BuildCount++;
            }
            finally{_syncing=false;}
        }
        public static void MembershipChanged(DarwinEntity entity,bool removed)
        {
            if(removed)
            {
                if(entity!=null&&entity.Genes!=null){Entries.Remove(entity.Genes);for(int i=Initializing.Count-1;i>=0;i--)if(Initializing[i].genes==entity.Genes)Initializing.RemoveAt(i);}
                EntityCoordinateGrid.Remove(entity);
                if(entity!=null)ProducerClassification.Remove(entity.Genes);
            }
            else
            {
                EntityCoordinateGrid.Configure();Add(entity);
            }
            // 直接登记，避免每次出生/死亡后又遍历所有注册实体。
            _membership=EntityRegistry.MembershipRevision;_version++;
        }
        // ✦ 新增：属性编辑/搬运/传送同帧可见；物理位移由下一次统一采集覆盖。
        public static void Changed(EntityGeneController genes)
        {
            if(genes==null)return;Synchronize();
            if(Entries.TryGetValue(genes,out var entry))Refresh(entry);
        }
        public static void ObservePosition(EntityGeneController genes)
        {
            if(genes==null)return;Synchronize();
            if(Entries.TryGetValue(genes,out var entry)&&(entry.position!=entry.transform.position||entry.stats==null||!entry.alive))Refresh(entry);
        }
        public static float Competition(EntityGeneController genes)
        {
            Synchronize();ObservePosition(genes);
            if(genes==null||!Entries.TryGetValue(genes,out var self))return 1;
            var settings=EcologyBalanceSettings.Current;
            long version=DarwinPark.Core.SimulationOptimization.Current.localCompetition?EntityCoordinateGrid.QueryRevision(self.position,settings.competitionRadius):_version;
            if(self.competitionVersion==version)return self.competition;
            self.neighbors??=new List<DarwinEntity>();EntityCoordinateGrid.QueryEntities(self.position,settings.competitionRadius,self.neighbors);
            int count=0;foreach(var other in self.neighbors)
                if(other!=self.entity&&Entries.TryGetValue(other.Genes,out var candidate)&&candidate.alive&&candidate.producer)count++;
            self.neighbors.Clear();self.competition=1+count*settings.competitionPerNeighbor;self.competitionVersion=version;
            return self.competition;
        }
        public static float ProductionCompetition(EntityGeneController genes)
        {Synchronize();return genes!=null&&Entries.TryGetValue(genes,out var entry)&&entry.producer?Competition(genes):1;}
        public static bool TryPosition(DarwinEntity entity,out Vector3 point)
        {bool found=EntityCoordinateGrid.TryGet(entity,out var coordinate);point=coordinate.position;return found;}
        public static void CopyPositions(List<EntityCoordinate> result)=>EntityCoordinateGrid.CopyPositions(result);
        public static void Query(Vector3 position,float radius,List<DarwinEntity> result)=>EntityCoordinateGrid.QueryEntities(position,radius,result);
        public static void Clear()
        {
            Entries.Clear();Initializing.Clear();ProducerClassification.Clear();_membership=-1;_frame=-1;_syncing=false;_version++;BuildCount=0;
            EntityCoordinateGrid.Clear();
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]private static void Reset()=>Clear();
    }
}
