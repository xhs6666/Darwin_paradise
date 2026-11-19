using DarwinPark.Core; // ★ 适配：实时可选组件查询避免空查找的编辑器分配。
using System;
using System.Collections.Generic;
using System.Linq;
using DarwinPark.Entities.Logic;
using DarwinPark.Genetics;
using UnityEngine;

// ★ 修复（防Bug回归）：可选组件允许正常缺失，使用即时TryGet，保留安装/销毁可见性并消除编辑器空查询分配。
namespace DarwinPark.Entities.Ecology
{
    [Serializable] public sealed class AntColonyMemberSave
    {
        public string role,lockedGenome,resourceTarget,livingTarget,carriedResource,carriedLiving;
        public float scan,patrolRemaining,restRemaining,circleAngle,reproductionTimer,reproductionCooldown;
        // ★ 适配：王后无配偶等待和孤雌孕育状态与已有蚁群快照一起持久化。
        public float mateWait,maintenanceWait;public bool parthenogenetic;
        public bool founding,deliveryNursery,originalKinematic,originalDetect;
        public Vector3 foundingPoint,deliveryPoint;
        public List<Vector3> supplyPoints=new List<Vector3>();
        public Genome partnerGenome;
        // ★ 适配：创业储粮领取记录随分工保存，重载和重算不能重复领取。
        public ColonyFoundingSave startup;
        // ★ 适配：保存补给任务和固定资源，读档不会在任务间反复抢占。
        public EntityTaskPlanSave plan;
        public Logic.MultipleBirthState litter;
        public bool HasData=>!string.IsNullOrEmpty(role);

        internal AntColonyMemberSave Snapshot()
        {
            var copy=(AntColonyMemberSave)MemberwiseClone();
            copy.supplyPoints=supplyPoints==null?new List<Vector3>():new List<Vector3>(supplyPoints);
            copy.partnerGenome=Genome.Snapshot(partnerGenome);
            copy.litter=Logic.MultipleBirthPolicy.Copy(litter);
            if(startup!=null)copy.startup=new ColonyFoundingSave
                {version=startup.version,collecting=startup.collecting,foodIssued=startup.foodIssued};
            if(plan!=null)
            {
                copy.plan=new EntityTaskPlanSave{feeding=plan.feeding,supply=plan.supply};
                if(plan.tasks!=null)
                    foreach(var task in plan.tasks)
                        copy.plan.tasks.Add(new PlannedEntityTask{kind=task.kind,target=task.target});
            }
            return copy;
        }
    }
    public enum ColonyActivity { Rest,Patrol,Hunt,Collect,Transport,Feed,Reproduce,Founding,Young,Mate }
    // ★ 新增：固定出生分工与可持久化工作状态独立于基因倾向，不给原种群脚本堆叠蚁群流程。
    public sealed class AntColonyMember : MonoBehaviour
    {
        private static readonly List<AntColonyMember> Members=new List<AntColonyMember>();
        public static IEnumerable<AntColonyMember> All=>Members.Where(m=>m!=null&&m.Active);
        public AntColonyMemberSave State {get;private set;}=new AntColonyMemberSave();
        public EntityGeneController Genes {get;private set;}
        public DarwinEntity Entity {get;private set;}
        public PopulationBehavior Population {get;private set;}
        private string _roleText;private AntRole _role;
        // ★ 修复（防Bug回归）：固定分工的文本只在变化时解析，不能每次读取移动属性都重新解析枚举。
        public AntRole Role {get{if(_roleText!=State.role){_roleText=State.role;_role=Enum.TryParse<AntRole>(State.role,out var role)?role:AntRole.None;}return _role;}}
        public bool Active=>Population!=null&&Population.Active&&Population.Mode==EcologySettings.Load().antState;
        public bool Young=>Genes.CurrentAge==AgeState.Infant||OptionalComponent.Read<EggIncubation>(this)?.Incubating==true;
        public string GroupId=>Population.GroupId;
        public string Id=>EcologyIdentity.Of(Entity);
        public IEnumerable<AntColonyMember> Group=>Population.GroupMembers.Select(p=>DarwinPark.Core.OptionalComponent.Read<AntColonyMember>(p)).Where(m=>m!=null&&m.Active);
        public AntColonyMember Queen=>PopulationGroupIndex.Queen(Population);
        public Vector3 Center {get{var queen=Queen;return queen!=null?queen.transform.position:transform.position;}}
        public float Radius=>Mathf.Max(AntColonySettings.Current.minimumColonyRadius,Genes.GetAttribute("territory_radius"),AntColonySettings.Current.colonyRadius)*DarwinPark.Core.GameplayTuning.Current.colonyActivityRadiusMultiplier;
        public bool Frozen=>Active&&Role!=AntRole.None&&AntColonySettings.Current.Rule(Role).frozenGenome;
        // ★ 适配：面板和行动共用计划优先级，避免补给中显示运输/繁殖。
        private ColonyActivity _activity;
        public ColonyActivity Activity {get=>EntityTaskPlan.Feeding(this)&&ColonyResource.Find(State.plan?.supply)!=null?ColonyActivity.Feed:_activity;internal set{_activity=value;MemberTaskSequence.Record(this,value);}}
        public int TaskPriority=>EntityTaskPlan.Priority(Activity);
        internal DarwinEntity HuntTarget;
        private AntColonyMember _orderQueen;
        private float _orderRemaining, _orderMultiplier;
        private Vector3 _orderPoint;
        private DarwinEntity _orderTarget, _orderHunt;
        private string _orderSupply, _orderCargo, _orderWork;
        private ColonyActivity _orderActivity;
        private int _orderRevision;
        private bool _orderValid, _orderFeeding, _orderHasSupply;
        private long _dispatchRevision=-1;
        private int _dispatchGeneRevision=-1;
        private AntColonyMember _dispatchQueen, _roleQueen;
        private CentralGeneLogic _dispatcher;
        private float _roleElapsed, _roleRemaining;
        private string _roleWork;
        private bool _roleYoung, _roleRelief, _roleFeeding;
        internal bool ReliefWorker {get;set;}
        private float _assignedLandSpeed;
        internal float AssignedLandSpeed(AntColonyMember queen)=>_orderQueen==queen&&_orderRevision==Genes.LogicRevision
            ?_assignedLandSpeed:EcologyMotion.MobileLandSpeed(Genes);

        // ★ 适配：派工者按群关系和王后表达版本失效，子步无需重复选择同一个中枢。
        internal CentralGeneLogic Dispatcher(out AntColonyMember queen)
        {
            if(_dispatchRevision!=PopulationGroupIndex.Revision||_dispatchQueen!=null&&!_dispatchQueen.gameObject.activeInHierarchy)
            {
                _dispatchQueen=Queen;
                _dispatchRevision=PopulationGroupIndex.Revision;
                _dispatchGeneRevision=-1;
                _dispatcher=null;
            }
            queen=_dispatchQueen;
            if(queen!=null&&_dispatchGeneRevision!=queen.Genes.LogicRevision)
            {
                _dispatchGeneRevision=queen.Genes.LogicRevision;
                _dispatcher=GeneBehaviorAuthority.Primary<CentralGeneLogic>(queen.Genes);
            }
            return queen!=null?_dispatcher:null;
        }
        internal bool Carrying=>!string.IsNullOrEmpty(State.carriedResource)||!string.IsNullOrEmpty(State.carriedLiving);
        private readonly Dictionary<string,float> _suppression=new Dictionary<string,float>();
        private string _suppressionRole;
        private void Awake(){Genes=GetComponent<EntityGeneController>();Entity=GetComponent<DarwinEntity>();Population=DarwinPark.Core.OptionalComponent.Read<PopulationBehavior>(this);}
        private void OnEnable(){if(!Members.Contains(this))Members.Add(this);}
        private void OnDisable(){AntColonyWorker.Release(this);Members.Remove(this);}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] private static void Reset()=>Members.Clear();
        public static AntColonyMember Ensure(PopulationBehavior pop)=>DarwinPark.Core.OptionalComponent.Read<AntColonyMember>(pop)??pop.gameObject.AddComponent<AntColonyMember>();
        public static AntColonyMember QueenFor(PopulationBehavior pop)=>DarwinPark.Core.OptionalComponent.Read<AntColonyMember>(pop)?.Queen;
        public static bool IsManaged(EntityGeneController genes)=>Simulation.ActiveSimulationBatch.Read<AntColonyMember>(genes)?.Active==true;
        public void Assign(AntRole role,string group=null)
        {
            if(group!=null)Population.JoinColony(group,role==AntRole.Queen);
            State.role=role.ToString();State.lockedGenome=AntColonySettings.Current.Rule(role).frozenGenome?JsonUtility.ToJson(Genes.Genome):null;
            PopulationGroupIndex.Invalidate();
            Genes.IsGestating=false;_suppressionRole=null;Genes.Recalculate();
            if(GetComponent<ColonyMemberMarker>()==null)gameObject.AddComponent<ColonyMemberMarker>();
            if(role==AntRole.Queen)EnsureSupplyPoints();
        }
        public static void OnBirth(EntityGeneController parent,DarwinEntity child)
        {
            var source=DarwinPark.Core.OptionalComponent.Read<AntColonyMember>(parent);if(source==null||!source.Active||child==null)return;
            var pop=DarwinPark.Core.OptionalComponent.Read<PopulationBehavior>(child);if(pop==null)return;
            var member=Ensure(pop);member.Assign(source.ChooseBirthRole(),source.GroupId);
        }
        public AntRole ChooseBirthRole()
        {
            // ★ 适配：前几胎先补齐工匠，避免随机连续出生士兵/繁殖个体形成补给死循环。
            if(ColonyFoundingSupport.NeedsWorker(this))return AntRole.Worker;
            // ★ 适配：先工匠再繁殖后继，不靠大种群随机抽取来维持小群谱系。
            if(ColonyContinuityPriority.NeedsSuccessor(this))return AntRole.Reproductive;
            var settings=AntColonySettings.Current;
            if(Group.Count()>=settings.reproductivePopulationThreshold&&Group.Count(m=>m.Role==AntRole.Reproductive)<settings.minimumReproductives)return AntRole.Reproductive;
            var choices=settings.roles.Where(r=>r.birthWeight>0&&(r.Role!=AntRole.Reproductive||Group.Count(m=>m.Role==AntRole.Reproductive)<settings.maximumReproductives)).ToArray();
            float roll=UnityEngine.Random.value*choices.Sum(r=>r.birthWeight);
            foreach(var rule in choices){roll-=rule.birthWeight;if(roll<=0)return rule.Role;}return choices[choices.Length-1].Role;
        }
        public void Advance(float dt)
        {
            if(!Active||dt<=0||WorldAuthority.IsReplica||!AdvanceNeeds(dt))return;
            AdvanceRole(dt);
        }

        internal bool AdvanceNeeds(float dt)
        {
            if(Role==AntRole.None)Assign(Population.LeaderFlag?AntRole.Queen:ChooseBirthRole());
            if(Simulation.ActiveSimulationBatch.Read<EggIncubation>(Genes)?.Incubating==true)return false;
            ColonyResource.Feed(this,dt);
            return true;
        }

        internal void AdvanceRole(float dt,bool emergencyCollector=false,int adultWorkers=-1)
        {
            // ★ 适配：只在主动性能采样期间计时，定位完整场景的实际消耗。
            using var performance=DarwinPark.Core.PerformanceTimings.Measure(DarwinPark.Core.PerformanceTimings.Area.ColonyTasks);
            State.scan-=dt;State.reproductionCooldown=Mathf.Max(0,State.reproductionCooldown-dt);
            if(OptionalComponent.Read<EggIncubation>(this)?.Incubating==true)return;
            if(Young&&!ReliefWorker){Activity=ColonyActivity.Young;return;}
            // ★ 新增：用户计划接管行为时暂存原分工任务，不覆盖顺序或丢弃正在搬运的货物。
            if(EntityActivityPlan.BlocksAutomatic(this))return;
            if(Role==AntRole.Reproductive){BeginFounding();return;}
            if(State.founding)
            {
                // ★ 修复（防Bug回归）：迁居途中也必须先补给，原定建群点保留，不能带着饥饿一直赶路。
                if(ColonyFeeding.Destination(this,out _))return;
                Activity=ColonyActivity.Founding;
                if(EcologyQueries.Distance(transform.position,EcologyQueries.ToWorld(State.foundingPoint))<=AntColonySettings.Current.arrivalDistance)
                {State.founding=false;EnsureSupplyPoints();DarwinPark.Core.OptionalComponent.Read<TerritoryBehavior>(this)?.Restore(EcologyQueries.ToLocal(transform.position),true,0,Vector3.zero);}
                return;
            }
            if(Role==AntRole.Queen){Activity=ColonyActivity.Reproduce;ColonyFoundingSupport.Advance(this,dt,emergencyCollector,adultWorkers);AntColonyReproduction.Advance(this,dt);return;}
            if(Role==AntRole.Worker){AntColonyWorker.Advance(this,dt);return;}
            if(Role==AntRole.Mate){Activity=ColonyActivity.Mate;return;}
            AdvanceSoldier(dt);
        }

        internal void AdvanceLocalRole(float dt)
        {
            ReliefWorker=Young&&Role==AntRole.Worker&&Carrying;
            AdvanceRole(dt+_roleElapsed);
            _roleElapsed=0;
            _roleQueen=null;
        }

        // ★ 新增：派工间隔按游戏时间累加。携带与交付保持实时，空闲成员无需每帧重查分工。
        internal void AdvanceDispatchedRole(AntColonyMember queen,float dt,bool relief)
        {
            bool young=Young;
            bool carrying=Carrying;
            ReliefWorker=relief||young&&Role==AntRole.Worker&&carrying;
            string work=WorkTarget;
            _roleElapsed+=dt;
            _roleRemaining-=dt;
            bool changed=_roleQueen!=queen||_roleYoung!=young||_roleRelief!=ReliefWorker||
                _roleWork!=work||_roleFeeding!=(State.plan?.feeding==true)||_orderRevision!=Genes.LogicRevision;
            if(!changed&&!carrying&&_roleRemaining>0)return;
            var settings=SimulationOptimization.Current;
            if(_roleQueen!=queen)
            {
                float phase=((uint)GetInstanceID()%(uint)settings.decisionFrameBuckets)/(float)settings.decisionFrameBuckets;
                _roleRemaining=settings.decisionSeconds*(1+phase);
            }
            else if(_roleRemaining<=0)_roleRemaining=settings.decisionSeconds;
            _roleQueen=queen;_roleYoung=young;_roleRelief=ReliefWorker;
            AdvanceRole(_roleElapsed);
            if(this!=null&&GroupId==queen.GroupId)AssignMovement(queen,_roleElapsed);
            _roleElapsed=0;
            _roleWork=WorkTarget;_roleFeeding=State.plan?.feeding==true;
        }

        private string WorkTarget=>string.IsNullOrEmpty(State.resourceTarget)?State.livingTarget:State.resourceTarget;

        // ★ 新增：群体意图由王后发布。成员只消费目的地，分工、补给、货物或猎物变化时立即换单。
        internal void AssignMovement(AntColonyMember queen,float dt)
        {
            _orderRemaining-=dt;
            bool feeding=ColonyFeeding.WantsFood(this);
            string supply=State.plan?.supply;
            string cargo=string.IsNullOrEmpty(State.carriedResource)?State.carriedLiving:State.carriedResource;
            string work=WorkTarget;
            if(_orderQueen==queen&&_orderRevision==Genes.LogicRevision&&_orderRemaining>0&&_orderActivity==_activity&&_orderFeeding==feeding&&
                _orderSupply==supply&&_orderCargo==cargo&&_orderWork==work&&_orderHunt==HuntTarget&&
                (!_orderHasSupply||ColonyResource.Find(_orderSupply)!=null))return;
            if(_orderQueen!=queen)
            {
                var config=SimulationOptimization.Current;
                float phase=((uint)GetInstanceID()%(uint)config.decisionFrameBuckets)/(float)config.decisionFrameBuckets;
                _orderRemaining=config.decisionSeconds*(1+phase);
            }
            else if(_orderRemaining<=0)_orderRemaining=SimulationOptimization.Current.decisionSeconds;
            _orderQueen=queen;_orderFeeding=feeding;_orderCargo=cargo;_orderWork=work;_orderHunt=HuntTarget;
            Vector3 point;
            bool supplyOrder=false;
            if(feeding&&TrySupplyDestination(out point))supplyOrder=true;
            else _orderValid=Destination(out point,out _orderTarget,out _orderMultiplier,false);
            if(supplyOrder){_orderTarget=null;_orderMultiplier=1;_orderValid=true;}
            _orderActivity=_activity;_orderHasSupply=supplyOrder;_orderRevision=Genes.LogicRevision;
            _assignedLandSpeed=EcologyMotion.MobileLandSpeed(Genes);
            _orderSupply=State.plan?.supply;_orderPoint=EcologyQueries.ToLocal(point);
        }

        internal bool AssignedMovement(AntColonyMember queen,out Vector3 point,out DarwinEntity target,out float multiplier,out bool supply)
        {
            bool current=queen!=null&&_orderQueen==queen&&_orderRevision==Genes.LogicRevision&&_orderValid;
            point=current?EcologyQueries.ToWorld(_orderPoint):transform.position;
            target=current?_orderTarget:null;multiplier=current?_orderMultiplier:1;
            supply=current&&_orderHasSupply&&ColonyResource.Find(_orderSupply)!=null;
            return current&&(!_orderHasSupply||supply);
        }
        private void BeginFounding()
        {
            string parentGroup=GroupId;
            Vector3 center=Center,up=EcologyQueries.Up(center);var tangent=Vector3.ProjectOnPlane(UnityEngine.Random.onUnitSphere,up).normalized;
            State.foundingPoint=EcologyQueries.ToLocal(EcologyQueries.Surface(center+tangent*Radius*AntColonySettings.Current.foundingDistanceRatio));
            State.founding=true;State.supplyPoints.Clear();Assign(AntRole.Queen,Guid.NewGuid().ToString("N"));State.supplyPoints.Clear();
            // ★ 新增：在种群标识变化的真实建群入口记录来源，而不是按地理接近猜测谱系。
            PopulationArchive.RecordFounding(parentGroup,GroupId,Population.Mode,Entity.DisplayName);
            Activity=ColonyActivity.Founding;
        }
        private void AdvanceSoldier(float dt)
        {
            // ★ 修复（防Bug回归）：持续可见的猎物不能永久占据饥饿士兵的补给优先级。
            if(ColonyFeeding.Destination(this,out _))return;
            if(!PerceivedTargets.Valid(Genes,HuntTarget,false,out _))HuntTarget=null;
            if(State.scan<=0&&Simulation.ActionDecisionCadence.RoutineTurn(Genes))
            {
                State.scan=AntColonySettings.Current.scanInterval;
                // ★ 适配：士兵从观察收件箱选择，不再为每次巡猎排序全体活体。
                HuntTarget=PerceivedTargets.Nearest(Genes,false,HuntTarget,out _);
            }
            if(HuntTarget!=null){Activity=ColonyActivity.Hunt;return;}
            var settings=AntColonySettings.Current;
            if(State.patrolRemaining>0)
            {
                State.patrolRemaining=Mathf.Max(0,State.patrolRemaining-dt);
                float circumference=2*Mathf.PI*ColonyActivityRange.PatrolCircle(this).Radius*settings.soldierPatrolRadiusRatio;
                State.circleAngle+=360*Genes.GetAttribute("moveSpeed")*dt/Mathf.Max(settings.arrivalDistance,circumference);
                Activity=ColonyActivity.Patrol;if(State.patrolRemaining<=0)State.restRemaining=settings.soldierRestSeconds;
            }
            else
            {State.restRemaining=Mathf.Max(0,State.restRemaining-dt);Activity=ColonyActivity.Rest;if(State.restRemaining<=0)State.patrolRemaining=settings.soldierPatrolSeconds;}
        }
        public void AfterKill(){HuntTarget=null;State.scan=AntColonySettings.Current.soldierPatrolSeconds;State.patrolRemaining=AntColonySettings.Current.soldierPatrolSeconds;State.restRemaining=0;Activity=ColonyActivity.Patrol;}
        public bool Destination(out Vector3 point,out DarwinEntity target,out float multiplier)
            =>Destination(out point,out target,out multiplier,true);

        internal bool Destination(out Vector3 point,out DarwinEntity target,out float multiplier,bool checkSupply)
        {
            point=transform.position;target=null;multiplier=1;if(!Active||Role==AntRole.None)return false;
            // ★ 修复（防Bug回归）：所有分工包括迁居王后先寻可吃补给；专用点优先，备用块与危急改选由同一入口决定。
            if(checkSupply&&ColonyFeeding.Destination(this,out point))return true;
            if(State.founding){point=EcologyQueries.ToWorld(State.foundingPoint);return true;}
            // ★ 修复（防Bug回归）：跟随终点是各自站位，不能让同群成员持续向王后的身体中心挤压。
            if(Young&&!ReliefWorker||Role==AntRole.Mate){point=ColonyFormation.Home(this,Center,AntColonySettings.Current.feedRadius*.8f);return true;}
            // ★ 适配：工匠成年前王后可以临时执行采集/运输，继续共用原有实际货物与扣粮路径。
            if(Role==AntRole.Worker||ColonyFoundingSupport.Collecting(this)){point=AntColonyWorker.Destination(this);return true;}
            if(Role==AntRole.Soldier&&PerceivedTargets.Valid(Genes,HuntTarget,false,out point)){target=HuntTarget;return true;}
            if(Role==AntRole.Soldier&&State.patrolRemaining>0)
            {
                // ★ 适配：巡逻路径与捕猎、图层使用同一批王后和补给活动中心。
                var circle=ColonyActivityRange.PatrolCircle(this);
                point=ColonyFormation.Landward(this,ColonyActivityRange.Boundary(circle,State.circleAngle,AntColonySettings.Current.soldierPatrolRadiusRatio));
            }
            else if(Role==AntRole.Soldier)point=ColonyFormation.Home(this,Center,Radius*.75f);
            return true;
        }
        public bool TrySupplyDestination(out Vector3 point)
        {
            return ColonyFeeding.Destination(this,out point);
        }
        public void EnsureSupplyPoints()
        {
            // ★ 修复（防Bug回归）：王后出生不预建空检查点；只保留当前确有剩余能量的成年补给位置。
            State.supplyPoints.Clear();
            foreach(var item in ResourceGroups.Read(GroupId))if(item.resource.State.kind==ColonyResourceKind.Supply&&item.resource.State.energy>0)
                State.supplyPoints.Add(EcologyQueries.ToLocal(item.position));
        }
        public void EnforceGenome()
        {
            if(Frozen&&!string.IsNullOrEmpty(State.lockedGenome)&&JsonUtility.ToJson(Genes.Genome)!=State.lockedGenome)
                Genes.Genome=JsonUtility.FromJson<Genome>(State.lockedGenome);
        }
        public static IReadOnlyDictionary<string,float> ExpressionSuppression(EntityGeneController genes)
        {
            var member=OptionalComponent.Read<AntColonyMember>(genes);if(member==null||!member.Active||member.Role==AntRole.None)return null;
            if(member._suppressionRole!=member.State.role)
            {
                member._suppression.Clear();foreach(var id in AntColonySettings.Current.Rule(member.Role).suppressedGenes)member._suppression[id]=AntColonySettings.Current.geneSuppression;
                // ★ 适配：旧角色配置已禁用有性繁殖时，其新增出错分支也应禁用，避免工匠/配偶显示可繁殖能力。
                if(member._suppression.ContainsKey(ParthenogenesisEvolutionUpgrade.SourceGeneId))member._suppression[ParthenogenesisEvolutionUpgrade.GeneId]=AntColonySettings.Current.geneSuppression;
                if(member._suppression.ContainsKey(MultipleBirthEvolutionUpgrade.SourceGeneId))member._suppression[MultipleBirthEvolutionUpgrade.GeneId]=AntColonySettings.Current.geneSuppression;
                member._suppressionRole=member.State.role;
            }
            return member._suppression;
        }
        public static float Attribute(EntityGeneController genes,string id,float value)
        {
            var member=Simulation.ActiveSimulationBatch.Read<AntColonyMember>(genes);if(member==null||!member.Active||member.Role==AntRole.None)return value;
            var rule=AntColonySettings.Current.Rule(member.Role);
            if(id=="attack")return member.Young?0:value*rule.attackMultiplier;
            if(id=="moveSpeed")return value*(member.State.founding?1:rule.moveMultiplier);
            return value;
        }
        public AntColonyMemberSave Capture()=>State.Snapshot();
        public void Suspend(){AntColonyWorker.Release(this);HuntTarget=null;_orderQueen=null;_roleQueen=null;_roleElapsed=0;ReliefWorker=false;}
        public void Restore(AntColonyMemberSave state){if(state==null||!state.HasData)return;State=state.Snapshot();if(State.partnerGenome?.Genes?.Count==0)State.partnerGenome=null;_suppressionRole=null;PopulationGroupIndex.Invalidate();Genes.Recalculate();Population.EnsureMembership();if(GetComponent<ColonyMemberMarker>()==null)gameObject.AddComponent<ColonyMemberMarker>();}
    }
}
