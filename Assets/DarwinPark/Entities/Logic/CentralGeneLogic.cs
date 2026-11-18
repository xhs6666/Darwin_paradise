using DarwinPark.Entities.Simulation; // ★ 适配：同步模拟批内复用组件，不缓存实时状态数值。
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
        private float _actionCooldown, _wanderTimer;
        private Vector3 _wanderPoint;
        // ★ 适配：自主追逐经全局陆地路线，不改变玩家手动输入。
        private DarwinEntity _target;
        private DarwinEntity _lastSeenTarget;
        private Vector3 _observedTarget;
        private readonly Simulation.ActionDecisionCadence _decision=new();

        public virtual void Initialize(EntityGeneController controller, GeneInstance gene)
        {
            _controller = controller; _gene = gene;
            _settings = LocomotionSettings.Load();
        }
        public void OnUpdate(float dt)
        {
            // ★ 适配：按需采样独立列出行动逻辑，和其包含的队形/邻域子项区分。
            using var performance=PerformanceTimings.Measure(PerformanceTimings.Area.Locomotion);
            // ★ 适配：社会谱系和复制行动基因只保留一个行为写入者；寄生/孵化自行控制移动。
            if(!GeneBehaviorAuthority.IsPrimary<CentralGeneLogic>(_controller,_gene)||EcologyMotion.Immobilized(_controller))return;
            if(dt<=0)return;
            _actionCooldown -= dt;
            float speed = _controller.GetAttribute("moveSpeed");
            var action=GeneCapabilities.Active(_controller,BiologySettings.Current.locomotionGene);
            if(action==null||_controller.GetLogic(action.Gene) is not LocomotionGeneLogic motor)return;
            var stats = ActiveSimulationBatch.Read<EntityStats>(_controller);
            if (speed <= 0 || stats == null || !stats.TryGet("health", out float hp) || hp <= 0) return;
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
                    bool graze=GeneCapabilities.Diet(_controller)=="herbivore";
                    _target=manual.GetAttackTarget();
                    // 玩家主动攻击不应被 AI 的食性筛选拒绝；没有行动能力仍无法攻击。
                    if(_target!=null)TryBite(stats,graze);
                }
                return;
            }
            // ★ 适配：查询/任务决策按模拟节奏缓存，持续移动与咬合冷却保持逐步推进。
            // ★ 新增：用户顺序任务具有实际目标和完成条件，不能被普通巡逻/随机觅食覆盖。
            var plan=ActiveSimulationBatch.Read<EntityActivityPlan>(_controller);
            if(plan!=null&&plan.Active)
            {
                _decision.Reset();plan.Destination(out var plannedPoint,out _target);motor.Move(plannedPoint,speed,dt);
                if(_target!=null)TryBite(stats,GeneCapabilities.Diet(_controller)=="herbivore");return;
            }
            bool decide=_decision.Due(_controller,dt);
            if(decide)_decision.directive=EcologyMotion.Directive(_controller,out _decision.point,out _decision.target,out _decision.multiplier);
            if(_decision.directive)
            {
                _target=_decision.target;
                if(_target!=null){var explicitOrder=ActiveSimulationBatch.Read<RtsOrderController>(_controller);var pop=ActiveSimulationBatch.Read<PopulationBehavior>(_controller);if(explicitOrder?.State.active==true&&explicitOrder.State.kind==RtsOrderKind.Attack||pop?.ManualRemaining>0)EntityNeighborhood.TryPosition(_target,out _decision.point);else if(!EntityPerceptionHub.TryLocate(_controller,_target,out _decision.point))
                {
                    if(EntityPerceptionHub.TryRecall(_controller,_target,out var known)&&LandReachability.CanReach(_controller,_target,known.position))
                    {motor.Move(known.position,speed*_decision.multiplier,dt);return;}
                    _decision.Reset();_target=null;return;
                }}
                motor.Move(_decision.point,speed*_decision.multiplier,dt);
                if(_target!=null)TryBite(stats,false);return;
            }
            // ★ 适配：由生态预算决定是否需补充繁殖储备，仓储充足后避免过度捕食。
            if (NutritionTransfer.CanHuntForFood(_controller)&&EcologyEnergyBudget.WantsFood(_controller,stats,_settings.hungerThreshold) && GeneCapabilities.Diet(_controller) is string diet)
            {
                bool graze = diet == "herbivore";
                if (diet == "hunt" || graze)
                {
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
        public virtual string GetTooltipInfo() => _target != null ? "正在追击：" + _target.DisplayName : "中枢选择目标和行为，行动基因执行移动，口部异养基因提供伤害与消化。";
    }
}
