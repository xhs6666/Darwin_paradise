using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using DarwinPark.Genetics;
using DarwinPark.Entities.Stats;

namespace DarwinPark.Entities.Logic
{
    public class ReproductionGeneLogic : IGeneLogic
    {
        private EntityGeneController _controller;
        private GeneInstance _gene;

        private float _gestationTimer = 0f;
        private float _cooldownTimer = 0f;

        private LifeCycleSettings _settings;

        private Genome _partnerGenome = null;

        // ★ 补上这一行：用于控制调试日志输出频率，防止刷屏
        private float _debugTimer = 0f;

        public void Initialize(EntityGeneController controller, GeneInstance gene)
        {
            _controller = controller;
            _gene = gene;
            _settings=LifeCycleSettings.Load();
        }

        // ★ 适配：孕育计时、产后冷却与配偶基因组一起保存，不能读档后立即生产或丢失后代遗传。
        public ReproductionSaveState CaptureState()=>new ReproductionSaveState{gene=_gene.InstanceId,gestation=_gestationTimer,cooldown=_cooldownTimer,partner=_partnerGenome};
        public void RestoreState(ReproductionSaveState state){_gestationTimer=state.gestation;_cooldownTimer=state.cooldown;_partnerGenome=state.partner;}
        public void OnUpdate(float dt)
        {
            if(dt<=0)return;
            // ★ 适配：蚁群王后走独立孕育流程，其余分工不得运行普通繁殖逻辑。
            if(Ecology.AntColonyReproduction.BlocksOrdinary(_controller))return;
            // ★ 适配：基础、性别特化和卵生复制可共存，但只由最高阶有效繁殖基因推进一次孕育。
            if(!GeneBehaviorAuthority.IsPrimary<ReproductionGeneLogic>(_controller,_gene))return;
            var stats = _controller.GetComponent<EntityStats>();
            if (stats == null) return;

            if (_cooldownTimer > 0) { _cooldownTimer -= dt; return; }

            if (_controller.IsGestating)
            {
                _gestationTimer -= dt;
                // ★ 适配：孕育额外营养开销与生态收支共用配置，避免长孕育必然饿死。
                stats.Modify("nutrient", -Ecology.EcologyBalanceSettings.Current.gestationFoodPerSecond * dt);

                if (_gestationTimer <= 0f)
                {
                    if(!SpawnOffspring())return;
                    _controller.IsGestating = false;
                    _controller.Recalculate();
                    _cooldownTimer = Ecology.EcologyReproductionTiming.CooldownSeconds(_controller);
                    stats.Modify("nutrient", -Ecology.EcologyBalanceSettings.Current.birthFoodCost);
                    _partnerGenome = null;
                }
                return;
            }

            // 检查成年
            // ★ 核心修改：直接判断实体自带状态
            if (_controller.CurrentAge != AgeState.Adult)
            {
                _debugTimer -= dt;
                if (_debugTimer <= 0)
                {
                    _debugTimer = 5f;
                    Debug.Log($"[繁殖调试] {_controller.gameObject.name} 无法繁殖：当前是 {_controller.CurrentAge}，需要成年。");
                }
                return;
            }

            // ★ 核心修改：根据体型计算所需营养和时间
            float size = 1f;
            // ★ 适配：实际扣款与提前储粮采用同一体型下限，微型实体不会准备不足的繁殖预算。
            if (stats.TryGet("size", out var s)) size = Mathf.Max(Ecology.EcologyBalanceSettings.Current.minimumReproductionSize, s);
            float requiredNutrient = _settings.nutrientCost * size;

            // ★ 检查条件：必须是储存营养 (storedNutrient) 足够
            if (!stats.TryGet("storedNutrient", out var stored) || stored < requiredNutrient)
            {
                // ★ 新增：缺失条件日志
                _debugTimer -= dt;
                if (_debugTimer <= 0)
                {
                    _debugTimer = 5f;
                    Debug.Log($"[繁殖调试] {_controller.gameObject.name} 无法繁殖：储存营养不足 ({stored:F1}/{requiredNutrient:F1})。");
                }
                return;
            }

            var repDef = GeneDatabase.Get(_gene.DefinitionId);
            var repState = repDef?.ResolveState(_gene.Value, _gene.MissingStateIds);

            if (repState != null && repState.StateId == "asexual")
            {
                // ★ 修复（防Bug回归）：无性孕育同样消耗储存营养，不能零成本无限生产。
                stats.Modify("storedNutrient",-requiredNutrient);
                StartGestation(stats, null, size);
            }
            else if (repState != null && repState.StateId == "sexual")
            {
                var mate = FindMate();

                if (mate == null)
                {
                    // ★ 新增：缺失条件日志
                    _debugTimer -= dt;
                    if (_debugTimer <= 0)
                    {
                        _debugTimer = 5f;
                        Debug.Log($"[繁殖调试] {_controller.gameObject.name} 无法繁殖：当前是有性繁殖，但附近 20 米没有找到合适的成年伴侣。");
                    }
                    return;
                }

                if (mate != null)
                {
                    var mateStats = mate.GetComponent<EntityStats>();
                    if (mateStats != null && mateStats.TryGet("storedNutrient", out var mateStored) && mateStored >= requiredNutrient)
                    {
                        // 双方扣除储存营养
                        stats.Modify("storedNutrient", -requiredNutrient);
                        mateStats.Modify("storedNutrient", -requiredNutrient);

                        var mateGeneCtrl = mate.GetComponent<EntityGeneController>();
                        StartGestation(stats, mateGeneCtrl.Genome, size);
                    }
                }
            }
        }

        private void StartGestation(EntityStats stats, Genome partnerGenome, float size)
        {
            _partnerGenome = partnerGenome;
            _controller.IsGestating = true;

            _gestationTimer=CalculateGestationTime(size);

            // ★ 修复（防Bug回归）：提示使用与孕育计时相同的配置日长，避免调整日长后显示失真。
            string mode = partnerGenome == null ? "无性繁殖" : "有性繁殖";
            Debug.Log($"[繁殖] {_controller.gameObject.name} 开始{mode}！预计需要 {(_gestationTimer / _settings.secondsPerDay):F0} 天。");
            _controller.Recalculate();
        }

        // ★ 修复（防Bug回归）：体型/生长影响仍保留，但总孕育时长必须处于 7～30 个游戏日。
        public float CalculateGestationTime(float size)
        {
            // ★ 修复（防Bug回归）：原计时读取遗传原值，基因抑制对繁殖周期没有效果。
            return Ecology.EcologyReproductionTiming.GestationSeconds(_controller,size);
        }

        private DarwinEntity FindMate()
        {
            Collider[] hits = Physics.OverlapSphere(_controller.transform.position, _settings.mateSearchRadius);
            foreach (var hit in hits)
            {
                var other = hit.GetComponentInParent<DarwinEntity>();
                if (other == null || other == _controller.GetComponent<DarwinEntity>()) continue;

                var otherGeneCtrl = other.GetComponent<EntityGeneController>();
                if (otherGeneCtrl == null) continue;

                var otherRepGene = otherGeneCtrl.Genome.Genes.FirstOrDefault(g => otherGeneCtrl.GetLogic(g) is ReproductionGeneLogic&&GeneBehaviorAuthority.IsPrimary<ReproductionGeneLogic>(otherGeneCtrl,g));
                if (otherRepGene == null) continue;

                var repDef = GeneDatabase.Get(otherRepGene.DefinitionId);
                var otherState = repDef?.ResolveState(otherRepGene.Value, otherRepGene.MissingStateIds);
                if (otherState == null || otherState.StateId != "sexual") continue;

                // ★ 修复（防Bug回归）：生长基因的状态 id 是 fast_growth/slow_growth，
                // 旧代码却查找 "adult"，导致有性繁殖永远找不到配偶。成年判定应使用 CurrentAge。
                if (otherGeneCtrl.CurrentAge != AgeState.Adult) continue;
                if (otherGeneCtrl.IsGestating) continue;

                return other;
            }
            return null;
        }

        private bool SpawnOffspring()
        {
            bool hasAction=_controller.Evaluation.Genes.Exists(g=>g.IsActive && _controller.GetLogic(g.Gene) is LocomotionGeneLogic);
            if(DarwinEntity.Spawner==null || !OffspringPlacement.TryGetPosition(_controller.transform.position,_partnerGenome==null && !hasAction,out var spawnPos))return false;
            var childGenome = new Genome();

            if (_partnerGenome == null)
            {
                foreach (var g in _controller.Genome.Genes)
                {
                    var copy=GeneInstance.Create(g.DefinitionId,g.Value,g.Tier);
                    copy.Enabled=g.Enabled;copy.CopyStrength=g.CopyStrength;copy.MissingStateIds=new List<string>(g.MissingStateIds);copy.OriginInstanceId=g.InstanceId;
                    childGenome.Add(copy);
                }
            }
            else
            {
                foreach (var myGene in _controller.Genome.Genes)
                {
                    var partnerGene = _partnerGenome.Genes.Find(g => g.DefinitionId == myGene.DefinitionId);
                    float newValue = myGene.Value;
                    if (partnerGene != null) newValue = (myGene.Value + partnerGene.Value) * 0.5f;
                    childGenome.Add(GeneInstance.Create(myGene.DefinitionId, newValue, myGene.Tier));
                }
            }

            if (DarwinEntity.Spawner != null)
            {
                // ★ 适配：卵生后代的孵化状态独立于母体孕育，不会把卵直接当作已出生幼体。
                Ecology.EcologyReproduction.Spawn(_controller,childGenome,spawnPos);
            }
            Debug.Log($"[繁殖] {_controller.gameObject.name} 孕育完成，产下了后代！");

            // ★ 修复（防Bug回归）：生产不属于生命阶段切换，不再偷偷改写亲本生长基因。
            return true;
        }

        public string GetTooltipInfo()
        {
            if (_cooldownTimer > 0) return $"产后休养中: {(_cooldownTimer / _settings.secondsPerDay):F0} 天";
            if (_controller.IsGestating)
            {
                string mode = _partnerGenome == null ? "无性孕育" : "有性孕育";
                return $"<color=#FF6B6B>{mode}中: {(_gestationTimer / _settings.secondsPerDay):F0} 天</color>";
            }
            return "状态: 待机（需成年且储存营养充足）";
        }
    }
}
