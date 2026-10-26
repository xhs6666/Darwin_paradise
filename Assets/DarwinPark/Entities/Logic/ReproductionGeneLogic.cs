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
            var stats = _controller.GetComponent<EntityStats>();
            if (stats == null) return;

            if (_cooldownTimer > 0) { _cooldownTimer -= dt; return; }

            if (_controller.IsGestating)
            {
                _gestationTimer -= dt;
                stats.Modify("nutrient", -_settings.gestationNutrientDrain * dt);

                if (_gestationTimer <= 0f)
                {
                    if(!SpawnOffspring())return;
                    _controller.IsGestating = false;
                    _controller.Recalculate();
                    _cooldownTimer = _settings.cooldownDays*_settings.secondsPerDay;
                    stats.Modify("nutrient", -50f);
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
            if (stats.TryGet("size", out var s)) size = Mathf.Max(0.5f, s);
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

            var repDef = GeneDatabase.Get("gene_reproduction");
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
            var growth=_controller.Genome.Genes.Find(g=>g.DefinitionId=="gene_growth");
            float speedFactor=Mathf.Clamp(growth?.Value??50,10,90)/50;
            float days=Mathf.Clamp(_settings.baseGestationDays/speedFactor*size,_settings.minimumGestationDays,_settings.maximumGestationDays);
            return days*_settings.secondsPerDay;
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

                var otherRepGene = otherGeneCtrl.Genome.Genes.Find(g => g.DefinitionId == "gene_reproduction");
                if (otherRepGene == null) continue;

                var repDef = GeneDatabase.Get("gene_reproduction");
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
                DarwinEntity.Spawner.Invoke(childGenome, spawnPos, "");
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
