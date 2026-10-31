using DarwinPark.Entities.Ecology;
using DarwinPark.Genetics;
namespace DarwinPark.Entities.Logic
{
    // ★ 新增：宿主跟随、碰撞恢复和吸取由组件持有，基因逻辑只负责激活与信息展示。
    public sealed class ParasiticGeneLogic : IGeneLogic
    {
        private ParasiteBehavior _parasite;
        public void Initialize(EntityGeneController controller,GeneInstance gene){_parasite=controller.GetComponent<ParasiteBehavior>()??controller.gameObject.AddComponent<ParasiteBehavior>();}
        public void OnUpdate(float dt){}
        public string GetTooltipInfo()=>_parasite.Host!=null?"宿主："+_parasite.Host.DisplayName+(_parasite.Internal?"（体内）":"（体外）"):"缓慢寻找更大的存活宿主；脱离后会饥饿";
    }
}
