using UnityEngine;
using DarwinPark.Genetics; // ★ 核心修复：引入底层基因命名空间

namespace DarwinPark.Entities.Logic
{
    public class RepairGeneLogic : IGeneLogic
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
            float repairEfficiency = _controller.GetAttribute("repairEfficiency");
            float repairCapability = _controller.GetAttribute("repairCapability");

            var stats = _controller.GetComponent<DarwinPark.Entities.Stats.EntityStats>();
            if (stats == null) return;

            if (stats.TryGet("health", out var hp) && stats.TryGet("nutrient", out var nutrient))
            {
                // 如果生命值低于100，大于最低修复阈值，且营养大于5
                if (hp < 100f && hp > repairCapability && nutrient > 5f && repairEfficiency > 0f)
                {
                    float hpToRepair = repairEfficiency * dt;
                    stats.Modify("health", hpToRepair);
                    stats.Modify("nutrient", -hpToRepair * 0.5f);
                }
            }
        }

        public string GetTooltipInfo()
        {
            return "被动生效中"; // 修复/储存通常是纯被动，不需要显示特殊状态
        }

    }
}