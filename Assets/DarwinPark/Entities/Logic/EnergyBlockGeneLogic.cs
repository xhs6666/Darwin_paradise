using DarwinPark.Entities.Ecology;
using DarwinPark.Genetics;

namespace DarwinPark.Entities.Logic
{
    // ★ 新增：能量块转化为独立基因行为，不继承营养生产逻辑，复制基因也只结算一次自动吸收。
    public sealed class EnergyBlockGeneLogic : IGeneLogic
    {
        private EntityGeneController _genes;private GeneInstance _gene;private EnergyBlockMetabolism _metabolism;
        public void Initialize(EntityGeneController controller,GeneInstance gene)
        {_genes=controller;_gene=gene;_metabolism=EnergyBlockMetabolism.Ensure(controller.gameObject);}
        public void OnUpdate(float dt){if(GeneBehaviorAuthority.IsPrimary<EnergyBlockGeneLogic>(_genes,_gene))_metabolism.Advance(dt);}
        public string GetTooltipInfo()=>"饥饿时自动吸收附近能量块；操控时按Q吐出。\n消耗自身营养："+_genes.GetAttribute(NutrientUtilizationSettings.Current.releaseAttribute).ToString("F1")+"；储存营养优先。";
    }
}
