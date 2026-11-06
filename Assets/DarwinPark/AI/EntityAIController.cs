using System.Collections.Generic;
using DarwinPark.Core;
using DarwinPark.Entities;
using DarwinPark.Genetics;
using UnityEngine;

namespace DarwinPark.AI
{
    [RequireComponent(typeof(EntityGeneController))]
    public class EntityAIController : MonoBehaviour
    {
        private EntityGeneController _genes;
        private EntityAIContext _context;
        private readonly Dictionary<string, IGeneAIModule> _activeModules = new();
        private readonly HashSet<string> _desiredModules = new();
        private readonly List<string> _removedModules = new();
        private readonly List<KeyValuePair<string, IGeneAIModule>> _tickModules = new();
        private bool _sleeping;

        private void Awake()
        {
            _genes = GetComponent<EntityGeneController>();
            _context = new EntityAIContext { Entity = GetComponent<DarwinEntity>(), Genes = _genes };
            _genes.OnEvaluationChanged += OnGenesChanged;
        }
        private void Start() { OnGenesChanged(_genes.Evaluation); }

        private void OnGenesChanged(GenomeEvaluation evaluation)
        {
            if (evaluation == null) return;
            // ★ 适配：复用决策/移除缓冲，工厂仅保留注册表一个入口。
            GeneAIDecisionService.Collect(evaluation, _desiredModules);
            _removedModules.Clear();
            foreach (var id in _activeModules.Keys) if (!_desiredModules.Contains(id)) _removedModules.Add(id);
            foreach (var id in _removedModules) { var module = _activeModules[id]; _activeModules.Remove(id); module.OnDisable(); }
            foreach (var id in _desiredModules)
            {
                if (_activeModules.ContainsKey(id)) continue;
                var module = GeneAIModuleRegistry.Create(id);
                if (module == null) continue;
                module.Initialize(_context);
                _activeModules.Add(id, module);
            }
            // ★ 适配：表达变化后准备下一帧快照；不能改写正在Tick的列表。
            _snapshotDirty = true;
            bool ticks = false;
            foreach (var module in _activeModules.Values) if (!(module is IPassiveGeneAIModule)) { ticks = true; break; }
            // ★ 适配：空模块休眠但继续订阅表达变化；只恢复自身休眠，不覆盖调用方主动禁用。
            if (!ticks && enabled) { _sleeping = true; enabled = false; }
            else if (ticks && _sleeping) { _sleeping = false; enabled = true; }
        }
        private bool _snapshotDirty = true;
        private void Update()
        {
            // ★ 修复（防Bug回归）：管理员推进模拟，副本不能独立运行扩展AI；暂停时仍保持旧有冻结语义。
            if (WorldAuthority.IsReplica) return;
            float dt = ServiceLocator.TryGet<SimulationClock>(out var clock) ? clock.GameDeltaTime : Time.deltaTime;
            if (dt <= 0) return;
            if (_snapshotDirty)
            {
                _tickModules.Clear();
                foreach (var pair in _activeModules) if (!(pair.Value is IPassiveGeneAIModule)) _tickModules.Add(pair);
                _snapshotDirty = false;
            }
            // ★ 修复（防Bug回归）：Tick可触发表达重算，使用快照并检查成员身份，防止集合修改异常或执行已卸载模块。
            foreach (var pair in _tickModules)
                if (_activeModules.TryGetValue(pair.Key, out var current) && ReferenceEquals(current, pair.Value)) current.Tick(dt);
        }
        private void OnDestroy()
        {
            // ★ 修复（防Bug回归）：销毁时必须成对退订并释放扩展模块，不留下事件或外部资源。
            if (_genes != null) _genes.OnEvaluationChanged -= OnGenesChanged;
            foreach (var module in _activeModules.Values) module.OnDisable();
            _activeModules.Clear(); _tickModules.Clear();
        }
    }
}
