using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace DarwinPark.Genetics
{
    // ★ 新增：解释真实表达结果；禁用基因只为检查界面补只读条目，不改变运行中的求值/属性或逻辑挂载。
    public static class GeneFailureReasons
    {
        public static List<GeneEvaluation> Inspect(Genome genome,GenomeEvaluation evaluation)
        {
            // ★ 修复（防Bug回归）：批量检查先建实例索引；禁用行只构造自己的诊断数据。
            var byGene = new Dictionary<GeneInstance, GeneEvaluation>();
            if (evaluation != null)
                foreach (var value in evaluation.Genes)
                    if (!byGene.ContainsKey(value.Gene)) byGene.Add(value.Gene, value);

            var result = new List<GeneEvaluation>(genome.Genes.Count);
            foreach (var gene in genome.Genes)
            {
                if (!byGene.TryGetValue(gene, out var inspected)) inspected = InspectGene(gene, null);
                if (inspected != null) result.Add(inspected);
            }
            return result;
        }

        public static GeneEvaluation InspectGene(GeneInstance gene, GenomeEvaluation evaluation)
        {
            if (gene == null) return null;
            var existing = evaluation?.Genes.Find(value => value.Gene == gene);
            if (existing != null) return existing;
            var definition = GeneDatabase.Get(gene.DefinitionId);
            return definition == null ? null : new GeneEvaluation
            {
                Gene = gene, Definition = definition, State = definition.ResolveState(gene)
            };
        }
        public static List<string> Explain(Genome genome,GeneEvaluation evaluation)
        {
            var reasons=new List<string>();var gene=evaluation.Gene;
            if(evaluation.MissingRequirements)reasons.Add("缺少组合能力的前置基因。");
            if(!gene.Enabled){reasons.Add("该基因已被手动禁用。");return reasons;}
            if(gene.Value<GeneEvaluation.MinimumActiveStrength)reasons.Add($"原始值 {gene.Value:F1} 低于生效阈值 {GeneEvaluation.MinimumActiveStrength:F1}。");
            if(evaluation.EnvironmentSuppression>0)reasons.Add($"环境不适应抑制 {evaluation.EnvironmentSuppression:F1}。");
            if(evaluation.RoleSuppression>0)reasons.Add($"种群分工/角色表达抑制 {evaluation.RoleSuppression:F1}。");
            float other=evaluation.SuppressionAmount-evaluation.EnvironmentSuppression-evaluation.RoleSuppression;
            if(other>.0001f)
            {
                foreach(var source in genome.Genes)
                {
                    if(!source.Enabled)continue;var definition=GeneDatabase.Get(source.DefinitionId);if(definition?.SuppressionRules==null||!evaluation.GestatingAtEvaluation&&(source.DefinitionId=="gene_reproduction"||definition.SuppressionOnlyWhileGestating))continue;
                    foreach(var rule in definition.SuppressionRules)if(rule.TargetGeneId==gene.DefinitionId&&source.Value*rule.Coefficient>0)reasons.Add($"受到“{definition.DisplayName}”抑制 {source.Value*rule.Coefficient/1000f:F1}。");
                }
                reasons.Add($"基因间抑制合计 {other:F1}。");
            }
            if(!evaluation.IsActive)reasons.Add($"有效值 = 原始值 {gene.Value:F1} − 抑制 {evaluation.SuppressionAmount:F1} = {evaluation.EffectiveStrength:F1}，未达到 {GeneEvaluation.MinimumActiveStrength:F1}。");
            if(evaluation.State==null)reasons.Add("没有可用基因状态（状态缺失或定义未提供）。");
            if(evaluation.IsActive&&gene.CopyStrength<=0)reasons.Add("表达值生效，但复制强度为0，不提供属性贡献。");
            return reasons;
        }
        public static string Summary(GeneEvaluation evaluation)=>evaluation.Definition.ExplicitState&&evaluation.State==null?"缺少可用模式":evaluation.MissingRequirements?"缺少前置基因":!evaluation.Gene.Enabled?"已禁用":evaluation.EffectiveStrength<GeneEvaluation.MinimumActiveStrength?(evaluation.SuppressionAmount>0?"受抑制":"数值不足"):evaluation.State==null?"无可用状态":"生效中";
    }
}
