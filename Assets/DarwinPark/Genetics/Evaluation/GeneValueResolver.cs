using System.Collections.Generic;
using UnityEngine;

namespace DarwinPark.Genetics
{
    // ★ 修复：补回丢失的数据类
    public class GeneEvaluation
    {
        public GeneInstance Gene;
        public GeneDefinition Definition;
        public GeneStateDefinition State;
        public float SuppressionAmount;
        public float EffectiveStrength;
        public Dictionary<string,float> AttributeContributions=new Dictionary<string,float>();
        public bool IsActive => EffectiveStrength >= 10f; // 生效阈值改为 10
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
            var result = new GenomeEvaluation();
            if (genome == null) return result;

            // 1. 处理抑制关系
            var suppressions = new Dictionary<GeneInstance, float>();
            foreach (var gene in genome.Genes)
            {
                if (!gene.Enabled) continue;
                var def = GeneDatabase.Get(gene.DefinitionId);
                if (def == null || def.SuppressionRules == null) continue;

                // 如果是繁殖基因，且不在孕育期，则不产生抑制
                if (gene.DefinitionId == "gene_reproduction" && !isGestating)
                    continue;

                foreach (var rule in def.SuppressionRules)
                {
                    foreach (var target in genome.FindByDefinition(rule.TargetGeneId))
                    {
                        if (!target.Enabled) continue;
                        suppressions.TryGetValue(target, out var cur);
                        suppressions[target] = cur + gene.Value * rule.Coefficient / 1000f;
                    }
                }
            }

            // 2. 逐基因计算属性

            foreach (var gene in genome.Genes)
            {
                if (!gene.Enabled) continue;
                var def = GeneDatabase.Get(gene.DefinitionId);
                if (def == null) continue;

                suppressions.TryGetValue(gene, out var sup);
                // ★ 适配：角色表达抑制只影响有效值与属性，不改写遗传值。
                if(additionalSuppression!=null&&additionalSuppression.TryGetValue(gene.DefinitionId,out float extra))sup+=Mathf.Max(0,extra);
                bool exempt = !string.IsNullOrEmpty(suppressExemptGeneId)
                    && gene.DefinitionId == suppressExemptGeneId;
                float global = exempt ? 0f : Mathf.Max(0f, globalSuppression);
                float effectiveValue = Mathf.Max(0f, gene.Value - sup - global);

                var eval = new GeneEvaluation
                {
                    Gene = gene,
                    Definition = def,
                    State = def.ResolveState(gene.Value, gene.MissingStateIds),
                    // ★ 修复（防Bug回归）：面板同时显示环境抑制，保证有效值与抑制值一致。
                    SuppressionAmount = sup + global,
                    EffectiveStrength = effectiveValue
                };
                result.Genes.Add(eval);

            }

            // ★ 修复（防Bug回归）：同时记录每个表达真正贡献的属性，失效基因和被同谱系替代的共有属性不能冒用实体总值。
            result.Attributes = GeneAttributeInheritance.Accumulate(result.Genes);
            return result;
        }
    }
}
