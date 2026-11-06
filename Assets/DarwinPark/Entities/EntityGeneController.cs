using DarwinPark.Core;
using DarwinPark.Entities.Logic;
using DarwinPark.Entities.Stats;
using DarwinPark.Genetics;
using DarwinPark.Genetics.Mutation;
using System;
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
        public int LogicRevision {get;private set;}
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
        private readonly List<IGeneLogic> _simulationLogics=new List<IGeneLogic>();
        private readonly Dictionary<string,GeneEvaluation> _evaluations=new Dictionary<string,GeneEvaluation>(StringComparer.Ordinal);
        private readonly HashSet<GeneInstance> _genomeMembership=new HashSet<GeneInstance>();
        private readonly List<GeneInstance> _removedLogics=new List<GeneInstance>();
        private GeneEvaluation _nullIdEvaluation;
        private int _snapshotRevision=-1;
        public int LogicSnapshotBuilds {get;private set;}
        private static NamePoolJson _namePool;private static bool _namePoolLoaded,_nameAssetMissing;
        private bool _isRecalculating = false;
        private EntityLifeCycle _life;private Ecology.CombatIntent _combat;private Ecology.EntityStateController _state;
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
            // ★ 适配：联机生态只在管理员进程推进，副本不能发生独立变异或出生。
            if (dt <= 0f||WorldAuthority.IsReplica) return;
            // ★ 适配：物理/搬运改变位置后更新已存在的邻居快照，不在每个小步重建索引。
            Ecology.EntityNeighborhood.ObservePosition(this);
            // ★ 适配：卵孵化前不执行出生变异和普通基因行为，孵化后从幼年开始。
            var egg=GetComponent<Ecology.EggIncubation>();if(egg!=null&&egg.Incubating){egg.Advance(dt);return;}
            if (_isRecalculating) return;
            // ★ 修复（防Bug回归）：生命与反击组件在实体生命周期内复用，不能每个模拟子步重复GetComponent。
            if(_life==null){_life=GetComponent<EntityLifeCycle>();if(_life==null&&GetComponent<EntityStats>()!=null)_life=gameObject.AddComponent<EntityLifeCycle>();}
            if(_combat==null)_combat=GetComponent<Ecology.CombatIntent>()??gameObject.AddComponent<Ecology.CombatIntent>();
            // ★ 修复（防Bug回归）：按整数步数结算，避免0.5反复减0.1留下浮点残差、额外跑第六轮全部行为。
            int steps=Mathf.CeilToInt(dt/.1f);
            for(int index=0;index<steps;index++)
            {
                float step=index==steps-1?dt-index*.1f:.1f;
                // ★ 适配：寿命由单一组件推进，基因复制/失效/重新挂载不会重置或重复推进年龄。
                _life?.Advance(step);
                if(_life!=null && _life.HasDied)return;
                // ★ 适配：反击记忆随模拟步长推进，暂停或倍率切换不会消耗真实秒数。
                _combat.Advance(step);
                // ★ 适配：独立状态机先按配置选择意图，所有复制行动基因随后共用一个行为写入者。
                _state?.Advance(step);
                // ★ 修复（防Bug回归）：仍使用逻辑快照防止重算时修改集合，但复用缓冲区，避免每个实体每个子步分配新列表。
                // ★ 适配：优化重复计算，只在逻辑重算后重建快照；回调中的重算仅标记失效，不能修改正在遍历的列表。
                if(_snapshotRevision!=LogicRevision){_simulationLogics.Clear();_simulationLogics.AddRange(_activeLogics.Values);_snapshotRevision=LogicRevision;LogicSnapshotBuilds++;}
                foreach (var logic in _simulationLogics) logic.OnUpdate(step);
            }
        }

        public void Recalculate()
        {
            if (_isRecalculating) return;
            // ★ 适配：固定分工先还原出生遗传快照，编辑和阶段变异不能改写士兵/工匠基因。
            GetComponent<Ecology.AntColonyMember>()?.EnforceGenome();
            _isRecalculating = true;

            try
            {
                // ★ 适配：优化重复计算，重算复用成员表和移除缓冲，不为每次环境表达变动重新分配集合。
                _genomeMembership.Clear();_genomeMembership.UnionWith(Genome.Genes);

                // ★ 修复（防Bug回归）：环境不适应时所有基因受抑；突变作用于遗传值，表达受抑仍可变异。
                Evaluation = GeneValueResolver.Resolve(Genome, IsGestating, EnvironmentGeneSuppression,additionalSuppression:Ecology.AntColonyMember.ExpressionSuppression(this));

                // ★ 适配：优化重复计算，建立一次实例ID索引，供重算、行动及界面查询；重复/缺省ID保持原Find的首项语义。
                _evaluations.Clear();_nullIdEvaluation=null;
                foreach(var evaluation in Evaluation.Genes)
                {
                    var id=evaluation.Gene.InstanceId;if(id==null){if(_nullIdEvaluation==null)_nullIdEvaluation=evaluation;}
                    else if(!_evaluations.ContainsKey(id))_evaluations.Add(id,evaluation);
                }

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
                _removedLogics.Clear();
                foreach (var kvp in _activeLogics)
                {
                    var eval = GetEvaluation(kvp.Key);

                    // 如果基因不在了，或者未启用，或者被抑制到失效 (!eval.IsActive)
                    if (!_genomeMembership.Contains(kvp.Key) || !kvp.Key.Enabled || eval == null || !eval.IsActive ||
                        kvp.Value.GetType().Name != eval.Definition.LogicScriptId)
                    {
                        _removedLogics.Add(kvp.Key);
                    }
                }
                // ★ 修复（防Bug回归）：出错变异会原地更换 DefinitionId，旧逻辑必须释放并重建，不能继续执行旧行动/旧事件。
                foreach (var g in _removedLogics){if(_activeLogics[g] is IDisposable disposable)disposable.Dispose();_activeLogics.Remove(g);}

                _removedLogics.Clear();
                OnEvaluationChanged?.Invoke(Evaluation);

                // 3. 挂载新逻辑：只挂载有效且还未挂载的基因
                foreach (var gene in Genome.Genes)
                {
                    if (!gene.Enabled) continue;

                    var eval = GetEvaluation(gene);
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
            }
            finally
            {
                // ★ 修复（防Bug回归）：订阅方/逻辑初始化失败后仍释放重算锁，并让下一子步重新捕获可能已变更的逻辑。
                _isRecalculating=false;_snapshotRevision=-1;
            }
            // ★ 适配：标签新增/移除、出错换基因或受抑后同步更新实体状态授权。
            Ecology.EntityStateController.Rebind(this);
            _state=GetComponent<Ecology.EntityStateController>();LogicRevision++;
            // ★ 适配：必须在新逻辑挂载和状态授权完成后刷新生产者分类，避免使用旧表达。
            Ecology.EntityNeighborhood.Changed(this);
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

        // ★ 新增：统一按实例ID查询表达，不能让每个行动/生产子步各建闭包遍历整份基因列表。
        public GeneEvaluation GetEvaluation(GeneInstance gene)
        {if(gene==null)return null;return gene.InstanceId==null?_nullIdEvaluation:_evaluations.TryGetValue(gene.InstanceId,out var value)?value:null;}

        public IGeneLogic GetLogic(GeneInstance gene)
        {
            if (gene != null && _activeLogics.TryGetValue(gene, out var logic)) return logic;
            return null;
        }

        private void OnDestroy()
        {
            // ★ 适配：纯 C# 逻辑在移除/销毁时释放；仅停用控制器冻结测试时仍保留可查询的评估与逻辑。
            foreach(var logic in _activeLogics.Values)if(logic is IDisposable disposable)disposable.Dispose();
            _activeLogics.Clear();_simulationLogics.Clear();_evaluations.Clear();_genomeMembership.Clear();_removedLogics.Clear();_nullIdEvaluation=null;
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

        // ★ 适配：优化重复计算，内置词库每个播放会话只解析一次；随机抽样顺序和正常命名结果与原实现一致。
        public string GetRandomName()
        {
            if(!_namePoolLoaded)
            {
                _namePoolLoaded=true;var asset=Resources.Load<TextAsset>("Names/name_pool");_nameAssetMissing=asset==null;
                if(asset!=null)try{_namePool=JsonUtility.FromJson<NamePoolJson>(asset.text);}catch(Exception ex){Debug.LogWarning("[命名] 词库解析失败："+ex.Message);}
                else Debug.LogWarning("[命名] 缺少 Names/name_pool，使用回退名称");
            }
            if(_nameAssetMissing)return $"未知生物-{UnityEngine.Random.Range(100,999)}";
            if(_namePool?.familyNames==null||_namePool.familyNames.Count==0||_namePool.givenNames==null||_namePool.givenNames.Count==0)return "无名生物";
            return _namePool.familyNames[UnityEngine.Random.Range(0,_namePool.familyNames.Count)]+"·"+_namePool.givenNames[UnityEngine.Random.Range(0,_namePool.givenNames.Count)];
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetNamePool(){_namePool=null;_namePoolLoaded=_nameAssetMissing=false;}

        public float GetAttribute(string id)
        {
            if (Evaluation != null && Evaluation.Attributes.TryGetValue(id, out var v)) return Ecology.EcologyMotion.ResolveAttribute(this,id,v);
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
                if (GetComponent<Ecology.AntColonyMember>()?.Frozen!=true&&UnityEngine.Random.value < effectiveMutationChance)
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
