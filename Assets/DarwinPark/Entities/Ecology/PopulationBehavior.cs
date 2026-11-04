using System;
using System.Collections.Generic;
using System.Linq;
using DarwinPark.Core;
using DarwinPark.Entities.Stats;
using UnityEngine;
namespace DarwinPark.Entities.Ecology
{
    public enum PopulationCommand {None,Hunt,Gather,Rally,Defend}
    // ★ 新增：群体消息只在同一持久化群体内直接分发；没有持有死亡实体的全局静态事件。
    [DefaultExecutionOrder(-190)]
    public sealed class PopulationBehavior : MonoBehaviour
    {
        private static readonly List<PopulationBehavior> Members=new List<PopulationBehavior>();
        private static long _serial;
        internal static IReadOnlyList<PopulationBehavior> RegisteredMembers=>Members;
        private EntityGeneController _genes;private DarwinEntity _entity;private EcologySettings _settings;
        private EntityStats _stats;
        private string _mode;private float _scan,_manualRemaining,_duelRemaining;
        public string GroupId {get;private set;}="";
        public long JoinOrder {get;private set;}
        public bool LeaderFlag {get;private set;}
        public PopulationCommand Command {get;private set;}
        public float CommandRemaining {get;private set;}
        public Vector3 CommandPointLocal {get;private set;}
        public DarwinEntity Target {get;private set;}
        public float DuelRemaining=>_duelRemaining;
        // ★ 修复（防Bug回归）：孵化限制的是行动而非血缘，卵必须保留同群标记，避免下一帧退群、被士兵误伤或读档后随机入群。
        private Genetics.GenomeEvaluation _populationEvaluation;
        private Genetics.GeneEvaluation _populationGene;
        private Genetics.GeneEvaluation PopulationGene
        {
            get
            {
                // ★ 修复（防Bug回归）：一次表达只选一次种群基因，属性读取不能不断分配 LINQ 迭代器和排序数组。
                if(!ReferenceEquals(_populationEvaluation,_genes.Evaluation))
                {
                    _populationEvaluation=_genes.Evaluation;_populationGene=null;
                    if(_populationEvaluation!=null)foreach(var gene in _populationEvaluation.Genes)
                        if(gene.IsActive&&gene.Definition.Id==_settings.populationGene&&(_populationGene==null||gene.EffectiveStrength>_populationGene.EffectiveStrength))_populationGene=gene;
                    PopulationGroupIndex.Invalidate();
                }
                return _populationGene;
            }
        }
        public string Mode=>PopulationGene?.State?.StateId??"";
        // ★ 适配：缓存属性组件但实时读取生命值，同帧死亡立即生效，不用每次关系查询都查找相同组件。
        private bool Alive {get{if(_stats==null)_stats=GetComponent<EntityStats>();return _entity!=null&&_entity.isActiveAndEnabled&&_stats!=null&&_stats.TryGet("health",out float hp)&&hp>0;}}
        public bool Active=>PopulationGene!=null&&Alive&&
            (GetComponent<EggIncubation>()?.Incubating!=true||
             Mode==_settings.antState&&!string.IsNullOrEmpty(GroupId)&&GetComponent<AntColonyMember>()?.Role is AntRole role&&role!=AntRole.None);
        // ★ 适配：蚁群领袖由王后分工确定，狼群仍沿用原有决斗与加入顺序。
        public PopulationBehavior Leader=>PopulationGroupIndex.Leader(this);
        public bool IsLeader=>Active&&Leader==this;
        public IEnumerable<PopulationBehavior> GroupMembers=>PopulationGroupIndex.Members(this);
        public int MemberCount=>GroupMembers.Count();
        public float Aura=>Mathf.Max(_settings.arrivalDistance,_genes.GetAttribute("leadership_aura"));
        public bool InAura=>Leader is PopulationBehavior leader&&EcologyQueries.Distance(transform.position,leader.transform.position)<=leader.Aura;
        public float MoveMultiplier=>!Active?1:Mode==_settings.antState?_settings.antBodyMultiplier*(InAura?1+_settings.antSpeedBonus*_genes.GetAttribute("coordination"):1):1;
        public float AttackMultiplier
        {
            get
            {
                if(!Active)return 1;if(Mode==_settings.antState)return _settings.antBodyMultiplier;if(!InAura)return 1;
                // ★ 适配：狼群协同由真实附近同群成员贡献，独狼不凭空获得群攻加成，贡献上限由配置控制。
                int helpers=Mathf.Min(_settings.maximumCoordinators,GroupMembers.Count(m=>m!=this&&EcologyQueries.Distance(transform.position,m.transform.position)<=Aura));
                return 1+_settings.wolfAttackBonus*_genes.GetAttribute("coordination")*helpers;
            }
        }
        private void Awake(){_genes=GetComponent<EntityGeneController>();_entity=GetComponent<DarwinEntity>();_settings=EcologySettings.Load();EcologyIdentity.Ensure(gameObject);_genes.OnEvaluationChanged+=OnGenesChanged;}
        // ★ 修复（防Bug回归）：表达变更必须立即使关系缓存失效，不能等下一帧才移除同帧禁用/变为其他模式的成员。
        private void OnGenesChanged(Genetics.GenomeEvaluation evaluation){_populationEvaluation=null;_populationGene=null;PopulationGroupIndex.Invalidate();}
        private void OnDestroy(){if(_genes!=null)_genes.OnEvaluationChanged-=OnGenesChanged;}
        private void OnDisable(){Members.Remove(this);PopulationGroupIndex.Invalidate();}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset(){Members.Clear();_serial=0;}
        private void Update(){Advance(ServiceLocator.TryGet<SimulationClock>(out var clock)?clock.GameDeltaTime:Time.deltaTime);}
        public void Advance(float dt)
        {
            // ★ 适配：副本保留读档的身份与分工，只由房主计算成员变化。
            if(WorldAuthority.IsReplica)return;
            if(!Active){GetComponent<AntColonyMember>()?.Suspend();if(Members.Remove(this)||!string.IsNullOrEmpty(GroupId))PopulationGroupIndex.Invalidate();GroupId="";LeaderFlag=false;Command=PopulationCommand.None;return;}
            if(dt<=0)return;
            string previousGroup=_mode!=null&&_mode!=Mode?GroupId:null;
            if(_mode!=null&&_mode!=Mode){GetComponent<AntColonyMember>()?.Suspend();Members.Remove(this);PopulationGroupIndex.Invalidate();GroupId="";LeaderFlag=false;Command=PopulationCommand.None;}
            _mode=Mode;EnsureMembership();
            // ★ 适配：种群状态改变后独立成群也记录真实来源；加入既有种群不会改写其谱系。
            if(!string.IsNullOrEmpty(previousGroup)&&previousGroup!=GroupId&&LeaderFlag)PopulationArchive.RecordFounding(previousGroup,GroupId,Mode,_entity.DisplayName,"种群状态变化形成新种群");
            if(GetComponent<EggIncubation>()?.Incubating==true)return;
            // ★ 适配：固定分工独立推进，旧全群采集与决斗不能覆盖蚁群角色。
            if(Mode==_settings.antState){AntColonyMember.Ensure(this).Advance(dt);return;}
            _duelRemaining=Mathf.Max(0,_duelRemaining-dt);_manualRemaining=Mathf.Max(0,_manualRemaining-dt);
            CommandRemaining=Mathf.Max(0,CommandRemaining-dt);
            if(CommandRemaining<=0||Command==PopulationCommand.Hunt&&(!EcologyQueries.Alive(Target)||SameGroup(Target))){Command=PopulationCommand.None;Target=null;}
            var leader=Leader;if(leader!=null&&!leader.LeaderFlag)leader.LeaderFlag=true;
            if(!IsLeader&&leader!=null&&_duelRemaining<=0&&leader._duelRemaining<=0&&EcologyQueries.Distance(transform.position,leader.transform.position)<=_settings.arrivalDistance&&Score()>leader.Score()*_settings.duelAdvantage)
                ChallengeLeadership(out _);
            _scan-=dt;
            if(IsLeader&&_scan<=0&&_manualRemaining<=0){_scan=_settings.scanInterval;AutoCommand();}
            if(Mode==_settings.antState&&Command==PopulationCommand.Gather&&EcologyQueries.Distance(transform.position,EcologyQueries.ToWorld(CommandPointLocal))<=_settings.arrivalDistance&&Planet.PlanetOreSystem.Instance!=null)
            {
                float auraBonus=InAura?1+_settings.antGatherBonus:1;
                float extracted=Planet.PlanetOreSystem.Instance.ConsumeOre(transform.position,_settings.antGatherRate*Mathf.Max(0,_genes.GetAttribute("coordination"))*auraBonus*dt);
                _entity.GetComponent<EntityStats>().Feed(extracted);
            }
        }
        public void EnsureMembership()
        {
            if(!Active)return;
            if(string.IsNullOrEmpty(GroupId))
            {
                var neighbor=Members.Where(m=>m!=null&&m!=this&&m.Active&&m.Mode==Mode&&EcologyQueries.Distance(transform.position,m.transform.position)<=_settings.joinRadius)
                    .OrderBy(m=>m.JoinOrder).FirstOrDefault();
                GroupId=neighbor?.GroupId??Guid.NewGuid().ToString("N");JoinOrder=++_serial;LeaderFlag=neighbor==null;
                // ★ 新增：新建种群记录真实成立时间；读档期间不把首次观察误写成出生。
                if(neighbor==null&&WorldRuntimeGate.Ready&&!WorldRuntimeGate.Loading&&!WorldAuthority.IsReplica)PopulationArchive.Register(GroupId,Mode,_entity.DisplayName,true);
            }
            _serial=Math.Max(_serial,JoinOrder);if(!Members.Contains(this)){Members.Add(this);PopulationGroupIndex.Invalidate();}
        }
        // ★ 适配：出生继承和新王后建群通过此接口写入持久标识，不重新按距离随机入群。
        public void JoinColony(string group,bool queen)
        {PopulationGroupIndex.Invalidate();GroupId=group;JoinOrder=++_serial;LeaderFlag=queen;Command=PopulationCommand.None;Target=null;CommandRemaining=0;EnsureMembership();}
        public bool SameGroup(DarwinEntity other)=>Active&&other!=null&&other.GetComponent<PopulationBehavior>() is PopulationBehavior pop&&pop.Active&&pop.Mode==Mode&&!string.IsNullOrEmpty(GroupId)&&pop.GroupId==GroupId;
        private float Score(){_entity.GetComponent<EntityStats>().TryGet("health",out var health);return _genes.GetAttribute("attack")+health*_settings.duelHealthWeight;}
        public bool ChallengeLeadership(out string message)
        {
            // ★ 修复（防Bug回归）：王后身份由出生分工确定，任何调用入口都不能用狼群决斗改写蚁群领袖，隐藏按钮不足以保证此约束。
            if(Mode==_settings.antState){message="蚁群王后不能被挑战";return false;}
            EnsureMembership();var leader=Leader;
            if(!Active||leader==null||leader==this){message="当前没有可挑战的领袖";return false;}
            if(_duelRemaining>0||leader._duelRemaining>0){message="决斗仍在冷却";return false;}
            if(EcologyQueries.Distance(transform.position,leader.transform.position)>_settings.joinRadius){message="请先靠近领袖";return false;}
            _duelRemaining=leader._duelRemaining=_settings.duelCooldown;
            bool won=Score()>leader.Score()*_settings.duelAdvantage;
            if(won){foreach(var member in GroupMembers)member.LeaderFlag=false;LeaderFlag=true;PopulationGroupIndex.Invalidate();}
            message=won?"决斗获胜，成为领袖":"决斗失败，原领袖留任";return won;
        }
        public bool Issue(PopulationCommand command,DarwinEntity target,Vector3 point,bool manual,out string message)
        {
            EnsureMembership();if(!IsLeader){message="只有领袖可以下令";return false;}
            // ★ 适配：领袖选择目标也服从体型判定，成员执行时仍须独立检查自己的体型。
            if(command==PopulationCommand.Hunt&&(!EcologyQueries.Alive(target)||SameGroup(target)||!PredationSizePolicy.Allows(_genes,target)||EcologyQueries.Distance(transform.position,target.transform.position)>_settings.maximumCommandRange))
            {message="需要范围内的非同群存活猎物";return false;}
            foreach(var member in GroupMembers)
            {
                member.Command=command;member.Target=target;member.CommandPointLocal=EcologyQueries.ToLocal(point);member.CommandRemaining=_settings.commandLifetime;
                member._manualRemaining=manual?_settings.commandLifetime:0;
            }
            message="已下令："+CommandLabel(command);return true;
        }
        private void AutoCommand()
        {
            // ★ 适配：自动群猎尊重食性，手动下令仍可指挥防御攻击；食腐/幼年寄生不会因狼群模式被迫捕猎。
            if(Mode==_settings.wolfState&&NutritionTransfer.CanHuntForFood(_genes))
            {
                var prey=EntityRegistry.All.Where(e=>EcologyQueries.Alive(e)&&e!=_entity&&!SameGroup(e)&&NutritionTransfer.FoodTargetAllowed(_genes,e)&&PredationSizePolicy.Allows(_genes,e)&&EcologyMotion.Detectable(e,transform.position,_genes.GetAttribute("senseRange"),_entity))
                    .OrderBy(e=>(e.transform.position-transform.position).sqrMagnitude).FirstOrDefault();
                if(prey!=null){Issue(PopulationCommand.Hunt,prey,prey.transform.position,false,out _);return;}
            }
            if(Mode==_settings.antState&&FindOre(out var ore)){Issue(PopulationCommand.Gather,null,ore,false,out _);return;}
            var territory=GetComponent<TerritoryBehavior>();Issue(PopulationCommand.Rally,null,territory!=null?territory.Center:transform.position,false,out _);
        }
        public bool FindOre(out Vector3 point)
        {
            point=transform.position;var ore=Planet.PlanetOreSystem.Instance;if(ore==null)return false;
            float best=ore.GetOreAmount(point);Vector3 up=EcologyQueries.Up(point),axis=Vector3.Cross(up,Vector3.forward).normalized;
            if(axis.sqrMagnitude<.5f)axis=Vector3.Cross(up,Vector3.right).normalized;
            for(int i=0;i<_settings.oreSearchSamples;i++)
            {
                Vector3 candidate=EcologyQueries.Surface(transform.position+Quaternion.AngleAxis(i*137.50776f,up)*axis*Aura*Mathf.Sqrt((i+1f)/_settings.oreSearchSamples));
                float value=ore.GetOreAmount(candidate);if(value>best){best=value;point=candidate;}
            }
            return best>0;
        }
        public bool Destination(out Vector3 point,out DarwinEntity target)
        {
            point=transform.position;target=null;if(!Active||Command==PopulationCommand.None||CommandRemaining<=0)return false;
            if(Command==PopulationCommand.Hunt)
            {
                if(_manualRemaining<=0&&!NutritionTransfer.FoodTargetAllowed(_genes,Target))return false;
                if(!EcologyQueries.Alive(Target)||SameGroup(Target)||!PredationSizePolicy.Allows(_genes,Target))return false;
                target=Target;point=Target.transform.position;
                var peers=GroupMembers.OrderBy(m=>m.JoinOrder).ToList();int index=Mathf.Max(0,peers.IndexOf(this));
                if(EcologyQueries.Distance(transform.position,point)>_settings.formationSpacing*2)
                {
                    Vector3 up=EcologyQueries.Up(point),side=Vector3.Cross(up,transform.position-point).normalized;
                    point=EcologyQueries.Surface(point+Quaternion.AngleAxis(index*360f/Mathf.Max(1,peers.Count),up)*side*_settings.formationSpacing);
                }
                return true;
            }
            point=EcologyQueries.ToWorld(CommandPointLocal);return true;
        }
        public void ShareReward(float amount)
        {
            var others=GroupMembers.Where(m=>m!=this&&EcologyQueries.Distance(transform.position,m.transform.position)<=Aura).ToArray();
            float shared=others.Length>0?amount*Mathf.Clamp01(_settings.shareFraction):0;
            _entity.GetComponent<EntityStats>().Feed(amount-shared);
            foreach(var member in others)member._entity.GetComponent<EntityStats>().Feed(shared/others.Length);
        }
        public void Restore(EcologySaveState state)
        {
            Members.Remove(this);PopulationGroupIndex.Invalidate();GroupId=state.groupId??"";JoinOrder=state.joinOrder;LeaderFlag=state.leader;_mode=state.mode;
            Command=state.command;CommandRemaining=state.commandRemaining;CommandPointLocal=state.commandPoint;_manualRemaining=state.manualRemaining;_duelRemaining=state.duelRemaining;
            EnsureMembership();
        }
        public float ManualRemaining=>_manualRemaining;
        public void RestoreTarget(DarwinEntity target){Target=target;if(Command==PopulationCommand.Hunt&&!EcologyQueries.Alive(target))Command=PopulationCommand.None;}
        public static string CommandLabel(PopulationCommand command)=>command switch {PopulationCommand.Hunt=>"捕猎",PopulationCommand.Gather=>"采集",PopulationCommand.Rally=>"集结",PopulationCommand.Defend=>"守卫",_=>"自由活动"};
    }
}
