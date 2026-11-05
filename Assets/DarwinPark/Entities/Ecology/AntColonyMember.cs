using System;
using System.Collections.Generic;
using System.Linq;
using DarwinPark.Genetics;
using UnityEngine;

namespace DarwinPark.Entities.Ecology
{
    [Serializable] public sealed class AntColonyMemberSave
    {
        public string role,lockedGenome,resourceTarget,livingTarget,carriedResource,carriedLiving;
        public float scan,patrolRemaining,restRemaining,circleAngle,reproductionTimer,reproductionCooldown;
        // ★ 适配：王后无配偶等待和孤雌孕育状态与已有蚁群快照一起持久化。
        public float mateWait;public bool parthenogenetic;
        public bool founding,deliveryNursery,originalKinematic,originalDetect;
        public Vector3 foundingPoint,deliveryPoint;
        public List<Vector3> supplyPoints=new List<Vector3>();
        public Genome partnerGenome;
        public bool HasData=>!string.IsNullOrEmpty(role);
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
        public bool Young=>Genes.CurrentAge==AgeState.Infant||GetComponent<EggIncubation>()?.Incubating==true;
        public string GroupId=>Population.GroupId;
        public string Id=>EcologyIdentity.Of(Entity);
        public IEnumerable<AntColonyMember> Group=>Population.GroupMembers.Select(p=>p.GetComponent<AntColonyMember>()).Where(m=>m!=null&&m.Active);
        public AntColonyMember Queen=>PopulationGroupIndex.Queen(Population);
        public Vector3 Center {get{var queen=Queen;return queen!=null?queen.transform.position:transform.position;}}
        public float Radius=>Mathf.Max(AntColonySettings.Current.minimumColonyRadius,Genes.GetAttribute("territory_radius"),AntColonySettings.Current.colonyRadius)*DarwinPark.Core.GameplayTuning.Current.colonyActivityRadiusMultiplier;
        public bool Frozen=>Active&&Role!=AntRole.None&&AntColonySettings.Current.Rule(Role).frozenGenome;
        public ColonyActivity Activity {get;internal set;}
        internal DarwinEntity HuntTarget;
        private readonly Dictionary<string,float> _suppression=new Dictionary<string,float>();
        private string _suppressionRole;
        private void Awake(){Genes=GetComponent<EntityGeneController>();Entity=GetComponent<DarwinEntity>();Population=GetComponent<PopulationBehavior>();}
        private void OnEnable(){if(!Members.Contains(this))Members.Add(this);}
        private void OnDisable(){AntColonyWorker.Release(this);Members.Remove(this);}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] private static void Reset()=>Members.Clear();
        public static AntColonyMember Ensure(PopulationBehavior pop)=>pop.GetComponent<AntColonyMember>()??pop.gameObject.AddComponent<AntColonyMember>();
        public static AntColonyMember QueenFor(PopulationBehavior pop)=>pop.GetComponent<AntColonyMember>()?.Queen;
        public static bool IsManaged(EntityGeneController genes)=>genes!=null&&genes.GetComponent<AntColonyMember>()?.Active==true;
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
            var source=parent.GetComponent<AntColonyMember>();if(source==null||!source.Active||child==null)return;
            var pop=child.GetComponent<PopulationBehavior>();if(pop==null)return;
            var member=Ensure(pop);member.Assign(source.ChooseBirthRole(),source.GroupId);
        }
        public AntRole ChooseBirthRole()
        {
            var settings=AntColonySettings.Current;
            if(Group.Count()>=settings.reproductivePopulationThreshold&&Group.Count(m=>m.Role==AntRole.Reproductive)<settings.minimumReproductives)return AntRole.Reproductive;
            var choices=settings.roles.Where(r=>r.birthWeight>0&&(r.Role!=AntRole.Reproductive||Group.Count(m=>m.Role==AntRole.Reproductive)<settings.maximumReproductives)).ToArray();
            float roll=UnityEngine.Random.value*choices.Sum(r=>r.birthWeight);
            foreach(var rule in choices){roll-=rule.birthWeight;if(roll<=0)return rule.Role;}return choices[choices.Length-1].Role;
        }
        public void Advance(float dt)
        {
            if(!Active||dt<=0)return;
            if(Role==AntRole.None)Assign(Population.LeaderFlag?AntRole.Queen:ChooseBirthRole());
            State.scan-=dt;State.reproductionCooldown=Mathf.Max(0,State.reproductionCooldown-dt);
            ColonyResource.Feed(this,dt);
            if(GetComponent<EggIncubation>()?.Incubating==true)return;
            if(Young){Activity=ColonyActivity.Young;return;}
            if(Role==AntRole.Reproductive){BeginFounding();return;}
            if(State.founding)
            {
                Activity=ColonyActivity.Founding;
                if(EcologyQueries.Distance(transform.position,EcologyQueries.ToWorld(State.foundingPoint))<=AntColonySettings.Current.arrivalDistance)
                {State.founding=false;EnsureSupplyPoints();GetComponent<TerritoryBehavior>()?.Restore(EcologyQueries.ToLocal(transform.position),true,0,Vector3.zero);}
                return;
            }
            if(Role==AntRole.Queen){Activity=ColonyActivity.Reproduce;AntColonyReproduction.Advance(this,dt);return;}
            if(Role==AntRole.Worker){AntColonyWorker.Advance(this,dt);return;}
            if(Role==AntRole.Mate){Activity=ColonyActivity.Mate;return;}
            AdvanceSoldier(dt);
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
            if(!EcologyQueries.Alive(HuntTarget)||!EcologyMotion.CanHunt(Genes,HuntTarget)||!EcologyMotion.Detectable(HuntTarget,transform.position,Genes.GetAttribute("senseRange"),Entity))HuntTarget=null;
            if(State.scan<=0)
            {
                State.scan=AntColonySettings.Current.scanInterval;
                float range=Genes.GetAttribute("senseRange");Vector3 position=transform.position;
                // ★ 修复（防Bug回归）：先筛感知距离，再执行体型、同群和领地判断，分散的其他种群不消耗完整捕猎判定。
                HuntTarget=EntityRegistry.All.Where(e=>e!=Entity&&(e.transform.position-position).sqrMagnitude<=range*range&&EcologyQueries.Alive(e)&&EcologyMotion.CanHunt(Genes,e)&&EcologyMotion.Detectable(e,position,range,Entity))
                    .OrderBy(e=>(e.transform.position-transform.position).sqrMagnitude).FirstOrDefault();
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
        {
            point=transform.position;target=null;multiplier=1;if(!Active||Role==AntRole.None)return false;
            if(State.founding){point=EcologyQueries.ToWorld(State.foundingPoint);return true;}
            // ★ 修复（防Bug回归）：所有已定居分工先寻可用补给，王后/幼体前往育幼能量块，成年成员前往普通补给。
            if(ColonyFeeding.Destination(this,out point))return true;
            if(Young){point=Center;return true;}
            if(Role==AntRole.Mate){point=Center;return true;}
            if(Role==AntRole.Worker){point=AntColonyWorker.Destination(this);return true;}
            if(Role==AntRole.Soldier&&EcologyQueries.Alive(HuntTarget)){target=HuntTarget;point=target.transform.position;return true;}
            if(Role==AntRole.Soldier&&State.patrolRemaining>0)
            {
                // ★ 适配：巡逻路径与捕猎、图层使用同一批王后和补给活动中心。
                var circle=ColonyActivityRange.PatrolCircle(this);
                point=ColonyActivityRange.Boundary(circle,State.circleAngle,AntColonySettings.Current.soldierPatrolRadiusRatio);
            }
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
            foreach(var resource in ColonyResource.All)if(resource.State.group==GroupId&&resource.State.kind==ColonyResourceKind.Supply)
                State.supplyPoints.Add(EcologyQueries.ToLocal(resource.transform.position));
        }
        public void EnforceGenome()
        {
            if(Frozen&&!string.IsNullOrEmpty(State.lockedGenome)&&JsonUtility.ToJson(Genes.Genome)!=State.lockedGenome)
                Genes.Genome=JsonUtility.FromJson<Genome>(State.lockedGenome);
        }
        public static IReadOnlyDictionary<string,float> ExpressionSuppression(EntityGeneController genes)
        {
            var member=genes.GetComponent<AntColonyMember>();if(member==null||!member.Active||member.Role==AntRole.None)return null;
            if(member._suppressionRole!=member.State.role)
            {
                member._suppression.Clear();foreach(var id in AntColonySettings.Current.Rule(member.Role).suppressedGenes)member._suppression[id]=AntColonySettings.Current.geneSuppression;
                // ★ 适配：旧角色配置已禁用有性繁殖时，其新增出错分支也应禁用，避免工匠/配偶显示可繁殖能力。
                if(member._suppression.ContainsKey(ParthenogenesisEvolutionUpgrade.SourceGeneId))member._suppression[ParthenogenesisEvolutionUpgrade.GeneId]=AntColonySettings.Current.geneSuppression;
                member._suppressionRole=member.State.role;
            }
            return member._suppression;
        }
        public static float Attribute(EntityGeneController genes,string id,float value)
        {
            var member=genes.GetComponent<AntColonyMember>();if(member==null||!member.Active||member.Role==AntRole.None)return value;
            var rule=AntColonySettings.Current.Rule(member.Role);
            if(id=="attack")return member.Young?0:value*rule.attackMultiplier;
            if(id=="moveSpeed")return value*(member.State.founding?1:rule.moveMultiplier);
            return value;
        }
        public AntColonyMemberSave Capture()=>JsonUtility.FromJson<AntColonyMemberSave>(JsonUtility.ToJson(State));
        public void Suspend(){AntColonyWorker.Release(this);HuntTarget=null;}
        public void Restore(AntColonyMemberSave state){if(state==null||!state.HasData)return;State=JsonUtility.FromJson<AntColonyMemberSave>(JsonUtility.ToJson(state));if(State.partnerGenome?.Genes?.Count==0)State.partnerGenome=null;_suppressionRole=null;PopulationGroupIndex.Invalidate();Genes.Recalculate();Population.EnsureMembership();if(GetComponent<ColonyMemberMarker>()==null)gameObject.AddComponent<ColonyMemberMarker>();}
    }
}
