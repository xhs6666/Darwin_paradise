using UnityEngine;
using DarwinPark.Genetics;

namespace DarwinPark.Entities.Logic
{
    public interface IGeneLogic
    {
        void Initialize(EntityGeneController controller, GeneInstance gene);
        void OnUpdate(float deltaTime);
        string GetTooltipInfo(); // ★ 新增：返回悬浮框里的额外信息
    }

    public static class GeneLogicRegistry
    {
        public static IGeneLogic CreateLogic(string scriptId)
        {
            switch (scriptId)
            {
                case "GrowthGeneLogic": return new GrowthGeneLogic();
                case "RepairGeneLogic": return new RepairGeneLogic();
                case "NutritionGeneLogic": return new NutritionGeneLogic(); // ★ 这里
                case "StorageGeneLogic": return new StorageGeneLogic();     // ★ 补上这个
                case "LocomotionGeneLogic": return new LocomotionGeneLogic();
                case "ReproductionGeneLogic": return new ReproductionGeneLogic();
                case "EnvironmentAdaptationGeneLogic": return new EnvironmentAdaptationGeneLogic();
                default:
                    Debug.LogWarning($"[Gene] 未注册的基因逻辑脚本: {scriptId}");
                    return null;
            }
        }
    }
}