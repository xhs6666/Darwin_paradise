using System.Collections.Generic;
using UnityEngine;

namespace DarwinPark.Genetics
{
    // ★ 修复：补回丢失的数据类
    public class GeneEvaluation
    {
        public GeneInstance Gene;
        internal int GenomeIndex;
        public GeneDefinition Definition;
        public GeneStateDefinition State;
        public float SuppressionAmount;
        public float EffectiveStrength;
        public Dictionary<string,float> AttributeContributions=new Dictionary<string,float>();
        public const float MinimumActiveStrength=10f;
        public float EnvironmentSuppression,RoleSuppression;public bool GestatingAtEvaluation;
        public bool MissingRequirements;
        public bool IsActive => EffectiveStrength >= MinimumActiveStrength && (Definition==null||!Definition.ExplicitState||State!=null);
    }

    public class GenomeEvaluation
    {
        public List<GeneEvaluation> Genes = new List<GeneEvaluation>();
        public Dictionary<string, float> Attributes = new Dictionary<string, float>();
    }

    public static class GeneValueResolver
    {
        /// <summary>
        /// ★ 修复（防Bug回归）：默认把环境压力施加给所有基因；遗传值不受影响，
        /// 环境适应突变仍可发生。可选豁免参数仅供显式需要豁免的调用使用。
        /// </summary>
        public static GenomeEvaluation Resolve(Genome genome, bool isGestating = false,
            float globalSuppression = 0f, string suppressExemptGeneId = null,IReadOnlyDictionary<string,float> additionalSuppression=null)
        {
            // ★ 适配：仅共享纯求值结果，返回值仍绑定调用者自己的基因。
            return GeneExpressionCache.Resolve(genome,isGestating,globalSuppression,suppressExemptGeneId,additionalSuppression);
        }
        internal static GenomeEvaluation ResolveUncached(Genome genome,bool isGestating,float globalSuppression,string suppressExemptGeneId,IReadOnlyDictionary<string,float> additionalSuppression)
        {
            var result = new GenomeEvaluation();
            if (genome == null) return result;

            // 1. 处理抑制关系
            // ★ 修复（防Bug回归）：同定义的启用实例受到相同抑制，按定义累计一次，保留原累加顺序。
            var suppressions = new Dictionary<string, float>();
            var definitions = new GeneDefinition[genome.Genes.Count];
            for (int index = 0; index < genome.Genes.Count; index++)
            {
                var gene = genome.Genes[index];
                if (!gene.Enabled) continue;
                var def = definitions[index] = GeneDatabase.Get(gene.DefinitionId);
                if (def == null || def.SuppressionRules == null) continue;

                // 如果是繁殖基因，且不在孕育期，则不产生抑制
                // ★ 修复（防Bug回归）：新增繁殖分支也遵守孕育抑制条件，未成年时不能冻结生长。
                if ((gene.DefinitionId == "gene_reproduction"||def.SuppressionOnlyWhileGestating) && !isGestating)
                    continue;

                foreach (var rule in def.SuppressionRules)
                {
                    if (rule.TargetGeneId == null) continue;
                    suppressions.TryGetValue(rule.TargetGeneId, out float current);
                    suppressions[rule.TargetGeneId] = current + gene.Value * rule.Coefficient / 1000f;
                }
            }

            // 2. 逐基因计算属性

            for (int index = 0; index < genome.Genes.Count; index++)
            {
                var gene = genome.Genes[index];
                if (!gene.Enabled) continue;
                var def = definitions[index];
                if (def == null) continue;

                suppressions.TryGetValue(gene.DefinitionId, out var sup);
                // ★ 适配：角色表达抑制只影响有效值与属性，不改写遗传值。
                float role=0;
                if(additionalSuppression!=null&&additionalSuppression.TryGetValue(gene.DefinitionId,out float extra)){role=Mathf.Max(0,extra);sup+=role;}
                bool exempt = !string.IsNullOrEmpty(suppressExemptGeneId)
                    && gene.DefinitionId == suppressExemptGeneId;
                float global = exempt ? 0f : Mathf.Max(0f, globalSuppression);
                float effectiveValue = Mathf.Max(0f, gene.Value - sup - global);

                var eval = new GeneEvaluation
                {
                    Gene = gene,
                    GenomeIndex = index,
                    Definition = def,
                    State = def.ResolveState(gene),
                    // ★ 修复（防Bug回归）：面板同时显示环境抑制，保证有效值与抑制值一致。
                    SuppressionAmount = sup + global,
                    // ★ 适配：仅记录来源数值供界面解释，不额外拼接字符串或改变求值。
                    EnvironmentSuppression=global,RoleSuppression=role,GestatingAtEvaluation=isGestating,
                    EffectiveStrength = effectiveValue
                };
                result.Genes.Add(eval);

            }

            // ★ 修复（防Bug回归）：同时记录每个表达真正贡献的属性，失效基因和被同谱系替代的共有属性不能冒用实体总值。
            // ★ 新增：先整体求组合依赖，防止前置缺失时部位和行为留下半生效属性。
            RemoveUnsatisfiedExpressions(result.Genes);
            result.Attributes = GeneAttributeInheritance.Accumulate(result.Genes);
            return result;
        }

        private static void RemoveUnsatisfiedExpressions(List<GeneEvaluation> evaluations)
        {
            if (!evaluations.Exists(gene => gene.IsActive && gene.Definition.RequiresGenes.Count > 0)) return;

            // ★ 修复（防Bug回归）：检查前置时按定义查找，保留同定义多个有效副本及级联失效。
            var activeCounts = new Dictionary<string, int>();
            foreach (var gene in evaluations)
            {
                if (!gene.IsActive) continue;
                string id = gene.Definition.Id;
                activeCounts.TryGetValue(id, out int count);
                activeCounts[id] = count + 1;
            }

            bool changed;
            do
            {
                changed = false;
                foreach (var gene in evaluations)
                {
                    if (!gene.IsActive) continue;
                    foreach (string required in gene.Definition.RequiresGenes)
                    {
                        if (required != null && activeCounts.ContainsKey(required)) continue;
                        string id = gene.Definition.Id;
                        if (--activeCounts[id] == 0) activeCounts.Remove(id);
                        gene.MissingRequirements = true;
                        gene.EffectiveStrength = 0;
                        changed = true;
                        break;
                    }
                }
            } while (changed);
        }
    }
}
