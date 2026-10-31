using DarwinPark.Entities.Ecology;
using DarwinPark.Genetics;
namespace DarwinPark.Entities.Logic
{
    // ★ 新增：社会谱系沿用原行动器的攻击/冷却/地表约束，只安装独立领地决策组件。
    public class TerritoryGeneLogic : LocomotionGeneLogic
    {
        protected TerritoryBehavior Territory;
        public override void Initialize(EntityGeneController controller,GeneInstance gene)
        {base.Initialize(controller,gene);Territory=controller.GetComponent<TerritoryBehavior>()??controller.gameObject.AddComponent<TerritoryBehavior>();}
        // ★ 适配：避免提示把领地能力误写成始终巡逻，实际活动由独立实体状态决定。
        public override string GetTooltipInfo()=>Territory.EvictionRemaining>0?"遭到驱逐，撤离后重新定居":"领地半径 "+Territory.Radius.ToString("F1")+"m；由实体状态决定行动";
    }
}
