using UnityEngine;
using System.Linq;

namespace DarwinPark.Genetics.Mutation
{
    public static class GeneMutator
    {
        public static Genome CreateBaseGenome()
        {
            var genome = new Genome();
            genome.Add(GeneInstance.Create("gene_growth", 20f, 0));
            genome.Add(GeneInstance.Create("gene_nutrition", 50f, 0));
            // ★ 新增：没有实体默认 JSON 时的兜底基因组也要有环境适应方向。
            genome.Add(GeneInstance.Create("gene_environment", 50f, 0));
            return genome;
        }

        public static MutationResult Mutate(Genome genome, GeneMutationSettings settings)
        {
            if (genome.Genes.Count == 0) return MutationResult.Fail("基因组为空");
            if (settings == null) settings = new GeneMutationSettings();

            GeneInstance gene = null;

            // ★ 新增：环境压力会提高环境适应基因的变异概率。
            bool preferAdaptation = !string.IsNullOrEmpty(settings.AdaptationGeneId)
                && settings.AdaptationMutationChance > 0f
                && Random.value < settings.AdaptationMutationChance;
            // ★ 修复（防Bug回归）：环境压力仅偏向已有基因，不再绕过 90/5/5 概率插入新类型。
            if(preferAdaptation)gene=genome.Genes.Find(g=>g.DefinitionId==settings.AdaptationGeneId);
            if (gene == null)
            {
                int index = Random.Range(0, genome.Genes.Count);
                gene = genome.Genes[index];
            }

            float total=Mathf.Max(0,settings.ErrorWeight)+Mathf.Max(0,settings.DuplicateWeight)+Mathf.Max(0,settings.DeletionWeight);
            if(total<=0)return MutationResult.Fail("变异权重之和必须大于零");
            float roll = Random.value*total;

            if (roll < settings.ErrorWeight) // ★ 修复：默认 90% 数值出错，实际读取配置权重
            {
                // ★ 适配：数值出错分支可沿工具配置的侧向出错关系迁移，仍保持90/5/5类型权重。
                string errorTarget=GeneTreeStorage.Successor(gene.DefinitionId,"error");
                if(errorTarget!=null) gene.DefinitionId=errorTarget;
                float delta = Random.Range(-settings.MaxErrorDelta, settings.MaxErrorDelta);
                if(preferAdaptation && settings.AdaptationTargetValue>=0 && gene.DefinitionId==settings.AdaptationGeneId)
                    delta=Mathf.Abs(delta)*Mathf.Sign(settings.AdaptationTargetValue-gene.Value);
                gene.Value = Mathf.Clamp(gene.Value + delta, 0f, 100f);
                return MutationResult.Ok(MutationType.Error, $"{gene.DefinitionId} 偏移到 {gene.Value:F1}");
            }
            else if (roll < settings.ErrorWeight+settings.DuplicateWeight) // 默认 5% 复制
            {
                var copy = GeneInstance.Create(gene.DefinitionId, gene.Value, gene.Tier);
                copy.CopyStrength = Random.Range(settings.DuplicateMinStrength, settings.DuplicateMaxStrength);

                var def = GeneDatabase.Get(gene.DefinitionId);
                var currentState = def?.ResolveState(gene.Value, gene.MissingStateIds);

                // 平行变异
                if (currentState != null)
                {
                    if (currentState.StateId == "fast_growth" && Random.value < 0.4f)
                    {
                        copy.DefinitionId = "gene_repair";
                        Debug.Log("[变异] 生长基因补充了修复基因！");
                    }
                    else if (currentState.StateId == "hetero" && Random.value < 0.3f)
                    {
                        copy.DefinitionId = "gene_storage";
                        Debug.Log("[变异] 营养基因变异为储存基因！");
                    }
                }

                // ★ 适配：复制的新基因优先使用配置的向下进化关系。
                string next=GeneTreeStorage.Successor(gene.DefinitionId,"evolution");
                if(next!=null)copy.DefinitionId=next;
                genome.Add(copy);
                return MutationResult.Ok(MutationType.Duplicate, $"复制出了 {copy.DefinitionId}");
            }
            else // 默认 5% 缺失
            {
                // ★ 新增：基础基因保护逻辑
                string[] coreGenes = { "gene_growth", "gene_reproduction", "gene_nutrition" };
                if (System.Array.IndexOf(coreGenes, gene.DefinitionId) >= 0)
                {
                    // 统计当前基因组里这个基础基因有几个
                    int count = genome.Genes.Count(g => g.DefinitionId == gene.DefinitionId);
                    if (count <= 1)
                    {
                        return MutationResult.Fail($"{gene.DefinitionId} 是唯一的基础基因，无法缺失！");
                    }
                }

                genome.Remove(gene.InstanceId);
                return MutationResult.Ok(MutationType.Deletion, $"缺失了 {gene.DefinitionId}");
            }
        }
    }
}
