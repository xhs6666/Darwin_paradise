using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using DarwinPark.Core;
using DarwinPark.Entities.Ecology;
using DarwinPark.Entities.Stats;
using DarwinPark.Entities.Vitals;
using UnityEngine;

namespace DarwinPark.Entities.Simulation
{
    // ★ 新增：由GameBootstrap运行时挂载。交互/受伤实体先算，静止近邻共享基础变化，个体身份和繁殖不合并。
    [DefaultExecutionOrder(-100)] public sealed class EntityWorkScheduler:MonoBehaviour
    {
        private sealed class Entry
        {
            public EntityGeneController genes;public EntityStats stats;public EntityLifeCycle life;
            public EntityVitals vitals;public EntityEnvironmentHazard environment;
            public Vector3 position;public float quiet;public float pending {get=>genes.PendingSimulationTime;set=>genes.PendingSimulationTime=value;}public int revision=-1,frame=-1,ordinal,count;public bool dormant;
            public string signature,stateId;public bool supported;
        }
        private sealed class Cohort {public readonly List<Entry> members=new List<Entry>();public float time,priority;}
        private static EntityWorkScheduler _instance;
        private readonly Dictionary<EntityGeneController,Entry> _entries=new Dictionary<EntityGeneController,Entry>();
        private readonly List<Entry> _active=new List<Entry>();
        private readonly List<Cohort> _groups=new List<Cohort>();
        private readonly Dictionary<(Vector3Int,string,AgeState,bool,int,int,float,float,float),List<Cohort>> _cells=new Dictionary<(Vector3Int,string,AgeState,bool,int,int,float,float,float),List<Cohort>>();
        private long _membership=-1;
        public long RepresentativeSteps {get;private set;}
        public long SharedMemberSteps {get;private set;}
        public int DormantMembers {get;private set;}
        public static void Ensure(GameObject owner){if(_instance==null)owner.AddComponent<EntityWorkScheduler>();}
        private void Awake(){if(_instance!=null&&_instance!=this){Destroy(this);return;}_instance=this;}
        private void OnDestroy(){if(_instance==this)_instance=null;_entries.Clear();}
        public static bool Handled(EntityGeneController genes)=>_instance!=null&&_instance.isActiveAndEnabled&&_instance._entries.TryGetValue(genes,out var entry)&&entry.frame==Time.frameCount;
        public static bool Dormant(EntityGeneController genes)=>Handled(genes)&&_instance._entries[genes].dormant;
        public static void Wake(EntityGeneController genes){if(genes!=null&&_instance!=null&&_instance._entries.TryGetValue(genes,out var entry)){entry.quiet=0;entry.revision=-1;}}
        public static void AdvanceUnscheduled(EntityGeneController genes,float dt)
        {
            if(dt<=0||WorldAuthority.IsReplica)return;
            float owed=Mathf.Min(genes.PendingSimulationTime,GameplayTuning.Current.mergeMaximumCatchupSeconds);genes.PendingSimulationTime-=owed;
            genes.AdvanceSimulation(dt+owed);
            if(owed>0&&genes!=null&&genes.gameObject.activeInHierarchy){StationaryCohortKernel.AdvanceCore(genes,owed);}
        }
        public static bool TryCohortOrdinal(EntityGeneController genes,out int ordinal,out int count)
        {
            ordinal=0;count=1;if(!Handled(genes))return false;var entry=_instance._entries[genes];ordinal=entry.ordinal;count=entry.count;return count>1;
        }
        private Entry Get(EntityGeneController genes)
        {
            if(_entries.TryGetValue(genes,out var entry))return entry;
            entry=new Entry{genes=genes,stats=genes.GetComponent<EntityStats>(),life=genes.GetComponent<EntityLifeCycle>(),vitals=genes.GetComponent<EntityVitals>(),environment=genes.GetComponent<EntityEnvironmentHazard>(),position=genes.transform.position};
            _entries.Add(genes,entry);return entry;
        }
        private void Update()=>Advance(ServiceLocator.TryGet<SimulationClock>(out var clock)?clock.GameDeltaTime:Time.deltaTime);
        public void Advance(float dt)
        {
            if(dt<=0||WorldAuthority.IsReplica)return;
            if(_membership!=EntityRegistry.MembershipRevision)
            {
                var present=new HashSet<EntityGeneController>(EntityRegistry.All.Where(e=>e!=null).Select(e=>e.Genes));
                foreach(var key in _entries.Keys.Where(g=>g==null||!present.Contains(g)).ToArray())_entries.Remove(key);
                foreach(var genes in present)if(genes!=null)Get(genes);
                _membership=EntityRegistry.MembershipRevision;
            }
            _active.Clear();_groups.Clear();_cells.Clear();DormantMembers=0;
            var tuning=GameplayTuning.Current;
            // ★ 修复（防Bug回归）：出生/死亡会改注册表，推进前先完成调度快照，不能边枚举边生成。
            foreach(var entry in _entries.Values)
            {
                var genes=entry.genes;if(genes==null||!genes.isActiveAndEnabled)continue;
                if(entry.life==null)entry.life=genes.GetComponent<EntityLifeCycle>();
                entry.frame=Time.frameCount;entry.ordinal=0;entry.count=1;
                bool stationary=(genes.transform.position-entry.position).sqrMagnitude<=tuning.mergeWakeDistance*tuning.mergeWakeDistance;
                entry.position=genes.transform.position;
                if(entry.revision!=genes.LogicRevision)
                {entry.revision=genes.LogicRevision;entry.signature=Signature(genes);entry.supported=StationaryCohortKernel.Supported(genes);entry.quiet=0;}
                string currentState=genes.GetComponent<EntityStateController>()?.Current?.id??"";
                if(entry.stateId!=currentState){entry.stateId=currentState;entry.quiet=0;}
                bool eligible=tuning.mergeStationaryEntities&&stationary&&Eligible(entry);
                entry.quiet=eligible?entry.quiet+dt:0;
                entry.dormant=eligible&&entry.quiet>=tuning.mergeQuietSeconds;
                if(!entry.dormant){_active.Add(entry);continue;}
                DormantMembers++;entry.pending+=dt;
                if(entry.pending+0.00001f>=tuning.mergeInterval)AddGroup(entry,tuning);
            }
            _active.Sort((a,b)=>Priority(b).CompareTo(Priority(a)));
            foreach(var entry in _active)
            {
                // ★ 修复（防Bug回归）：高优先级攻击可能已杀死后面的快照成员；不能让死者再推进孕育。
                if(entry.genes==null||!entry.genes.isActiveAndEnabled)continue;
                float owed=Mathf.Min(entry.pending,tuning.mergeMaximumCatchupSeconds);entry.pending-=owed;
                entry.genes.AdvanceSimulation(dt+owed);
                // ★ 适配：醒来补清尚未结算的基础时间；本帧正常Vitals/环境Update仍只运行一次当前dt。
                if(owed>0&&entry.genes!=null&&entry.genes.gameObject.activeInHierarchy){StationaryCohortKernel.AdvanceCore(entry.genes,owed);}
            }
            // 最久未结算者优先，持续生成近邻组不会饿死远处组。
            _groups.Sort((a,b)=>b.priority.CompareTo(a.priority));
            int budget=Mathf.FloorToInt(tuning.mergeGroupsPerFrame);
            foreach(var group in _groups)
            {
                if(budget--<=0)break;
                var first=group.members[0];// ★ 修复（防Bug回归）：轮到后补算累计时间，不能固定只算一个间隔而在高倍率下永远追不上。
                float elapsed=Mathf.Min(group.time,tuning.mergeMaximumCatchupSeconds);
                foreach(var entry in group.members)entry.pending=Mathf.Max(0,entry.pending-elapsed);
                int steps=Mathf.CeilToInt(elapsed/.1f);
                for(int step=0;step<steps;step++)
                {
                    float slice=Mathf.Min(.1f,elapsed-step*.1f);
                    // ★ 修复（防Bug回归）：阶段切换/受伤/基因变化后立刻拆组；余下小步独立处理，不能沿用旧代表。
                    bool share=group.members.All(e=>e.genes!=null&&e.genes.gameObject.activeInHierarchy&&Eligible(e)&&e.revision==e.genes.LogicRevision)&&group.members.All(e=>e.genes.CurrentAge==first.genes.CurrentAge&&e.genes.IsGestating==first.genes.IsGestating);
                    if(share)
                    {
                        float speed=StationaryCohortKernel.LifeSpeed(first.genes);var delta=StationaryCohortKernel.Sample(first.genes,slice);RepresentativeSteps++;
                        for(int index=0;index<group.members.Count;index++)
                        {
                            var entry=group.members[index];if(index>0){StationaryCohortKernel.Apply(first.genes,entry.genes,delta);SharedMemberSteps++;}
                            entry.life?.AdvanceShared(slice,speed);
                            if(entry.genes.gameObject.activeInHierarchy&&entry.life?.HasDied!=true)StationaryCohortKernel.Reproduce(entry.genes,slice);
                        }
                    }
                    else foreach(var entry in group.members)
                    {
                        if(entry.genes==null||!entry.genes.gameObject.activeInHierarchy)continue;
                        entry.genes.AdvanceSimulation(slice);StationaryCohortKernel.AdvanceCore(entry.genes,slice);
                    }
                }
            }
        }
        private static int Priority(Entry entry)=>entry.genes.GetComponent<EntityManualControl>()?.IsControlled==true?3:entry.stats!=null&&entry.stats.CapacityRemaining("health")>0?2:1;
        private static bool Eligible(Entry entry)
        {
            var genes=entry.genes;
            if(entry.stats==null||entry.life==null||entry.life.HasDied||entry.stats.CapacityRemaining("health")>0||!entry.stats.TryGet("health",out var hp)||hp<=0||!entry.stats.TryGet("nutrient",out var food)||food<=0)return false;
            if(genes.EnvironmentGeneSuppression>0||entry.environment?.StressLevel>0||entry.environment?.IsDrowning==true||genes.GetAttribute("moveSpeed")>0||genes.GetComponent<EntityManualControl>()?.IsControlled==true||EcologyMotion.Immobilized(genes))return false;
            return entry.supported;
        }
        private void AddGroup(Entry entry,GameplayTuning tuning)
        {
            Vector3 point=entry.position;float radius=tuning.mergeRadius;var cell=new Vector3Int(Mathf.FloorToInt(point.x/radius),Mathf.FloorToInt(point.y/radius),Mathf.FloorToInt(point.z/radius));
            entry.stats.TryGet("nutrient",out var food);entry.stats.TryGet("storedNutrient",out var reserve);entry.stats.TryGet("size",out var size);
            entry.stats.TryGet("health",out var health);
            string sharedKey=entry.signature+"|"+Mathf.Floor(entry.pending/tuning.mergeInterval).ToString("R",CultureInfo.InvariantCulture)+"|"+entry.stateId+"|"+(entry.vitals!=null&&entry.vitals.isActiveAndEnabled)+"|"+(entry.environment!=null&&entry.environment.isActiveAndEnabled)+"|"+health.ToString("R",CultureInfo.InvariantCulture);
            int foodBand=Mathf.FloorToInt(food/tuning.mergeFoodTolerance),reserveBand=Mathf.FloorToInt(reserve/tuning.mergeReserveTolerance);
            float foodMax=food+entry.stats.CapacityRemaining("nutrient"),reserveMax=reserve+entry.stats.CapacityRemaining("storedNutrient");Cohort found=null;
            for(int x=-1;x<=1&&found==null;x++)for(int y=-1;y<=1&&found==null;y++)for(int z=-1;z<=1&&found==null;z++)
            {
                if(!_cells.TryGetValue((cell+new Vector3Int(x,y,z),sharedKey,entry.genes.CurrentAge,entry.genes.IsGestating,foodBand,reserveBand,size,foodMax,reserveMax),out var options))continue;
                foreach(var group in options)if(group.members.Count<tuning.mergeMaximumMembers&&(group.members[0].position-point).sqrMagnitude<=radius*radius){found=group;break;}
            }
            if(found==null)
            {
                found=new Cohort{time=entry.pending,priority=entry.pending};_groups.Add(found);
                var key=(cell,sharedKey,entry.genes.CurrentAge,entry.genes.IsGestating,foodBand,reserveBand,size,foodMax,reserveMax);
                if(!_cells.TryGetValue(key,out var list))_cells.Add(key,list=new List<Cohort>());list.Add(found);
            }
            found.time=Mathf.Min(found.time,entry.pending);found.priority=Mathf.Max(found.priority,entry.pending);entry.ordinal=found.members.Count;found.members.Add(entry);
            foreach(var member in found.members)member.count=found.members.Count;
        }
        private static string Signature(EntityGeneController genes)
        {
            var text=new StringBuilder();
            foreach(var gene in genes.Genome.Genes)
            {
                text.Append(gene.DefinitionId.Length).Append(':').Append(gene.DefinitionId).Append(':').Append(gene.Value.ToString("R",CultureInfo.InvariantCulture)).Append(':').Append(gene.Tier).Append(':').Append(gene.Enabled).Append(':').Append(gene.CopyStrength.ToString("R",CultureInfo.InvariantCulture)).Append(':');
                foreach(var state in gene.MissingStateIds)text.Append(state?.Length??-1).Append(':').Append(state).Append(',');text.Append(';');
            }
            return text.ToString();
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] private static void Reset()=>_instance=null;
    }
}
