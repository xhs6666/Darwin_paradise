using UnityEngine;
using DarwinPark.Genetics;
using DarwinPark.Entities.Stats;

namespace DarwinPark.Entities.Logic
{
    /// <summary>
    /// 环境适应基因的展示逻辑。
    /// ★ 新增：基因本身只提供 environmentResistance 属性与耐寒/耐热状态，
    /// 实际伤害计算集中在 EntityEnvironmentHazard，避免基因逻辑与伤害系统互相耦合。
    /// </summary>
    public class EnvironmentAdaptationGeneLogic : IGeneLogic
    {
        private EntityGeneController _controller;
        private GeneInstance _gene;

        public void Initialize(EntityGeneController controller, GeneInstance gene)
        {
            _controller = controller;
            _gene = gene;
        }

        public void OnUpdate(float deltaTime)
        {
            // 被动基因，无需每帧逻辑。
        }

        public string GetTooltipInfo()
        {
            var def = GeneDatabase.Get(_gene.DefinitionId);
            var state = def?.ResolveState(_gene.Value, _gene.MissingStateIds);
            string direction = state != null ? state.DisplayName : "未分化";
            float resistance = _controller.GetAttribute("environmentResistance");
            return $"适应方向: <color=#6EE7FF>{direction}</color>\n环境抵抗力: {resistance:F1}";
        }
    }
}
