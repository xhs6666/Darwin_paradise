using DarwinPark.Entities.Ecology;
using DarwinPark.Genetics;
namespace DarwinPark.Entities.Logic
{
    // ★ 新增：观察是被动能力，按需收取坐标；不为每个实体新增逐帧MonoBehaviour。
    public sealed class ObservationGeneLogic:IGeneLogic
    {
        private EntityGeneController _genes;
        public void Initialize(EntityGeneController controller,GeneInstance gene)=>_genes=controller;
        public void OnUpdate(float dt){}
        public string GetTooltipInfo()=>$"有效视野范围：{EntityPerceptionHub.Range(_genes):F1}；范围观察接收广域坐标，注意追踪更及时更新已知目标位置。";
    }
}
