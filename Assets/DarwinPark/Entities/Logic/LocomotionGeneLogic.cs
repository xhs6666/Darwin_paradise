using DarwinPark.Core;
using DarwinPark.Genetics;
using DarwinPark.Entities.Stats;
using DarwinPark.Planet;
using UnityEngine;
using DarwinPark.Entities.Ecology;

namespace DarwinPark.Entities.Logic
{
    public class LocomotionGeneLogic : IGeneLogic
    {
        private EntityGeneController _controller;
        private GeneInstance _gene;
        private Rigidbody _rb;
        private PlanetTerrainMeshBuilder _planet;
        private LocomotionSettings _settings;
        private float _actionCooldown, _wanderTimer;
        private Vector3 _wanderDir;
        private DarwinEntity _target;

        public virtual void Initialize(EntityGeneController controller, GeneInstance gene)
        {
            _controller = controller; _gene = gene; _rb = controller.GetComponent<Rigidbody>();
            _planet = Object.FindFirstObjectByType<PlanetTerrainMeshBuilder>();
            _settings = LocomotionSettings.Load(); _wanderDir = Random.onUnitSphere;
        }
        public void OnUpdate(float dt)
        {
            // ★ 适配：社会谱系和复制行动基因只保留一个行为写入者；寄生/孵化自行控制移动。
            if(!GeneBehaviorAuthority.IsPrimary<LocomotionGeneLogic>(_controller,_gene)||EcologyMotion.Immobilized(_controller))return;
            // ★ 修复（防Bug回归）：模板 Awake 可能早于工厂补挂刚体，首次缓存为空时必须能自愈。
            if (_rb == null) _rb = _controller.GetComponent<Rigidbody>();
            if (_planet == null) _planet = Object.FindFirstObjectByType<PlanetTerrainMeshBuilder>();
            if (_rb == null || _planet == null || dt <= 0) return;
            _actionCooldown -= dt;
            float speed = _controller.GetAttribute("moveSpeed");
            var stats = _controller.GetComponent<EntityStats>();
            if (speed <= 0 || stats == null || !stats.TryGet("health", out float hp) || hp <= 0) return;
            // ★ 修复（防Bug回归）：行动基因只有陆地推进，不能在水中继续追击/漫游充当游泳。
            if(_controller.GetComponent<DarwinPark.Entities.Vitals.EntityEnvironmentHazard>()?.IsSubmerged == true)
            { _target=null; return; }
            // ★ 适配：使用重算时建立的实例索引，行动子步不重复扫描和分配查询闭包。
            var evaluated = _controller.GetEvaluation(_gene);
            // ★ 修复（防Bug回归）：行为状态共用面板评估结果，失效基因不执行行动。
            var state = evaluated?.State;
            // ★ 适配：手动控制期间 AI 不再抢写速度，只有玩家按攻击键才执行近距离进食。
            var manual = _controller.GetComponent<EntityManualControl>();
            if (manual != null && manual.IsControlled)
            {
                // ★ 修复（防Bug回归）：瞬时左键请求保留到基因逻辑消费，不能只依赖两个 Update 之间是否仍按住。
                // ★ 修复（防Bug回归）：冷却期间不消费短点击，留到可攻击的模拟步，避免点击被无声丢弃。
                if (_actionCooldown > 0) return;
                bool requested=manual.ConsumeAttack() || manual.AttackHeld;
                if (manual.CanMove && requested && state != null)
                {
                    // ★ 修复（防Bug回归）：手动每次攻击按准星重新选取，不沿用 AI 的最近猎物缓存。
                    bool graze=state.StateId=="herbivore";
                    _target=manual.GetAttackTarget();
                    // 玩家主动攻击不应被 AI 的食性筛选拒绝；没有行动能力仍无法攻击。
                    if(_target!=null)TryBite(stats,graze);
                }
                return;
            }
            if(EcologyMotion.Directive(_controller,out var destination,out var commandTarget,out float multiplier))
            {
                _target=commandTarget;Chase(destination,speed*multiplier,dt);
                if(_target!=null)TryBite(stats,false);return;
            }
            // ★ 适配：由生态预算决定是否需补充繁殖储备，仓储充足后避免过度捕食。
            if (NutritionTransfer.CanHuntForFood(_controller)&&EcologyEnergyBudget.WantsFood(_controller,stats,_settings.hungerThreshold) && state != null)
            {
                bool graze = state.StateId == "herbivore";
                if (state.StateId == "hunt" || graze)
                {
                    FindTarget(graze);
                    if (_target != null)
                    {
                        Chase(_target.transform.position, speed, dt);
                        TryBite(stats, graze);
                        return;
                    }
                    // 没有猎物时继续搜索漫游，不能永久 return 原地饿死。
                }
            }
            _target = null;
            _wanderTimer -= dt;
            if (_wanderTimer <= 0) { _wanderTimer = _settings.wanderInterval; _wanderDir = Random.onUnitSphere; }
            Chase(_controller.transform.position + _wanderDir * _settings.wanderDistance, speed * _settings.wanderSpeedRatio, dt);
        }
        private void TryBite(EntityStats stats, bool graze)
        {
            if (_target == null || _actionCooldown > 0) return;
            // ★ 修复（防Bug回归）：手动攻击、群体命令和缓存猎物同样不得捕食更大体型。
            if(!PredationSizePolicy.Allows(_controller,_target)){_target=null;return;}
            var otherStats = _target.GetComponent<EntityStats>();
            var a = _controller.GetComponent<Collider>(); var b = _target.GetComponent<Collider>();
            float gap = a != null && b != null ? Vector3.Distance(a.ClosestPoint(b.bounds.center),b.ClosestPoint(a.bounds.center)) : Vector3.Distance(_controller.transform.position,_target.transform.position);
            // ★ 修复（防Bug回归）：手动近战为准星判定留少量距离容差，AI的追击距离保持原有配置。
            float reach=_settings.attackReach+(_controller.GetComponent<EntityManualControl>()?.IsControlled==true?GameplayTuning.Current.manualAttackReachBonus:0);
            if (gap > reach || otherStats == null || !otherStats.TryGet("health",out float health) || health <= 0) return;
            float damage = graze ? _settings.grazeDamage : _controller.GetAttribute("attack");
            if (damage <= 0) return;
            _actionCooldown = _settings.attackInterval;
            // ★ 适配：仅致死结算产生可吸收营养，非致死伤害不会凭空增加食物。
            EntityCombat.Ensure(_target.gameObject).Receive(_controller.GetComponent<DarwinEntity>(),damage);
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
            // ★ 修复（防Bug回归）：捕食可选择所有存活实体，不再因没有行动基因就完全排除默认生物。
            // 以感知属性选最近目标，并保持目标至离开感知范围，避免每帧来回切换。
            float range = _controller.GetAttribute("senseRange");
            bool Valid(DarwinEntity e) => e != null && e.gameObject != _controller.gameObject && e.isActiveAndEnabled &&
                (e.transform.position-_controller.transform.position).sqrMagnitude<=range*range&&
                e.GetComponent<EntityStats>() is EntityStats s && s.TryGet("health", out float hp) && hp > 0 &&
                (!graze || e.Genes.GetAttribute("moveSpeed") <= 0) &&
                EcologyMotion.CanHunt(_controller,e)&&EcologyMotion.Detectable(e,_controller.transform.position,range,_controller.GetComponent<DarwinEntity>())&&
                (e.transform.position-_controller.transform.position).sqrMagnitude <= range*range;
            if (Valid(_target)) return;
            _target = null; float best = float.MaxValue;
            foreach (var entity in EntityRegistry.All)
            {
                if (!Valid(entity)) continue;
                float distance = (entity.transform.position-_controller.transform.position).sqrMagnitude;
                if (distance < best) { best = distance; _target = entity; }
            }
        }
        private void Chase(Vector3 position, float speed, float dt)
        {
            Vector3 radial = (_controller.transform.position-_planet.transform.position).normalized;
            Vector3 toward = Vector3.ProjectOnPlane(position-_controller.transform.position, radial);
            // ★ 适配：行动使用时钟实际交付倍率，过载补算限速不造成物理时间分离。
            float scale = ServiceLocator.TryGet<SimulationClock>(out var clock) ? clock.MotionTimeScale : 1;
            // ★ 修复（防Bug回归）：物理仍按真实步长运行，切向速度按倍率转换；限制到目标剩余距离避免高速越过。
            float physicalSpeed = Mathf.Min(EntityMovementLimits.Speed(speed) * scale, toward.magnitude / Mathf.Max(Time.deltaTime, .001f));
            Vector3 desired = toward.normalized * physicalSpeed;
            // ★ 适配：同群成员保持身体间距，重叠列也向侧面走开，不向王后头顶施加吸附或垂直速度。
            desired=ColonyFormation.Avoid(_controller,desired,EntityMovementLimits.Speed(speed)*scale);
            Vector3 velocity = _rb.linearVelocity;
            float inward = Mathf.Min(0,Vector3.Dot(velocity,radial));
            Vector3 tangent = Vector3.Lerp(Vector3.ProjectOnPlane(velocity,radial),desired,1-Mathf.Exp(-_settings.steering*dt));
            _rb.linearVelocity = Vector3.ClampMagnitude(tangent,_settings.maximumMoveSpeed*scale) + radial * inward;
        }
        public virtual string GetTooltipInfo() => _target != null ? "正在追击：" + _target.DisplayName : "提供移动与攻击；消化食性寻找活体，幼年寄生寻找宿主，食腐寻找尸体。食草倾向仅选择静止活体。";
    }
}
