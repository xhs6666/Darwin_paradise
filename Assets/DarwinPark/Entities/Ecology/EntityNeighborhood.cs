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
        private static readonly Stack<List<Entry>> Pool=new Stack<List<Entry>>();
        private static int _frame=-1;private static long _version;private static float _cellSize;private static EcologyBalanceSettings _settings;
        public static int BuildCount {get;private set;}
        private static Vector3Int Cell(Vector3 position)=>new Vector3Int(Mathf.FloorToInt(position.x/_cellSize),Mathf.FloorToInt(position.y/_cellSize),Mathf.FloorToInt(position.z/_cellSize));
        private static void AddCell(Entry entry)
        {
            entry.cell=Cell(entry.position);
            if(!Cells.TryGetValue(entry.cell,out var list)){list=Pool.Count>0?Pool.Pop():new List<Entry>();Cells.Add(entry.cell,list);}list.Add(entry);
        }
        private static void Refresh(Entry entry)
        {
            entry.position=entry.entity.transform.position;
            if(entry.stats==null)entry.stats=entry.entity.GetComponent<EntityStats>();
            entry.alive=entry.entity.isActiveAndEnabled&&entry.stats!=null&&entry.stats.TryGet("health",out var hp)&&hp>0;
            entry.producer=entry.alive&&EcologyEnergyBudget.IsProducer(entry.genes);
        }
        private static void Ensure()
        {
            if(_frame==Time.frameCount&&ReferenceEquals(_settings,EcologyBalanceSettings.Current))return;
            foreach(var list in Cells.Values){list.Clear();Pool.Push(list);}Cells.Clear();
            // ★ 修复（防Bug回归）：Clear、卸载和禁用同步清掉引用；普通帧复用Entry和桶，避免随实体数量产生逐帧垃圾。
            _settings=EcologyBalanceSettings.Current;_cellSize=_settings.competitionRadius;
            foreach(var entity in EntityRegistry.All)
            {
                if(entity==null||entity.Genes==null)continue;
                if(!Entries.TryGetValue(entity.Genes,out var entry)){entry=new Entry{entity=entity,genes=entity.Genes};Entries.Add(entity.Genes,entry);}
                Refresh(entry);AddCell(entry);
            }
            _frame=Time.frameCount;_version++;BuildCount++;
        }
        // ★ 适配：出生/停用改变注册表后，下次查询重建；初始化未结束时不提前锁定生产者分类。
        public static void MembershipChanged(DarwinEntity entity,bool removed)
        {
            // ★ 修复（防Bug回归）：离开世界后可能不再查询，注销时就释放桶内引用，不能等下一次重建才放开旧实体。
            if(removed&&entity!=null&&entity.Genes!=null&&Entries.TryGetValue(entity.Genes,out var entry))
            {if(Cells.TryGetValue(entry.cell,out var bucket))bucket.Remove(entry);Entries.Remove(entity.Genes);}
            _frame=-1;_version++;
        }
        // ★ 适配：在重算完成、生命归零/恢复以及模拟前同步，帧内表达变化和移动不等待下一次全表重建。
        public static void Changed(EntityGeneController genes)
        {
            if(genes==null||_frame!=Time.frameCount||!Entries.TryGetValue(genes,out var entry))return;
            var oldCell=entry.cell;Refresh(entry);var newCell=Cell(entry.position);
            if(oldCell!=newCell){Cells[oldCell].Remove(entry);AddCell(entry);}_version++;
        }
        public static void ObservePosition(EntityGeneController genes)
        {if(_frame==Time.frameCount&&Entries.TryGetValue(genes,out var entry)&&entry.position!=genes.transform.position)Changed(genes);}
        public static float Competition(EntityGeneController genes)
        {
            Ensure();ObservePosition(genes);
            if(!Entries.TryGetValue(genes,out var self))return 1;
            if(self.competitionVersion==_version)return self.competition;
            int count=0;var key=self.cell;float radius2=_cellSize*_cellSize;
            for(int x=-1;x<=1;x++)for(int y=-1;y<=1;y++)for(int z=-1;z<=1;z++)
                if(Cells.TryGetValue(key+new Vector3Int(x,y,z),out var list))foreach(var other in list)
                    if(other!=self&&other.producer&&(other.position-self.position).sqrMagnitude<=radius2)count++;
            self.competition=1+count*EcologyBalanceSettings.Current.competitionPerNeighbor;self.competitionVersion=_version;return self.competition;
        }
        public static float ProductionCompetition(EntityGeneController genes)
        {Ensure();return Entries.TryGetValue(genes,out var entry)&&entry.producer?Competition(genes):1;}
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
        public static void Clear(){Entries.Clear();Cells.Clear();Pool.Clear();_frame=-1;_version++;BuildCount=0;_settings=null;}
    }
}
