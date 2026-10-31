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
                // ★ 适配：能量块基因独立注册，不混入光合/化生生产。
                case "EnergyBlockGeneLogic": return new EnergyBlockGeneLogic();
                case "StorageGeneLogic": return new StorageGeneLogic();     // ★ 补上这个
                case "LocomotionGeneLogic": return new LocomotionGeneLogic();
                case "ReproductionGeneLogic": return new ReproductionGeneLogic();
                case "EnvironmentAdaptationGeneLogic": return new EnvironmentAdaptationGeneLogic();
                // ★ 新增：三种社会/寄生逻辑和卵生前置均显式注册，避免配置存在但运行无行为。
                case "TerritoryGeneLogic": return new TerritoryGeneLogic();
                case "PopulationGeneLogic": return new PopulationGeneLogic();
                case "ParasiticGeneLogic": return new ParasiticGeneLogic();
                case "OviparousGeneLogic": return new OviparousGeneLogic();
                default:
                    Debug.LogWarning($"[Gene] 未注册的基因逻辑脚本: {scriptId}");
                    return null;
            }
        }
    }
}
