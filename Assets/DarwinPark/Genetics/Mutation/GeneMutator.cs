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
            // 如果基因组里还没有该基因，压力变异直接插入一个新基因。
            bool preferAdaptation = !string.IsNullOrEmpty(settings.AdaptationGeneId)
                && settings.AdaptationMutationChance > 0f
                && Random.value < settings.AdaptationMutationChance;
            if (preferAdaptation)
            {
                gene = genome.Genes.Find(g => g.DefinitionId == settings.AdaptationGeneId);
                if (gene == null)
                {
                    // 直接按压力方向创建，而不是先插入一个中性值。
                    float initialValue = settings.AdaptationTargetValue >= 0f
                        ? settings.AdaptationTargetValue
                        : 50f;
                    var added = GeneInstance.Create(settings.AdaptationGeneId, initialValue, 0);
                    genome.Add(added);
                    return MutationResult.Ok(MutationType.Insertion,
                        $"环境压力插入 {settings.AdaptationGeneId}", added);
                }
            }

            if (gene == null)
            {
                int index = Random.Range(0, genome.Genes.Count);
                gene = genome.Genes[index];
            }

            float roll = Random.value;

            // 压力选中的适应基因只做“定向数值漂移”，避免立即被缺失/复制噪声淹没。
            if (preferAdaptation)
            {
                float direction = settings.AdaptationTargetValue >= 0f
                    ? Mathf.Sign(settings.AdaptationTargetValue - gene.Value)
                    : Mathf.Sign(Random.Range(-1f, 1f));
                if (Mathf.Approximately(direction, 0f)) direction = 1f;

                float delta = Random.Range(5f, 15f) * direction;
                gene.Value = Mathf.Clamp(gene.Value + delta, 0f, 100f);
                return MutationResult.Ok(MutationType.Error,
                    $"{gene.DefinitionId} 在环境压力下偏移到 {gene.Value:F1}");
            }
            if (roll < 0.6f) // 40% 出错
            {
                float delta = Random.Range(-15f, 15f);
                gene.Value = Mathf.Clamp(gene.Value + delta, 0f, 100f);
                return MutationResult.Ok(MutationType.Error, $"{gene.DefinitionId} 偏移到 {gene.Value:F1}");
            }
            else if (roll < 0.8f) // 30% 复制变异
            {
                var copy = GeneInstance.Create(gene.DefinitionId, gene.Value, gene.Tier);
                copy.CopyStrength = Random.Range(0.5f, 1.5f);

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

                genome.Add(copy);
                return MutationResult.Ok(MutationType.Duplicate, $"复制出了 {copy.DefinitionId}");
            }
            else // 30% 缺失
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