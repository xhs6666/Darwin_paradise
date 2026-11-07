using System;
using System.Collections.Generic;
using System.Globalization;
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
            public EntityVitals vitals;public EntityEnvironmentHazard environment;public EntityStateController state;
            public DarwinEntity entity;public EntityManualControl manual;public NutritionParasite nutritionParasite;public ParasiteBehavior parasite;public EggIncubation egg;
            public int reproductionRevision=-1;public DarwinPark.Entities.Logic.ReproductionGeneLogic reproduction;
            public Vector3 position;public float quiet;public float pending {get=>genes.PendingSimulationTime;set=>genes.PendingSimulationTime=value;}public int revision=-1,frame=-1,ordinal,count;public bool dormant;
            public int signature;public string stateId;public bool supported;
        }
        private sealed class Cohort {public readonly List<Entry> members=new List<Entry>();public float time,priority;public Cohort next;}
        // ★ 修复（防Bug回归）：兼容条件使用值类型和预计算散列，27格查询不能反复拼接/散列完整基因字符串。
        private readonly struct SharedState:IEquatable<SharedState>
        {
            private readonly int _signature,_timeBand,_foodBand,_reserveBand,_flags,_hash;
            private readonly string _state;private readonly AgeState _age;
            private readonly float _health,_size,_foodMax,_reserveMax;
            public SharedState(Entry entry,GameplayTuning tuning)
            {
                _signature=entry.signature;_state=entry.stateId;_age=entry.genes.CurrentAge;
                _flags=(entry.genes.IsGestating?1:0)|(entry.vitals!=null&&entry.vitals.isActiveAndEnabled?2:0)|(entry.environment!=null&&entry.environment.isActiveAndEnabled?4:0);
                _timeBand=Mathf.FloorToInt(entry.pending/tuning.mergeInterval);
                entry.stats.TryGet("nutrient",out var food);entry.stats.TryGet("storedNutrient",out var reserve);
                entry.stats.TryGet("size",out _size);entry.stats.TryGet("health",out _health);
                _foodBand=Mathf.FloorToInt(food/tuning.mergeFoodTolerance);_reserveBand=Mathf.FloorToInt(reserve/tuning.mergeReserveTolerance);
                _foodMax=food+entry.stats.CapacityRemaining("nutrient");_reserveMax=reserve+entry.stats.CapacityRemaining("storedNutrient");
                var hash=new HashCode();hash.Add(_signature);hash.Add(_state);hash.Add(_age);hash.Add(_flags);hash.Add(_timeBand);hash.Add(_foodBand);hash.Add(_reserveBand);hash.Add(_health);hash.Add(_size);hash.Add(_foodMax);hash.Add(_reserveMax);_hash=hash.ToHashCode();
            }
            public bool Equals(SharedState other)=>_signature==other._signature&&_state==other._state&&_age==other._age&&_flags==other._flags&&_timeBand==other._timeBand&&_foodBand==other._foodBand&&_reserveBand==other._reserveBand&&_health==other._health&&_size==other._size&&_foodMax==other._foodMax&&_reserveMax==other._reserveMax;
            public override bool Equals(object value)=>value is SharedState other&&Equals(other);
            public override int GetHashCode()=>_hash;
        }
        private static EntityWorkScheduler _instance;
        private readonly Dictionary<EntityGeneController,Entry> _entries=new Dictionary<EntityGeneController,Entry>();
        private readonly List<Entry> _active=new List<Entry>();
        private readonly List<Entry> _controlled=new List<Entry>(),_injured=new List<Entry>();
        private readonly List<Cohort> _groups=new List<Cohort>();
        private readonly List<Cohort> _pool=new List<Cohort>();private int _usedGroups;
        private readonly Dictionary<(Vector3Int,SharedState),Cohort> _cells=new Dictionary<(Vector3Int,SharedState),Cohort>();
        private readonly Dictionary<string,int> _signatures=new Dictionary<string,int>();
        private readonly HashSet<int> _liveSignatures=new HashSet<int>();private readonly List<string> _retiredSignatures=new List<string>();
        private int _nextSignature;private bool _signaturesDirty;
        private readonly HashSet<EntityGeneController> _present=new HashSet<EntityGeneController>();
        private readonly List<EntityGeneController> _removed=new List<EntityGeneController>();
        private long _membership=-1;
        public long RepresentativeSteps {get;private set;}
        public long SharedMemberSteps {get;private set;}
        public int DormantMembers {get;private set;}
        public int CohortPoolSize=>_pool.Count;
        public int SignatureCacheSize=>_signatures.Count;
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
            entry=new Entry{genes=genes,entity=genes.GetComponent<DarwinEntity>(),stats=genes.GetComponent<EntityStats>(),life=genes.GetComponent<EntityLifeCycle>(),vitals=genes.GetComponent<EntityVitals>(),environment=genes.GetComponent<EntityEnvironmentHazard>(),position=genes.transform.position};
            _entries.Add(genes,entry);return entry;
        }
        private void Update()=>Advance(ServiceLocator.TryGet<SimulationClock>(out var clock)?clock.GameDeltaTime:Time.deltaTime);
        public void Advance(float dt)
        {
            if(dt<=0||WorldAuthority.IsReplica)return;
            if(_membership!=EntityRegistry.MembershipRevision)
            {
                _present.Clear();foreach(var entity in EntityRegistry.All)if(entity!=null&&entity.Genes!=null)_present.Add(entity.Genes);
                _removed.Clear();foreach(var key in _entries.Keys)if(key==null||!_present.Contains(key))_removed.Add(key);
                foreach(var key in _removed)_entries.Remove(key);
                if(_removed.Count>0)_signaturesDirty=true;
                if(_entries.Count==0){_signatures.Clear();_nextSignature=0;}
                foreach(var genes in _present)Get(genes);
                _membership=EntityRegistry.MembershipRevision;
            }
            foreach(var previous in _groups){previous.members.Clear();previous.next=null;}
            _active.Clear();_controlled.Clear();_injured.Clear();_groups.Clear();_cells.Clear();_usedGroups=0;DormantMembers=0;
            var tuning=GameplayTuning.Current;
            // ★ 修复（防Bug回归）：出生/死亡会改注册表，推进前先完成调度快照，不能边枚举边生成。
            foreach(var entry in _entries.Values)
            {
                var genes=entry.genes;if(genes==null||!genes.isActiveAndEnabled)continue;
                if(entry.life==null)entry.life=genes.GetComponent<EntityLifeCycle>();
                if(entry.manual==null)entry.manual=genes.GetComponent<EntityManualControl>();
                entry.frame=Time.frameCount;entry.ordinal=0;entry.count=1;
                // ★ 修复（防Bug回归）：关闭合并时不做空间/签名检查；优先级只取样一次，不能在排序比较器里反复GetComponent。
                if(!tuning.mergeStationaryEntities){entry.dormant=false;entry.quiet=0;QueueActive(entry);continue;}
                bool stationary=(genes.transform.position-entry.position).sqrMagnitude<=tuning.mergeWakeDistance*tuning.mergeWakeDistance;
                entry.position=genes.transform.position;
                if(entry.revision!=genes.LogicRevision)
                {
                    entry.revision=genes.LogicRevision;entry.supported=StationaryCohortKernel.Supported(genes);entry.quiet=0;
                    entry.state=genes.GetComponent<EntityStateController>();entry.signature=0;_signaturesDirty=true;
                    if(entry.supported){string signature=Signature(genes);if(!_signatures.TryGetValue(signature,out entry.signature)){entry.signature=++_nextSignature;_signatures.Add(signature,entry.signature);}}
                }
                string currentState=entry.supported?entry.state?.Current?.id??"":"";
                if(entry.stateId!=currentState){entry.stateId=currentState;entry.quiet=0;}
                if(entry.supported){entry.nutritionParasite=genes.GetComponent<NutritionParasite>();entry.parasite=genes.GetComponent<ParasiteBehavior>();entry.egg=genes.GetComponent<EggIncubation>();}
                bool eligible=stationary&&Eligible(entry);
                entry.quiet=eligible?entry.quiet+dt:0;
                entry.dormant=eligible&&entry.quiet>=tuning.mergeQuietSeconds;
                if(!entry.dormant){QueueActive(entry);continue;}
                DormantMembers++;entry.pending+=dt;
                if(entry.pending+0.00001f>=tuning.mergeInterval)AddGroup(entry,tuning);
            }
            if(_signaturesDirty)TrimSignatures();
            // 三档优先级用线性分桶，普通实体不再付出O(n log n)组件查询成本。
            foreach(var group in _groups)foreach(var member in group.members)member.count=group.members.Count;
            AdvanceActive(_controlled,dt,tuning);AdvanceActive(_injured,dt,tuning);AdvanceActive(_active,dt,tuning);
            // 最久未结算者优先，持续生成近邻组不会饿死远处组。
            _groups.Sort((a,b)=>b.priority.CompareTo(a.priority));
            int budget=Mathf.FloorToInt(tuning.mergeGroupsPerFrame);
            foreach(var group in _groups)
            {
                if(budget--<=0)break;
                var first=group.members[0];// ★ 修复（防Bug回归）：轮到后补算累计时间，不能固定只算一个间隔而在高倍率下永远追不上。
                float elapsed=Mathf.Min(group.time,tuning.mergeMaximumCatchupSeconds);
                foreach(var entry in group.members)entry.pending=Mathf.Max(0,entry.pending-elapsed);
                // ★ 修复（防Bug回归）：孤立实体没有共享收益，沿逐体入口补算；不为单成员反复验证代表、取样和复制环境。
                if(group.members.Count==1)
                {
                    if(first.genes!=null&&first.genes.isActiveAndEnabled){first.genes.AdvanceSimulation(elapsed);if(first.genes!=null&&first.genes.isActiveAndEnabled)StationaryCohortKernel.AdvanceCore(first.genes,elapsed);}
                    continue;
                }
                int steps=Mathf.CeilToInt(elapsed/.1f);
                for(int step=0;step<steps;step++)
                {
                    float slice=Mathf.Min(.1f,elapsed-step*.1f);
                    // ★ 修复（防Bug回归）：阶段切换/受伤/基因变化后立刻拆组；余下小步独立处理，不能沿用旧代表。
                    bool share=CanShare(group,first);
                    if(share)
                    {
                        float speed=StationaryCohortKernel.LifeSpeed(first.genes);var delta=StationaryCohortKernel.Sample(first.genes,first.stats,first.environment,first.vitals,slice);RepresentativeSteps++;
                        for(int index=0;index<group.members.Count;index++)
                        {
                            var entry=group.members[index];if(index>0){StationaryCohortKernel.Apply(first.environment,entry.genes,entry.stats,entry.environment,entry.entity,delta);SharedMemberSteps++;}
                            entry.life?.AdvanceShared(slice,speed);
                            if(entry.genes.gameObject.activeInHierarchy&&entry.life?.HasDied!=true)
                            {
                                // ★ 修复（防Bug回归）：阶段/孕育重算后才重新取主繁殖逻辑，静止小步不再遍历整份基因。
                                if(entry.reproductionRevision!=entry.genes.LogicRevision){entry.reproduction=StationaryCohortKernel.PrimaryReproduction(entry.genes);entry.reproductionRevision=entry.genes.LogicRevision;}
                                entry.reproduction?.OnUpdate(slice);
                            }
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
        private void TrimSignatures()
        {
            // ★ 修复（防Bug回归）：签名共享表只能保留仍被实体引用的版本，连续出生/变异不能累积已灭绝基因字符串。
            _liveSignatures.Clear();foreach(var entry in _entries.Values)if(entry.signature!=0)_liveSignatures.Add(entry.signature);
            _retiredSignatures.Clear();foreach(var pair in _signatures)if(!_liveSignatures.Contains(pair.Value))_retiredSignatures.Add(pair.Key);
            foreach(var signature in _retiredSignatures)_signatures.Remove(signature);_retiredSignatures.Clear();_signaturesDirty=false;
        }
        private void QueueActive(Entry entry)
        {
            if(entry.manual?.IsControlled==true)_controlled.Add(entry);
            else if(entry.stats!=null&&entry.stats.CapacityRemaining("health")>0)_injured.Add(entry);
            else _active.Add(entry);
        }
        private static void AdvanceActive(List<Entry> entries,float dt,GameplayTuning tuning)
        {
            foreach(var entry in entries)
            {
                // ★ 修复（防Bug回归）：高优先级攻击可能已杀死后面的快照成员；不能让死者再推进孕育。
                if(entry.genes==null||!entry.genes.isActiveAndEnabled)continue;
                float owed=Mathf.Min(entry.pending,tuning.mergeMaximumCatchupSeconds);entry.pending-=owed;
                entry.genes.AdvanceSimulation(dt+owed);
                if(owed>0&&entry.genes!=null&&entry.genes.gameObject.activeInHierarchy)StationaryCohortKernel.AdvanceCore(entry.genes,owed);
            }
        }
        private static bool CanShare(Cohort group,Entry first)
        {
            foreach(var entry in group.members)
                if(entry.genes==null||!entry.genes.isActiveAndEnabled||entry.revision!=entry.genes.LogicRevision||!Eligible(entry)||entry.genes.CurrentAge!=first.genes.CurrentAge||entry.genes.IsGestating!=first.genes.IsGestating)return false;
            return true;
        }
        private static bool Eligible(Entry entry)
        {
            var genes=entry.genes;
            if(!entry.supported||entry.stats==null||entry.life==null||entry.life.HasDied||entry.stats.CapacityRemaining("health")>0||!entry.stats.TryGet("health",out var hp)||hp<=0||!entry.stats.TryGet("nutrient",out var food)||food<=0)return false;
            // ★ 修复（防Bug回归）：每帧刷新可附着组件引用，小步内直接检查实时字段；零基础速度无需重复走群体/寄生修饰链。
            if(genes.EnvironmentGeneSuppression>0||entry.environment?.StressLevel>0||entry.environment?.IsDrowning==true||entry.manual?.IsControlled==true||entry.nutritionParasite?.Host!=null||entry.parasite?.Active==true||entry.egg?.Incubating==true||AntColonyWorker.IsCarried(entry.entity))return false;
            if(genes.Evaluation.Attributes.TryGetValue("moveSpeed",out var move)&&move>0&&genes.GetAttribute("moveSpeed")>0)return false;
            return entry.supported;
        }
        private void AddGroup(Entry entry,GameplayTuning tuning)
        {
            Vector3 point=entry.position;float radius=tuning.mergeRadius;var cell=new Vector3Int(Mathf.FloorToInt(point.x/radius),Mathf.FloorToInt(point.y/radius),Mathf.FloorToInt(point.z/radius));
            var shared=new SharedState(entry,tuning);Cohort found=null;
            for(int x=-1;x<=1&&found==null;x++)for(int y=-1;y<=1&&found==null;y++)for(int z=-1;z<=1&&found==null;z++)
            {
                if(!_cells.TryGetValue((cell+new Vector3Int(x,y,z),shared),out var candidate))continue;
                for(var group=candidate;group!=null;group=group.next)
                    if(group.members.Count<tuning.mergeMaximumMembers&&(group.members[0].position-point).sqrMagnitude<=radius*radius){found=group;break;}
            }
            if(found==null)
            {
                // ★ 修复（防Bug回归）：组与成员数组在结算之间复用；桶用单链表，避免每次到期为每个格子分配List。
                if(_usedGroups==_pool.Count)_pool.Add(new Cohort());
                found=_pool[_usedGroups++];found.time=found.priority=entry.pending;_groups.Add(found);
                var key=(cell,shared);_cells.TryGetValue(key,out found.next);_cells[key]=found;
            }
            found.time=Mathf.Min(found.time,entry.pending);found.priority=Mathf.Max(found.priority,entry.pending);entry.ordinal=found.members.Count;found.members.Add(entry);
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
