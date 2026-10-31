using DarwinPark.Entities.Ecology;
using DarwinPark.Genetics;
namespace DarwinPark.Entities.Logic
{
    // ★ 新增：种群继承行动和领地接口，群体状态与命令由独立组件保存。
    public sealed class PopulationGeneLogic : TerritoryGeneLogic
    {
        private PopulationBehavior _population;
        public override void Initialize(EntityGeneController controller,GeneInstance gene)
        {base.Initialize(controller,gene);_population=controller.GetComponent<PopulationBehavior>()??controller.gameObject.AddComponent<PopulationBehavior>();
            // ★ 适配：角色组件使用时安装，不依赖场景手工挂载。
            AntColonyMember.Ensure(_population);}
        public override string GetTooltipInfo()=>"种群 "+_population.MemberCount+" 成员 · "+(_population.IsLeader?"领袖":"成员")+" · "+PopulationBehavior.CommandLabel(_population.Command);
    }
}
