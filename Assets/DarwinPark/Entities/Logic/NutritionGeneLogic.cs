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
            // ★ 适配：生产由独立收支模块结算，避免直接夹掉溢出营养或复制基因重复结算。
            Ecology.EcologyEnergyBudget.Produce(_controller,_gene,dt);
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
