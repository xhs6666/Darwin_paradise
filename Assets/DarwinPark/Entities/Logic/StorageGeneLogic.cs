using UnityEngine;
using DarwinPark.Genetics;

namespace DarwinPark.Entities.Logic
{
    public class StorageGeneLogic : IGeneLogic
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
            // 储存基因主要是通过 AttributeCurves 增加了 nutrientStorage（上限）
            // 真正的转换/封顶逻辑在 EntityVitals 中统一处理
        }

        public string GetTooltipInfo()
        {
            return "被动生效中"; // 修复/储存通常是纯被动，不需要显示特殊状态
        }

    }
}