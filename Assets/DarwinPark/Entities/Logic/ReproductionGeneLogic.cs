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

        private const float BaseGestationTime = 300f;
        private const float BaseNutrientCost = 20f; // ★ 基础消耗 20 储存营养
        private const float MateSearchRadius = 20f;

        private Genome _partnerGenome = null;

        // ★ 补上这一行：用于控制调试日志输出频率，防止刷屏
        private float _debugTimer = 0f;

        public void Initialize(EntityGeneController controller, GeneInstance gene)
        {
            _controller = controller;
            _gene = gene;
        }

        public void OnUpdate(float dt)
        {
            var stats = _controller.GetComponent<EntityStats>();
            if (stats == null) return;

            if (_cooldownTimer > 0) { _cooldownTimer -= dt; return; }

            if (_controller.IsGestating)
            {
                _gestationTimer -= dt;
                stats.Modify("nutrient", -3f * dt); // 怀孕期间依然消耗饱食度

                if (_gestationTimer <= 0f)
                {
                    SpawnOffspring();
                    _controller.IsGestating = false;
                    _controller.Recalculate();
                    _cooldownTimer = 150f;
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
            float requiredNutrient = BaseNutrientCost * size;

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

            float growthValue = _controller.Genome.Genes.Find(g => g.DefinitionId == "gene_growth").Value;
            float speedFactor = Mathf.Clamp(growthValue, 10f, 90f) / 50f;

            // ★ 体型越大，时间越长
            _gestationTimer = (BaseGestationTime / speedFactor) * size;

            string mode = partnerGenome == null ? "无性繁殖" : "有性繁殖";
            Debug.Log($"[繁殖] {_controller.gameObject.name} 开始{mode}！预计需要 {(_gestationTimer / 10f):F0} 天。");
            _controller.Recalculate();
        }

        private DarwinEntity FindMate()
        {
            Collider[] hits = Physics.OverlapSphere(_controller.transform.position, MateSearchRadius);
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

        private void SpawnOffspring()
        {
            var childGenome = new Genome();

            if (_partnerGenome == null)
            {
                foreach (var g in _controller.Genome.Genes)
                    childGenome.Add(GeneInstance.Create(g.DefinitionId, g.Value, g.Tier));
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

            Vector3 spawnPos = _controller.transform.position + Random.onUnitSphere * 2f;
            if (DarwinEntity.Spawner != null)
            {
                DarwinEntity.Spawner.Invoke(childGenome, spawnPos, "");
            }
            Debug.Log($"[繁殖] {_controller.gameObject.name} 孕育完成，产下了后代！");

            var growthGene = _controller.Genome.Genes.Find(g => g.DefinitionId == "gene_growth");
            if (growthGene != null)
            {
                growthGene.Value = Mathf.Min(100f, growthGene.Value + 15f);
            }
        }

        public string GetTooltipInfo()
        {
            if (_cooldownTimer > 0) return $"产后休养中: {(_cooldownTimer / 10f):F0} 天";
            if (_controller.IsGestating)
            {
                string mode = _partnerGenome == null ? "无性孕育" : "有性孕育";
                return $"<color=#FF6B6B>{mode}中: {(_gestationTimer / 10f):F0} 天</color>";
            }
            return "状态: 待机（需成年且储存营养充足）";
        }
    }
}