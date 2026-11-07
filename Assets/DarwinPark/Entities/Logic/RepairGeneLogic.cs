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

        // ★ 修复（防Bug回归）：EntityVitals已经统一修复，基因再次修复会重复扣粮并绕过溺水限制。
        public void OnUpdate(float dt) { }

        public string GetTooltipInfo()
        {
            return "被动生效中"; // 修复/储存通常是纯被动，不需要显示特殊状态
        }

    }
}