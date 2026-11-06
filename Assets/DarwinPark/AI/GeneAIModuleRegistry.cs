using System;
using System.Collections.Generic;
using DarwinPark.Genetics;

namespace DarwinPark.AI
{
    // ★ 适配：工厂统一走注册表，扩展模块不再被控制器内的第二份switch忽略。
    public static class GeneAIModuleRegistry
    {
        private static readonly Dictionary<string, Func<IGeneAIModule>> Factories = new()
        {
            ["ai_flight"] = () => new Modules.FlightAIModule(),
            ["ai_autotroph"] = () => new Modules.AutotrophAIModule()
        };
        public static void Register(string moduleId, Func<IGeneAIModule> factory)
        {if (!string.IsNullOrEmpty(moduleId) && factory != null) Factories[moduleId] = factory;}
        public static IGeneAIModule Create(string moduleId)
        {return moduleId != null && Factories.TryGetValue(moduleId, out var factory) ? factory() : null;}
    }

    // ★ 适配：保留公开服务入口；控制器传入缓冲区，表达变化时不重复分配HashSet。
    public static class GeneAIDecisionService
    {
        public static HashSet<string> DecideActiveModules(GenomeEvaluation evaluation)
        {var result = new HashSet<string>(); Collect(evaluation, result); return result;}
        public static void Collect(GenomeEvaluation evaluation, HashSet<string> result)
        {
            result.Clear();
            if (evaluation == null) return;
            foreach (var gene in evaluation.Genes)
                if (gene.IsActive && !string.IsNullOrEmpty(gene.Definition.AiModuleId)) result.Add(gene.Definition.AiModuleId);
        }
    }
}
