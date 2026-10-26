using UnityEngine;
using DarwinPark.Genetics;

namespace DarwinPark.Entities.Logic
{
    // ★ 修复（防Bug回归）：生长基因只提供属性和提示，寿命由独立 EntityLifeCycle 统一推进。
    public class GrowthGeneLogic : IGeneLogic
    {
        private EntityGeneController _controller;
        public void Initialize(EntityGeneController controller,GeneInstance gene){_controller=controller;}
        public void OnUpdate(float dt) { }
        public string GetTooltipInfo()
        {
            var life=_controller.GetComponent<EntityLifeCycle>();
            string age=_controller.CurrentAge==AgeState.Infant?"幼年":_controller.CurrentAge==AgeState.Adult?"成年":"老年";
            return $"基因状态: <color=#06D6A0>{_controller.GetAttribute("growthSpeed"):F1} 成长力</color>\n实体寿命: {life?.Progress??0:F1}% (当前阶段: {age})";
        }
    }
}