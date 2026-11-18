using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using DarwinPark.Core;
using DarwinPark.Entities.Ecology;
using DarwinPark.Entities.Stats;
using DarwinPark.Entities.Vitals;
using UnityEngine;

// ★ 修复（防Bug回归）：可选组件允许正常缺失，使用即时TryGet，保留安装/销毁可见性并消除编辑器空查询分配。
namespace DarwinPark.Entities.Simulation
{
    // ★ 新增：由GameBootstrap运行时挂载。交互/受伤实体先算，静止近邻共享基础变化，个体身份和繁殖不合并。
    [DefaultExecutionOrder(-100)] public sealed class EntityWorkScheduler:MonoBehaviour
    {
        private static EntityWorkScheduler _instance;
        private readonly Dictionary<EntityGeneController,EntityScheduleEntry> _entries=new Dictionary<EntityGeneController,EntityScheduleEntry>();
        private readonly List<EntityScheduleEntry> _active=new List<EntityScheduleEntry>();
        private readonly List<EntityScheduleEntry> _controlled=new List<EntityScheduleEntry>(),_injured=new List<EntityScheduleEntry>();
        private readonly List<StationaryCohort> _groups=new List<StationaryCohort>();
        private readonly List<StationaryCohort> _pool=new List<StationaryCohort>();private int _usedGroups;
        private readonly Dictionary<(Vector3Int,CohortCompatibility),StationaryCohort> _cells=new Dictionary<(Vector3Int,CohortCompatibility),StationaryCohort>();
        private readonly Dictionary<string,int> _signatures=new Dictionary<string,int>();
        private readonly HashSet<int> _liveSignatures=new HashSet<int>();private readonly List<string> _retiredSignatures=new List<string>();
        private int _nextSignature;private bool _signaturesDirty;
        private readonly HashSet<EntityGeneController> _present=new HashSet<EntityGeneController>();
        private readonly List<EntityGeneController> _removed=new List<EntityGeneController>();
        private long _membership=-1;
        private double _simulationTime;private float _phaseInterval,_phaseRadius;private int _phaseBuckets;private bool _stagger;
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
        private EntityScheduleEntry Get(EntityGeneController genes)
        {
            if(_entries.TryGetValue(genes,out var entry))return entry;
            entry=new EntityScheduleEntry{genes=genes,entity=genes.GetComponent<DarwinEntity>(),stats=genes.GetComponent<EntityStats>(),life=genes.GetComponent<EntityLifeCycle>(),vitals=genes.GetComponent<EntityVitals>(),environment=genes.GetComponent<EntityEnvironmentHazard>(),position=genes.transform.position};
            _entries.Add(genes,entry);return entry;
        }
        // ★ 适配：全局每帧一次检查自然数值的热加载，不在每个实体的生命小步访问磁盘。
        private void Update(){DarwinPark.Entities.Ecology.NaturalPopulationPace.Poll();Advance(ServiceLocator.TryGet<SimulationClock>(out var clock)?clock.GameDeltaTime:Time.deltaTime);}
        public void Advance(float dt)
        {
            // ★ 适配：只在主动性能采样期间计时，定位完整场景的实际消耗。
            using var performance=DarwinPark.Core.PerformanceTimings.Measure(DarwinPark.Core.PerformanceTimings.Area.Scheduler);
            if(dt<=0||WorldAuthority.IsReplica)return;
            RefreshMembership();
            foreach(var previous in _groups){previous.members.Clear();previous.next=null;}
            _active.Clear();_controlled.Clear();_injured.Clear();_groups.Clear();_cells.Clear();_usedGroups=0;DormantMembers=0;
            var tuning=GameplayTuning.Current;
            double frameStart=_simulationTime;_simulationTime+=dt;
            bool phaseChanged=_phaseInterval!=tuning.mergeInterval||_phaseRadius!=tuning.mergeRadius||_phaseBuckets!=tuning.mergePhaseBuckets||_stagger!=tuning.mergeStaggerBatches;
            _phaseInterval=tuning.mergeInterval;_phaseRadius=tuning.mergeRadius;_phaseBuckets=tuning.mergePhaseBuckets;_stagger=tuning.mergeStaggerBatches;
            // ★ 修复（防Bug回归）：出生/死亡会改注册表，推进前先完成调度快照，不能边枚举边生成。
            CollectEntries(dt, tuning, frameStart, phaseChanged);
            if(_signaturesDirty)TrimSignatures();
            // 三档优先级用线性分桶，普通实体不再付出O(n log n)组件查询成本。
            foreach(var group in _groups)foreach(var member in group.members)member.count=group.members.Count;
            AdvanceActive(_controlled,dt,tuning);AdvanceActive(_injured,dt,tuning);AdvanceActive(_active,dt,tuning);
            // 最久未结算者优先，持续生成近邻组不会饿死远处组。
            SettleGroups(tuning);
        }

        // ✅ 适配：成员变更、候选收集与分组结算各有边界，调用顺序与原实现一致。
        private void RefreshMembership()
        {
            if(_membership!=EntityRegistry.MembershipRevision)
            {
                _present.Clear();foreach(var entity in EntityRegistry.All)if(entity!=null&&entity.Genes!=null)_present.Add(entity.Genes);
                _removed.Clear();foreach(var key in _entries.Keys)if(key==null||!_present.Contains(key))_removed.Add(key);
                foreach(var key in _removed)_entries.Remove(key);
                if(_removed.Count>0)_signaturesDirty=true;
                if(_entries.Count==0){_signatures.Clear();_nextSignature=0;_simulationTime=0;}
                foreach(var genes in _present)Get(genes);
                _membership=EntityRegistry.MembershipRevision;
            }
        }

        private void CollectEntries(float dt, GameplayTuning tuning, double frameStart, bool phaseChanged)
        {
            foreach(var entry in _entries.Values)
            {
                var genes=entry.genes;if(genes==null||!genes.isActiveAndEnabled)continue;
                if(entry.life==null)entry.life=genes.GetComponent<EntityLifeCycle>();
                if(entry.manual==null)entry.manual=OptionalComponent.Read<EntityManualControl>(genes);
                entry.frame=Time.frameCount;entry.ordinal=0;entry.count=1;
                // ★ 修复（防Bug回归）：关闭合并时不做空间/签名检查；优先级只取样一次，不能在排序比较器里反复GetComponent。
                if(!tuning.mergeStationaryEntities){entry.dormant=false;entry.quiet=0;entry.nextDue=double.NaN;QueueActive(entry);continue;}
                if(phaseChanged)entry.nextDue=double.NaN;
                bool stationary=(genes.transform.position-entry.position).sqrMagnitude<=tuning.mergeWakeDistance*tuning.mergeWakeDistance;
                entry.position=genes.transform.position;
                if(entry.revision!=genes.LogicRevision)
                {
                    entry.revision=genes.LogicRevision;entry.supported=StationaryCohortKernel.Supported(genes);entry.quiet=0;entry.nextDue=double.NaN;
                    entry.state=DarwinPark.Core.OptionalComponent.Read<EntityStateController>(genes);entry.signature=0;_signaturesDirty=true;
                    if(entry.supported){string signature=Signature(genes);if(!_signatures.TryGetValue(signature,out entry.signature)){entry.signature=++_nextSignature;_signatures.Add(signature,entry.signature);}}
                }
                string currentState=entry.supported?entry.state?.Current?.id??"":"";
                if(entry.stateId!=currentState){entry.stateId=currentState;entry.quiet=0;}
                // ★ 修复（防Bug回归）：正常缺失的寄生/孵化组件不能在每帧调度中生成编辑器诊断分配。
                if(entry.supported){entry.nutritionParasite=OptionalComponent.Read<NutritionParasite>(genes);entry.parasite=OptionalComponent.Read<ParasiteBehavior>(genes);entry.egg=OptionalComponent.Read<EggIncubation>(genes);}
                bool eligible=stationary&&Eligible(entry);
                entry.quiet=eligible?entry.quiet+dt:0;
                entry.dormant=eligible&&entry.quiet>=tuning.mergeQuietSeconds;
                if(!entry.dormant){entry.nextDue=double.NaN;QueueActive(entry);continue;}
                DormantMembers++;entry.pending+=dt;
                // ★ 修复（防Bug回归）：到期按空间/基因相位错开，欠账仍完整保存；预算延期不能重置截止时间。
                if(_stagger)
                {
                    if(double.IsNaN(entry.nextDue))entry.nextDue=CohortDeadline.Next(frameStart,entry.position,entry.signature,tuning.mergeRadius,tuning.mergeInterval,tuning.mergePhaseBuckets);
                    if(_simulationTime>=entry.nextDue)AddGroup(entry,tuning);
                }
                else if(entry.pending+0.00001f>=tuning.mergeInterval)AddGroup(entry,tuning);
            }
        }

        private void SettleGroups(GameplayTuning tuning)
        {
            _groups.Sort((a,b)=>b.priority.CompareTo(a.priority));
            int budget=Mathf.FloorToInt(tuning.mergeGroupsPerFrame);
            foreach(var group in _groups)
            {
                if(budget--<=0)break;
                var first=group.members[0];// ★ 修复（防Bug回归）：轮到后补算累计时间，不能固定只算一个间隔而在高倍率下永远追不上。
                float elapsed=Mathf.Min(group.time,tuning.mergeMaximumCatchupSeconds);
                foreach(var entry in group.members)
                {
                    entry.pending=Mathf.Max(0,entry.pending-elapsed);
                    if(_stagger)entry.nextDue=CohortDeadline.Next(_simulationTime,entry.position,entry.signature,tuning.mergeRadius,tuning.mergeInterval,tuning.mergePhaseBuckets);
                }
                // ★ 修复（防Bug回归）：孤立实体没有共享收益，沿逐体入口补算；不为单成员反复验证代表、取样和复制环境。
                if(group.members.Count==1)
                {
                    if(first.genes!=null&&first.genes.isActiveAndEnabled){SingletonSettlement.Advance(first.genes,elapsed);}
                    continue;
                }
                int steps=SimulationSubsteps.Count(elapsed);
                for(int step=0;step<steps;step++)
                {
                    float slice=SimulationSubsteps.Slice(elapsed,step,steps);
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
        private void QueueActive(EntityScheduleEntry entry)
        {
            if(entry.manual?.IsControlled==true)_controlled.Add(entry);
            else if(entry.stats!=null&&entry.stats.CapacityRemaining("health")>0)_injured.Add(entry);
            else _active.Add(entry);
        }
        private static void AdvanceActive(List<EntityScheduleEntry> entries,float dt,GameplayTuning tuning)
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
        private static bool CanShare(StationaryCohort group,EntityScheduleEntry first)
        {
            foreach(var entry in group.members)
                if(entry.genes==null||!entry.genes.isActiveAndEnabled||entry.revision!=entry.genes.LogicRevision||!Eligible(entry)||entry.genes.CurrentAge!=first.genes.CurrentAge||entry.genes.IsGestating!=first.genes.IsGestating)return false;
            return true;
        }
        private static bool Eligible(EntityScheduleEntry entry)
        {
            var genes=entry.genes;
            if(!entry.supported||entry.stats==null||entry.life==null||entry.life.HasDied||entry.stats.CapacityRemaining("health")>0||!entry.stats.TryGet("health",out var hp)||hp<=0||!entry.stats.TryGet("nutrient",out var food)||food<=0)return false;
            if(DarwinPark.Core.OptionalComponent.Read<EntityActivityPlan>(genes)?.Active==true)return false;
            // ★ 修复（防Bug回归）：每帧刷新可附着组件引用，小步内直接检查实时字段；零基础速度无需重复走群体/寄生修饰链。
            if(genes.EnvironmentGeneSuppression>0||entry.environment?.StressLevel>0||entry.environment?.IsDrowning==true||entry.environment?.IsInWater==true||entry.manual?.IsControlled==true||entry.nutritionParasite?.Host!=null||entry.parasite?.Active==true||entry.egg?.Incubating==true||AntColonyWorker.IsCarried(entry.entity))return false;
            if(genes.Evaluation.Attributes.TryGetValue("moveSpeed",out var move)&&move>0&&genes.GetAttribute("moveSpeed")>0)return false;
            return entry.supported;
        }
        private void AddGroup(EntityScheduleEntry entry,GameplayTuning tuning)
        {
            Vector3 point=entry.position;float radius=tuning.mergeRadius;var cell=new Vector3Int(Mathf.FloorToInt(point.x/radius),Mathf.FloorToInt(point.y/radius),Mathf.FloorToInt(point.z/radius));
            var shared=new CohortCompatibility(entry,tuning);StationaryCohort found=null;
            for(int x=-1;x<=1&&found==null;x++)for(int y=-1;y<=1&&found==null;y++)for(int z=-1;z<=1&&found==null;z++)
            {
                if(!_cells.TryGetValue((cell+new Vector3Int(x,y,z),shared),out var candidate))continue;
                for(var group=candidate;group!=null;group=group.next)
                    if(group.members.Count<tuning.mergeMaximumMembers&&(group.members[0].position-point).sqrMagnitude<=radius*radius){found=group;break;}
            }
            if(found==null)
            {
                // ★ 修复（防Bug回归）：组与成员数组在结算之间复用；桶用单链表，避免每次到期为每个格子分配List。
                if(_usedGroups==_pool.Count)_pool.Add(new StationaryCohort());
                found=_pool[_usedGroups++];found.time=found.priority=entry.pending;_groups.Add(found);
                var key=(cell,shared);_cells.TryGetValue(key,out found.next);_cells[key]=found;
            }
            found.time=Mathf.Min(found.time,entry.pending);found.priority=Mathf.Max(found.priority,entry.pending);entry.ordinal=found.members.Count;found.members.Add(entry);
        }
        private static string Signature(EntityGeneController genes)
        {
            // ★ 适配：按需区分重算、签名与实际行为成本。
            using var performance=DarwinPark.Core.PerformanceTimings.Measure(DarwinPark.Core.PerformanceTimings.Area.GeneSignature);
            var text=new StringBuilder();
            foreach(var gene in genes.Genome.Genes)
            {
                text.Append(gene.DefinitionId.Length).Append(':').Append(gene.DefinitionId).Append(':').Append(gene.StateId?.Length??0).Append(':').Append(gene.StateId).Append(':').Append(gene.Value.ToString("R",CultureInfo.InvariantCulture)).Append(':').Append(gene.Tier).Append(':').Append(gene.Enabled).Append(':').Append(gene.CopyStrength.ToString("R",CultureInfo.InvariantCulture)).Append(':');
                foreach(var state in gene.MissingStateIds)text.Append(state?.Length??-1).Append(':').Append(state).Append(',');text.Append(';');
            }
            return text.ToString();
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] private static void Reset()=>_instance=null;
    }
}
