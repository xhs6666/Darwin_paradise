using DarwinPark.Entities;
using DarwinPark.Genetics;
using System.Collections.Generic;

namespace DarwinPark.AI
{
    public static class GeneAIDecisionService
    {
        public static HashSet<string> DecideActiveModules(GenomeEvaluation eval)
        {
            var result = new HashSet<string>();
            foreach (var g in eval.Genes)
            {
                if (!g.IsActive) continue;
                // 假设基因定义的 aiModuleId 字段存了 "ai_flight"
                if (!string.IsNullOrEmpty(g.Definition.AiModuleId))
                {
                    result.Add(g.Definition.AiModuleId);
                }
            }
            return result;
        }
    }
}