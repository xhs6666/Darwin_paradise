using DarwinPark.Genetics;
namespace DarwinPark.Entities.Logic
{
    // ★ 新增：阳光与矿物各自结算主副本，基础营养只提供代谢，不重复生产或因移动被归零。
    public sealed class AutotrophyGeneLogic:IGeneLogic
    {
        private EntityGeneController _genes;private GeneInstance _source;
        public void Initialize(EntityGeneController genes,GeneInstance source){_genes=genes;_source=source;}
        public void OnUpdate(float dt)=>Ecology.EcologyEnergyBudget.Produce(_genes,_source,dt);
        public string GetTooltipInfo()=>"按独立营养部件获取资源；阳光自养受太阳高度影响，矿石自养扣除真实矿产，复制基因不重复结算。";
    }
    public sealed class NutrientParasitismGeneLogic:IGeneLogic
    {
        public void Initialize(EntityGeneController genes,GeneInstance source){if(genes.GetComponent<Ecology.NutritionParasite>()==null)genes.gameObject.AddComponent<Ecology.NutritionParasite>();}
        public void OnUpdate(float dt){}
        public string GetTooltipInfo()=>"独立营养寄生能力，附着并吸取宿主营养；不改变基础异养口部。";
    }
}
