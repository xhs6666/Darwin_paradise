using DarwinPark.Entities.Simulation; // ★ 适配：同步模拟批内复用组件，不缓存实时状态数值。
using System.Collections.Generic;
using DarwinPark.Core;
using DarwinPark.Genetics;
using DarwinPark.Entities.Stats;
using DarwinPark.Planet;
using UnityEngine;
using DarwinPark.Entities.Ecology;

namespace DarwinPark.Entities.Logic
{
    // ★ 新增：行为决策从行动基因移出，中枢是唯一自主行为写入者。
    public class CentralGeneLogic : IGeneLogic
    {
        private EntityGeneController _controller;
        private GeneInstance _gene;
        private LocomotionSettings _settings;
        private GenomeEvaluation _expression;
        private int _expressionRevision = -1;
        private bool _primary;
        private LocomotionGeneLogic _motor;
        private string _diet;
        private bool _canDigest;
        private bool _wasDispatched;
        private float _speed;
        private float _actionCooldown, _wanderTimer;
        private Vector3 _wanderPoint;
        // ★ 适配：自主追逐经全局陆地路线，不改变玩家手动输入。
        private DarwinEntity _target;
        private DarwinEntity _lastSeenTarget;
        private Vector3 _observedTarget;
        private readonly Simulation.ActionDecisionCadence _decision=new();
        private long _staffingRevision=-1;
        private float _staffingRemaining;
        private int _adultMembers, _adultWorkers;
        private AntColonyMember _relief;

        public virtual void Initialize(EntityGeneController controller, GeneInstance gene)
        {
            _controller = controller; _gene = gene;
            _settings = LocomotionSettings.Load();
        }

        internal void DispatchColony(AntColonyMember queen,float dt)
        {
            var group=PopulationGroupIndex.DispatchMembers(queen.Population);
            _staffingRemaining-=dt;
            // ★ 适配：库存和成年工匠编制单独刷新，平时一次遍历下发任务，免去每帧为同组重复点名。
            if(_staffingRevision!=PopulationGroupIndex.Revision||_staffingRemaining<=0)
                RefreshStaffing(queen,group);
            // ★ 适配：未成年王后同样能派工；她自己的成年分工仍由原资格约束。
            queen.AdvanceRole(dt,_adultMembers==0&&_relief==null,_adultWorkers);
            queen.AssignMovement(queen,dt);
            foreach(var population in group)
            {
                var member=CurrentMember(population,queen);
                if(member!=null)member.AdvanceDispatchedRole(queen,dt,member==_relief);
            }
        }

        private void RefreshStaffing(AntColonyMember queen,IReadOnlyList<PopulationBehavior> group)
        {
            _staffingRemaining=SimulationOptimization.Current.decisionSeconds;
            _staffingRevision=PopulationGroupIndex.Revision;
            _adultMembers=_adultWorkers=0;
            _relief=null;
            foreach(var population in group)
            {
                var member=CurrentMember(population,queen);
                if(member==null)continue;
                bool worker=member.Role==AntRole.Worker;
                if(!member.Young)
                {
                    _adultMembers++;
                    if(worker)_adultWorkers++;
                }
                // ★ 修复（防Bug回归）：只允许已孵化、有行动能力的幼工临时采集；没有可用幼工则由王后接手。
                else if(_adultMembers==0&&_relief==null&&worker&&!EcologyMotion.Immobilized(member.Genes)&&
                    EcologyMotion.MobileSpeed(member.Genes)>0&&GeneBehaviorAuthority.Primary<CentralGeneLogic>(member.Genes)!=null)
                    _relief=member;
            }
            if(_adultMembers>0)_relief=null;
        }

        private static AntColonyMember CurrentMember(PopulationBehavior population,AntColonyMember queen)
        {
            if(population==null||population==queen.Population||population.GroupId!=queen.GroupId||!population.Active)return null;
            var member=population.ColonyMember;
            return member!=null&&member.Role!=AntRole.None?member:null;
        }
        public void OnUpdate(float dt)
        {
            // ★ 适配：按需采样独立列出行动逻辑，和其包含的队形/邻域子项区分。
            using var performance=PerformanceTimings.Measure(PerformanceTimings.Area.Locomotion);
            if (dt <= 0||!_controller.gameObject.activeInHierarchy) return;
            // ★ 适配：行动授权和马达从当前表达快照读取，避免每次补算重新查找同一基因。
            if (_expressionRevision != _controller.LogicRevision || !ReferenceEquals(_expression, _controller.Evaluation))
            {
                _expressionRevision = _controller.LogicRevision;
                _expression = _controller.Evaluation;
                _primary = GeneBehaviorAuthority.IsPrimary<CentralGeneLogic>(_controller, _gene);
                var action = GeneCapabilities.Active(_controller, BiologySettings.Current.locomotionGene);
                _motor = action == null ? null : _controller.GetLogic(action.Gene) as LocomotionGeneLogic;
                _diet = GeneCapabilities.Diet(_controller);
                _canDigest = NutritionTransfer.CanDigest(_controller);
                _speed = EcologyMotion.MobileLandSpeed(_controller);
            }
            if (!_primary || EcologyMotion.Immobilized(_controller)) return;
            _actionCooldown -= dt;
            var motor = _motor;
            if (motor == null) return;
            var stats = ActiveSimulationBatch.Read<EntityStats>(_controller);
            if (stats == null || !stats.TryGet("health", out float hp) || hp <= 0) return;
            // ★ 适配：使用重算时建立的实例索引，行动子步不重复扫描和分配查询闭包。
            // ★ 修复（防Bug回归）：行为状态共用面板评估结果，失效基因不执行行动。
            // ★ 适配：手动控制期间 AI 不再抢写速度，只有玩家按攻击键才执行近距离进食。
            var manual = ActiveSimulationBatch.Read<EntityManualControl>(_controller);
            if (manual != null && manual.IsControlled)
            {
                _decision.Reset();
                // ★ 修复（防Bug回归）：瞬时左键请求保留到基因逻辑消费，不能只依赖两个 Update 之间是否仍按住。
                // ★ 修复（防Bug回归）：冷却期间不消费短点击，留到可攻击的模拟步，避免点击被无声丢弃。
                if (_actionCooldown > 0) return;
                bool requested=manual.ConsumeAttack() || manual.AttackHeld;
                if (manual.CanMove && requested && _controller.GetAttribute("attack")>0)
                {
                    // ★ 修复（防Bug回归）：手动每次攻击按准星重新选取，不沿用 AI 的最近猎物缓存。
                    bool graze=_diet=="herbivore";
                    _target=manual.GetAttackTarget();
                    // 玩家主动攻击不应被 AI 的食性筛选拒绝；没有行动能力仍无法攻击。
                    if(_target!=null)TryBite(stats,graze);
                }
                return;
            }
            var member=ActiveSimulationBatch.Read<AntColonyMember>(_controller);
            bool colonyMember=member!=null&&member.Active;
            AntColonyMember queen=null;
            bool dispatched=colonyMember&&member.Dispatcher(out queen)!=null;
            if(dispatched!=_wasDispatched){_decision.Reset();_target=null;_lastSeenTarget=null;_wasDispatched=dispatched;}
            float speed=dispatched?EcologyMotion.CurrentMobileSpeed(_controller,member.AssignedLandSpeed(queen)):
                colonyMember?EcologyMotion.MobileSpeed(_controller):EcologyMotion.CurrentMobileSpeed(_controller,_speed);
            if(speed<=0)return;
            // ★ 适配：查询/任务决策按模拟节奏缓存，持续移动与咬合冷却保持逐步推进。
            // ★ 新增：用户顺序任务具有实际目标和完成条件，不能被普通巡逻/随机觅食覆盖。
            var plan=ActiveSimulationBatch.Read<EntityActivityPlan>(_controller);
            if(plan!=null&&plan.Active)
            {
                _decision.Reset();plan.Destination(out var plannedPoint,out _target);motor.Move(plannedPoint,speed,dt);
                if(_target!=null)TryBite(stats,_diet=="herbivore");return;
            }
            if(dispatched)
            {
                // ★ 新增：成员中枢不运行个体感知、觅食与巡逻决策。即时指令和物理执行仍由本实体处理。
                bool assigned=member.AssignedMovement(queen,out var point,out var target,out float multiplier,out bool supply);
                if(!supply&&EcologyMotion.OverrideDirective(_controller,out var urgentPoint,out var urgentTarget))
                    MoveDirective(stats,urgentPoint,urgentTarget,speed,dt);
                else
                    MoveDirective(stats,assigned?point:_controller.transform.position,assigned?target:null,speed*multiplier,dt);
                return;
            }
            bool decide=_decision.Due(_controller,dt);
            // ★ 适配：状态迁移从逐小步移到意图刷新。食物容量和饱食度仍由各结算入口实时读取。
            if(decide)
            {
                if(!colonyMember)_controller.RefreshMovementState(dt);
                _decision.directive=EcologyMotion.Directive(_controller,out _decision.point,out _decision.target,out _decision.multiplier);
            }
            if(_decision.directive)
            {
                if(!MoveDirective(stats,_decision.point,_decision.target,speed*_decision.multiplier,dt))_decision.Reset();
                return;
            }
            // ★ 适配：由生态预算决定是否需补充繁殖储备，仓储充足后避免过度捕食。
            if ((_diet == "hunt" || _diet == "herbivore") &&
                (_canDigest||colonyMember&&member.Role==AntRole.Soldier) &&
                EcologyEnergyBudget.WantsFood(_controller,stats,_settings.hungerThreshold))
            {
                bool graze = _diet == "herbivore";
                if(decide)FindTarget(graze);
                else if(_target!=null&&!PerceivedTargets.Valid(_controller,_target,graze,out _observedTarget))_target=null;
                if (_target != null)
                {
                    _lastSeenTarget=_target;
                    motor.Move(_observedTarget, speed, dt);
                    TryBite(stats, graze);
                    return;
                }
                if(_lastSeenTarget!=null&&EntityPerceptionHub.TryRecall(_controller,_lastSeenTarget,out var known)&&LandReachability.CanReach(_controller,_lastSeenTarget,known.position))
                {motor.Move(known.position,speed,dt);return;}
                // 没有猎物时继续搜索漫游，不能永久 return 原地饿死。
            }
            _target = null;_lastSeenTarget=null;
            _wanderTimer -= dt;
            if (_wanderTimer <= 0)
            {
                _wanderTimer = _settings.wanderInterval * Random.Range(.75f,1.15f);
                _wanderPoint = EcologyQueries.ToLocal(motor.WanderDestination(Random.onUnitSphere,_settings.wanderDistance));
            }
            motor.Move(EcologyQueries.ToWorld(_wanderPoint), speed * _settings.wanderSpeedRatio, dt);
        }

        private bool MoveDirective(EntityStats stats,Vector3 point,DarwinEntity target,float speed,float dt)
        {
            _target=target;
            if(target!=null)
            {
                var explicitOrder=ActiveSimulationBatch.Read<RtsOrderController>(_controller);
                var pop=ActiveSimulationBatch.Read<PopulationBehavior>(_controller);
                if(explicitOrder?.State.active==true&&explicitOrder.State.kind==RtsOrderKind.Attack||pop?.ManualRemaining>0)
                    EntityNeighborhood.TryPosition(target,out point);
                else if(!EntityPerceptionHub.TryLocate(_controller,target,out point))
                {
                    if(EntityPerceptionHub.TryRecall(_controller,target,out var known)&&LandReachability.CanReach(_controller,target,known.position))
                    {_motor.Move(known.position,speed,dt);return true;}
                    _target=null;
                    return false;
                }
            }
            _motor.Move(point,speed,dt);
            if(_target!=null)TryBite(stats,false);
            return true;
        }
        private void TryBite(EntityStats stats, bool graze)
        {
            if (_target == null || _actionCooldown > 0) return;
            // ★ 修复（防Bug回归）：手动攻击、群体命令和缓存猎物同样不得捕食更大体型。
            if(!PredationSizePolicy.Allows(_controller,_target)){_target=null;return;}
            var otherStats = _target.GetComponent<EntityStats>();
            var a = ActiveSimulationBatch.Read<Collider>(_controller); var b = _target.GetComponent<Collider>();
            float gap = a != null && b != null ? Vector3.Distance(a.ClosestPoint(b.bounds.center),b.ClosestPoint(a.bounds.center)) : Vector3.Distance(_controller.transform.position,_target.transform.position);
            // ★ 修复（防Bug回归）：手动近战为准星判定留少量距离容差，AI的追击距离保持原有配置。
            float reach=_settings.attackReach+(ActiveSimulationBatch.Read<EntityManualControl>(_controller)?.IsControlled==true?GameplayTuning.Current.manualAttackReachBonus:0);
            if (gap > reach || otherStats == null || !otherStats.TryGet("health",out float health) || health <= 0) return;
            float damage = graze ? Mathf.Min(_settings.grazeDamage,_controller.GetAttribute("attack")) : _controller.GetAttribute("attack");
            if (damage <= 0) return;
            _actionCooldown = _settings.attackInterval;
            // ★ 适配：仅致死结算产生可吸收营养，非致死伤害不会凭空增加食物。
            EntityCombat.Ensure(_target.gameObject).Receive(ActiveSimulationBatch.Read<DarwinEntity>(_controller),damage);
            otherStats.TryGet("health",out float remainingHealth);
            // ★ 适配：士兵只造成伤害并留下运输尸体，不再运行普通进食或群体分红。
            if(Ecology.AntColonyCombat.SettleSoldierAttack(_controller,_target,remainingHealth<=0))
            // ★ 修复（防Bug回归）：尸体已成为独立资源，立即注销死者，避免同帧存档仍保存可重复恢复的原实体。
            {if(remainingHealth<=0){_target.gameObject.SetActive(false);Object.Destroy(_target.gameObject);_target=null;}return;}
            // ★ 修复（防Bug回归）：伤害不是食物；击败后的消化与剩余尸体在EntityCombat中结算，非致死攻击不再凭空补粮。
            if(remainingHealth<=0)_target=null;
        }
        private void FindTarget(bool graze)
        {
            // ★ 适配：保留食性/体型/同群/寄生资格，只把全表搜索替换为视野内坐标投递。
            _target=PerceivedTargets.Nearest(_controller,graze,_target,out _observedTarget);
        }
        public virtual string GetTooltipInfo()
        {
            var member=ActiveSimulationBatch.Read<AntColonyMember>(_controller);
            AntColonyMember queen=null;
            bool dispatched=member!=null&&member.Active&&member.Dispatcher(out queen)!=null;
            if(dispatched&&queen!=member)
                return "个体自主决策已抑制，由王后中枢派工。";
            if(queen!=null&&queen==member)return "王后中枢统一分配种群的补给、运输和巡逻。";
            return _target!=null?"正在追击："+_target.DisplayName:"中枢负责当前状态与移动意图。营养和伤害由独立系统结算。";
        }
    }
}
