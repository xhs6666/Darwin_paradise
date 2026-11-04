using DarwinPark.Entities.Ecology;
using DarwinPark.Genetics;

namespace DarwinPark.Entities.Logic
{
    // ★ 新增：复制基因共享一个组件；寄生与食腐各自检查主表达，不重复吸收。
    public sealed class HeterotrophyGeneLogic:IGeneLogic
    {
        private EntityGeneController _genes;
        public void Initialize(EntityGeneController controller,GeneInstance gene){_genes=controller;if(controller.GetComponent<NutritionParasite>()==null)controller.gameObject.AddComponent<NutritionParasite>();}
        public void OnUpdate(float dt){}
        public string GetTooltipInfo()=>NutritionTransfer.CanDigest(_genes)?"击败后按消化效率吸收营养，吃不下的营养留为尸体。":"幼年寄生：隐藏并跟随宿主吸收饱食度，保留宿主安全口粮；成年脱离，独立消化取食。";
    }
    public sealed class ScavengingGeneLogic:IGeneLogic
    {
        public void Initialize(EntityGeneController controller,GeneInstance gene){if(controller.GetComponent<CorpseFeeding>()==null)controller.gameObject.AddComponent<CorpseFeeding>();}
        public void OnUpdate(float dt){}
        public string GetTooltipInfo()=>"吸收尸体剩余营养；有行动能力时自动靠近尸体，没有行动能力只能吸收接触范围内的尸体。";
    }
    public sealed class EggParasiticGeneLogic:ReproductionGeneLogic { }
}
