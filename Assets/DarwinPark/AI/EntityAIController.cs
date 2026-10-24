using UnityEngine;
using DarwinPark.Core;
using System.Collections.Generic;
using DarwinPark.Entities;
using DarwinPark.Genetics;
using DarwinPark.AI.Modules; // ★ 修复：引入 Modules 命名空间

namespace DarwinPark.AI
{
    [RequireComponent(typeof(EntityGeneController))]
    public class EntityAIController : MonoBehaviour
    {
        private EntityGeneController _genes;
        private EntityAIContext _context; // ★ 新增：上下文数据包
        private Dictionary<string, IGeneAIModule> _activeModules = new();

        private void Awake()
        {
            _genes = GetComponent<EntityGeneController>();
            var entity = GetComponent<DarwinEntity>();

            // ★ 初始化上下文
            _context = new EntityAIContext
            {
                Entity = entity,
                Genes = _genes
            };

            _genes.OnEvaluationChanged += OnGenesChanged;
        }

        private void Start()
        {
            // 初始评估
            OnGenesChanged(_genes.Evaluation);
        }

        private void OnGenesChanged(GenomeEvaluation eval)
        {
            if (eval == null) return;

            // 决定该启用什么模块
            var desiredModules = GeneAIDecisionService.DecideActiveModules(eval);

            // 移除不需要的
            var toRemove = new List<string>();
            foreach (var id in _activeModules.Keys)
            {
                if (!desiredModules.Contains(id)) toRemove.Add(id);
            }
            foreach (var id in toRemove)
            {
                // ★ 修复：卸载时调用 OnDisable
                _activeModules[id].OnDisable();
                _activeModules.Remove(id);
                Debug.Log($"[AI] 卸载模块 {id}");
            }

            // 挂载新的
            foreach (var id in desiredModules)
            {
                if (!_activeModules.ContainsKey(id))
                {
                    var module = CreateModule(id);
                    if (module != null)
                    {
                        // ★ 修复：传入上下文数据包 _context
                        module.Initialize(_context);
                        _activeModules.Add(id, module);
                        Debug.Log($"[AI] 启用模块 {id}");
                    }
                }
            }
        }

        private void Update()
        {
            // ★ 修复（防Bug回归）：AI 模块与实体基因共用模拟时间，暂停时不推进。
            float dt = ServiceLocator.TryGet<SimulationClock>(out var clock) ? clock.GameDeltaTime : Time.deltaTime;
            if (dt <= 0) return;
            foreach (var module in _activeModules.Values)
            {
                module.Tick(dt);
            }
        }

        // 简单工厂：根据 ID 创建具体的 AI 模块
        private IGeneAIModule CreateModule(string id)
        {
            switch (id)
            {
                case "ai_flight": return new FlightAIModule();
                case "ai_autotroph": return new AutotrophAIModule(); // 确保你建了这个文件
                default: return null;
            }
        }
    }
}
