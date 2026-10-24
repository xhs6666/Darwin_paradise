using UnityEngine;
using DarwinPark.Genetics;
using DarwinPark.Entities.Stats;
using DarwinPark.Planet;

namespace DarwinPark.Entities.Logic
{
    public class NutritionGeneLogic : IGeneLogic
    {
        private EntityGeneController _controller;
        private GeneInstance _gene;

        public void Initialize(EntityGeneController controller, GeneInstance gene)
        {
            _controller = controller;
            _gene = gene;
        }

        public void OnUpdate(float dt)
        {
            var stats = _controller.GetComponent<EntityStats>();
            if (stats == null) return;

            float absorbEfficiency = _controller.GetAttribute("absorbEfficiency");
            if (absorbEfficiency <= 0.01f) return;

            var def = GeneDatabase.Get(_gene.DefinitionId);
            var state = def?.ResolveState(_gene.Value, _gene.MissingStateIds);

            if (state != null)
            {
                if (state.StateId == "photo") // 阳光自养
                {
                    stats.Modify("nutrient", absorbEfficiency * 0.5f * dt);
                }
                else if (state.StateId == "chemo") // 化生自养
                {
                    // ★ 修复：传入两个参数
                    TryAbsorbFromOre(stats, absorbEfficiency, dt);
                }
                else if (state.StateId == "hetero")
                {
                    return;
                }
            }
        }

        // ★ 修复：补全方法定义，接收 absorbEfficiency 参数
        private void TryAbsorbFromOre(EntityStats stats, float absorbEfficiency, float dt)
        {
            if (PlanetOreSystem.Instance == null) return;

            // 检查脚下这片土地有没有矿
            float availableOre = PlanetOreSystem.Instance.GetOreAmount(_controller.transform.position);

            if (availableOre > 0.1f)
            {
                // 开采量 = 基础速度 * 效率
                // ★ 修复（防Bug回归）：采矿与其它营养模式使用同一模拟步长。
                float extractAmount = 10f * absorbEfficiency * dt;
                float actualExtracted = PlanetOreSystem.Instance.ConsumeOre(_controller.transform.position, extractAmount);

                if (actualExtracted > 0)
                {
                    stats.Modify("nutrient", actualExtracted);
                }
            }
        }

        public string GetTooltipInfo()
        {
            var stats = _controller.GetComponent<EntityStats>();
            string info = "";
            if (stats != null && stats.TryGet("storedNutrient", out var stored))
                info += $"当前存储营养: <color=#FFD166>{stored:F1}</color>\n";

            float finalAbsorb = _controller.GetAttribute("absorbEfficiency");
            info += $"当前吸收效率(最终值): {finalAbsorb:F2}";
            return info;
        }
    }
}
