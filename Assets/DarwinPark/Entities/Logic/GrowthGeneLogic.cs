using UnityEngine;
using DarwinPark.Genetics;
using DarwinPark.Entities.Stats;

namespace DarwinPark.Entities.Logic
{
    public class GrowthGeneLogic : IGeneLogic
    {
        private EntityGeneController _controller;
        private GeneInstance _gene;

        // ★ 核心：隐藏的寿命进度（0-100）
        private float _ageProgress = 0f;



        public void Initialize(EntityGeneController controller, GeneInstance gene)
        {
            _controller = controller;
            _gene = gene;
        }

        public void OnUpdate(float dt)
        {
            var stats = _controller.GetComponent<EntityStats>();
            if (stats == null || stats.TryGet("health", out var hp) && hp <= 0) return;

            var def = GeneDatabase.Get(_gene.DefinitionId);
            var state = def?.ResolveState(_gene.Value, _gene.MissingStateIds);

            if (state == null) return;

            // 1. 进度推进
            if (_controller.CurrentAge == AgeState.Infant || _controller.CurrentAge == AgeState.Adult)
            {
                // 幼年/成年阶段：由 growthSpeed 驱动
                float growthSpeed = _controller.GetAttribute("growthSpeed");
                _ageProgress += growthSpeed * dt * 0.2f; // 乘以0.2防止进化太快
            }
            else if (_controller.CurrentAge == AgeState.Elder)
            {
                // 老年阶段：由 agingSpeed 驱动走向死亡
                float agingSpeed = _controller.GetAttribute("agingSpeed");
                _ageProgress += agingSpeed * dt * 0.1f;

                if (_ageProgress >= 100f)
                {
                    stats.Modify("health", -2f * dt); // 寿命耗尽，持续扣血
                }
            }

            _ageProgress = Mathf.Clamp(_ageProgress, 0f, 100f);

            // 2. 更新实体阶段
            var previousAge = _controller.CurrentAge;
            if (_ageProgress < 30f)
            {
                _controller.CurrentAge = AgeState.Infant;
            }
            else if (_ageProgress >= 30f && _ageProgress < 80f)
            {
                _controller.CurrentAge = AgeState.Adult;
            }
            else
            {
                _controller.CurrentAge = AgeState.Elder;
            }
            // ★ 修复（防Bug回归）：成年/老年切换时立即重算年龄联动属性。
            if (previousAge != _controller.CurrentAge) _controller.Recalculate();
        }

        public string GetTooltipInfo()
        {
            string ageName = "幼年";
            if (_controller.CurrentAge == AgeState.Adult) ageName = "成年";
            else if (_controller.CurrentAge == AgeState.Elder) ageName = "老年";

            string info = $"基因状态: <color=#06D6A0>{_controller.GetAttribute("growthSpeed"):F1} 成长力</color>\n";
            info += $"实体寿命: {_ageProgress:F1}% (当前阶段: {ageName})";
            return info;
        }
    }
}
