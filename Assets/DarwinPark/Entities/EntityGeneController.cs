using DarwinPark.Core;
using DarwinPark.Entities.Logic;
using DarwinPark.Entities.Stats;
using DarwinPark.Genetics;
using DarwinPark.Genetics.Mutation;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DarwinPark.Entities
{
    // ★ 核心修复：新增实体自带生命阶段枚举
    public enum AgeState { Infant, Adult, Elder }

    public class EntityGeneController : MonoBehaviour
    {
        public Genome Genome = new();
        public GenomeEvaluation Evaluation { get; private set; }
        public event Action<GenomeEvaluation> OnEvaluationChanged;

        [Header("成长设置")]
        public float growthInterval = 5f;
        public float mutationChance = 0.3f;
        [HideInInspector] public bool IsGestating = false;

        [Header("环境选择压力（由 EntityEnvironmentHazard 写入）")]
        [HideInInspector] public float EnvironmentGeneSuppression = 0f;
        [HideInInspector] public float EnvironmentMutationChance = 0f;
        [HideInInspector] public string EnvironmentGeneId = "gene_environment";
        [HideInInspector] public float EnvironmentAdaptationTarget = -1f;

        // ★ 核心修复：新增 CurrentAge 属性，供繁殖和生长逻辑调用
        public AgeState CurrentAge { get; set; } = AgeState.Infant;

        private Dictionary<GeneInstance, IGeneLogic> _activeLogics = new();
        private bool _isRecalculating = false;
        private int _mutatedStages;
        public int StageMutationChecks {get;private set;}
        // ★ 适配：恢复已抽过的阶段，读档不能重抽出生/成年变异。
        public int MutatedStageMask=>_mutatedStages;
        public void RestoreStageState(AgeState age,int mask,int checks){CurrentAge=age;_mutatedStages=mask;StageMutationChecks=checks;}

        private void Awake()
        {
            LoadGenesFromJson("entity_default");
            Recalculate();
        }

        private void Update()
        {
            // ★ 修复（防Bug回归）：基因逻辑、成长及突变共用模拟时间，不能只让日历加速。
            float dt = ServiceLocator.TryGet<SimulationClock>(out var clock) ? clock.GameDeltaTime : Time.deltaTime;
            AdvanceSimulation(dt);
        }

        public void AdvanceSimulation(float dt)
        {
            if (dt <= 0f) return;
            if (_isRecalculating) return;
            float remaining = dt;
            while (remaining > 0f)
            {
                float step = Mathf.Min(remaining, .1f);
                remaining -= step;
                // ★ 适配：寿命由单一组件推进，基因复制/失效/重新挂载不会重置或重复推进年龄。
                var life=GetComponent<EntityLifeCycle>();
                if(life==null && GetComponent<EntityStats>()!=null)life=gameObject.AddComponent<EntityLifeCycle>();
                life?.Advance(step);
                if(life!=null && life.HasDied)return;
                var logicsCopy = new List<IGeneLogic>(_activeLogics.Values);
                foreach (var logic in logicsCopy) logic.OnUpdate(step);
            }
        }

        public void Recalculate()
        {
            if (_isRecalculating) return;
            _isRecalculating = true;

            var currentGenes = new HashSet<GeneInstance>(Genome.Genes);

            // ★ 修复（防Bug回归）：环境不适应时所有基因受抑；突变作用于遗传值，表达受抑仍可变异。
            Evaluation = GeneValueResolver.Resolve(Genome, IsGestating, EnvironmentGeneSuppression);

            var stats = GetComponent<EntityStats>();
            float size = 1f;
            if (stats != null && stats.TryGet("size", out float s)) size = s;

            Evaluation.Attributes = EntityAttributeProcessor.Process(
                Evaluation.Attributes,
                Evaluation.Genes,
                CurrentAge,
                size,
                IsGestating
            );

            // ★ 2. 清理旧逻辑：如果基因被移除、被禁用，或者因为被抑制而失效，就删掉它的逻辑
            var toRemove = new List<GeneInstance>();
            foreach (var kvp in _activeLogics)
            {
                var eval = Evaluation.Genes.Find(g => g.Gene.InstanceId == kvp.Key.InstanceId);

                // 如果基因不在了，或者未启用，或者被抑制到失效 (!eval.IsActive)
                if (!currentGenes.Contains(kvp.Key) || !kvp.Key.Enabled || eval == null || !eval.IsActive)
                {
                    toRemove.Add(kvp.Key);
                }
            }
            foreach (var g in toRemove) _activeLogics.Remove(g);

            OnEvaluationChanged?.Invoke(Evaluation);

            // 3. 挂载新逻辑：只挂载有效且还未挂载的基因
            foreach (var gene in Genome.Genes)
            {
                if (!gene.Enabled) continue;

                var eval = Evaluation.Genes.Find(g => g.Gene.InstanceId == gene.InstanceId);
                if (eval == null || !eval.IsActive) continue; // 被抑制失效，不挂载

                if (_activeLogics.ContainsKey(gene)) continue;

                var def = GeneDatabase.Get(gene.DefinitionId);
                if (def == null || string.IsNullOrEmpty(def.LogicScriptId)) continue;

                var logic = GeneLogicRegistry.CreateLogic(def.LogicScriptId);
                if (logic != null)
                {
                    logic.Initialize(this, gene);
                    _activeLogics[gene] = logic;
                }
            }

            _isRecalculating = false;
        }

        /// <summary>
        /// ★ 新增：环境压力写入。压力变化较大时才重算，避免每帧重建评估。
        /// </summary>
        public void ApplyEnvironmentPressure(float suppression, float mutationChance, string adaptationGeneId, float targetValue)
        {
            EnvironmentMutationChance = Mathf.Clamp01(mutationChance);
            EnvironmentGeneId = string.IsNullOrEmpty(adaptationGeneId) ? "gene_environment" : adaptationGeneId;
            EnvironmentAdaptationTarget = targetValue;

            suppression = Mathf.Max(0f, suppression);

            // ★ 修复（防Bug回归）：旧版只比较 `_lastAppliedSuppression` 且阈值 0.75，
            // 当压力从 0.4 回到 0 时差值不足 0.75，抑制值会永久残留。
            // 现在始终用当前生效值做比较，并保证“压力归零”一定能触发恢复。
            float delta = Mathf.Abs(suppression - EnvironmentGeneSuppression);
            bool mustReset = suppression <= 0.01f && EnvironmentGeneSuppression > 0.01f;
            if (delta > 0.3f || mustReset)
            {
                EnvironmentGeneSuppression = suppression;
                Recalculate();
            }
        }

        public IGeneLogic GetLogic(GeneInstance gene)
        {
            if (gene != null && _activeLogics.TryGetValue(gene, out var logic)) return logic;
            return null;
        }

        public void LoadGenesFromJson(string jsonId)
        {
            var asset = Resources.Load<TextAsset>($"Entities/{jsonId}");
            if (asset == null)
            {
                Genome = GeneMutator.CreateBaseGenome();
                return;
            }

            var data = JsonUtility.FromJson<EntityGeneData>(asset.text);
            Genome = new Genome();
            foreach (var g in data.genes)
            {
                Genome.Add(GeneInstance.Create(g.definitionId, g.value, g.tier));
            }
        }

        public string GetRandomName()
        {
            var nameAsset = Resources.Load<TextAsset>("Names/name_pool");
            if (nameAsset == null) return $"未知生物-{UnityEngine.Random.Range(100, 999)}";
            var pool = JsonUtility.FromJson<NamePoolJson>(nameAsset.text);
            if (pool == null || pool.familyNames == null || pool.familyNames.Count == 0) return "无名生物";
            string f = pool.familyNames[UnityEngine.Random.Range(0, pool.familyNames.Count)];
            string g = pool.givenNames[UnityEngine.Random.Range(0, pool.givenNames.Count)];
            return $"{f}·{g}";
        }

        public float GetAttribute(string id)
        {
            if (Evaluation != null && Evaluation.Attributes.TryGetValue(id, out var v)) return v;
            return 0f;
        }

        public void TryStageMutation(AgeState stage)
        {
                // ★ 修复（防Bug回归）：只在出生/成年/老年首次进入时抽一次，普通帧和定时刷新不能变异。
                int bit=1<<(int)stage;if((_mutatedStages&bit)!=0)return;
                _mutatedStages|=bit;StageMutationChecks++;
                // 基因变异与刷新。
                // ★ 修复：环境压力不仅提高“定向选择”概率，也提高本次变异的总体发生率
                //（最高 +25%），否则玩家很难观察到适应基因被优先突变。
                float effectiveMutationChance = Mathf.Clamp01(
                    mutationChance + EnvironmentMutationChance * 0.25f);
                if (UnityEngine.Random.value < effectiveMutationChance)
                {
                    var settings = GeneMutationSettings.CreateDefault();
                    settings.AdaptationGeneId = EnvironmentGeneId;
                    settings.AdaptationMutationChance = EnvironmentMutationChance;
                    settings.AdaptationTargetValue = EnvironmentAdaptationTarget;
                    GeneMutator.Mutate(Genome, settings);
                }
                Recalculate();
        }
    }
}
