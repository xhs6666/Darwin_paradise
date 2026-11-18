using UnityEngine;
using System.Linq;

namespace DarwinPark.Genetics.Mutation
{
    public static class GeneMutator
    {
        public static Genome CreateBaseGenome()
        {
            // ★ 修复（防Bug回归）：兜底也读取同一默认实体配置，不能由第二份硬编码名单漏掉基础繁殖基因。
            var asset=Resources.Load<TextAsset>("Entities/entity_default");
            if(asset==null)throw new System.InvalidOperationException("缺少 Entities/entity_default，无法创建完整基础基因组");
            var data=JsonUtility.FromJson<EntityGeneData>(asset.text);
            if(data?.genes==null||data.genes.Count==0)throw new System.InvalidOperationException("默认实体基因配置为空");
            return data.ToGenome();
        }

        // ★ 修复（防Bug回归）：替换 DefinitionId 同样会删除原类型；所有减少必要基础副本的路径共用配置中的保护名单。
        private static bool IsLastBaseCopy(Genome genome,GeneInstance gene)
            => GeneMutationSettings.IsProtected(gene.DefinitionId)&&genome.Genes.Count(g=>g.DefinitionId==gene.DefinitionId)<=1;

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
                // ★ 新增：联合变异属于数值出错事件，不改变原有90/5/5类型权重。
                var joint=JointGeneMutation.Try(genome);if(joint!=null)return joint;
                // ★ 适配：数值出错分支可沿工具配置的侧向出错关系迁移，仍保持90/5/5类型权重。
                string errorTarget=GeneTreeStorage.Successor(gene.DefinitionId,"error",gene.Value,genome);
                if(errorTarget!=null&&!IsLastBaseCopy(genome,gene)){gene.DefinitionId=errorTarget;gene.Tier=GeneDatabase.Get(errorTarget).Tier;gene.StateId=GeneDatabase.Get(errorTarget).DefaultStateId;}
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

                copy.StateId=gene.StateId;

                // ★ 适配：复制的新基因优先使用配置的向下进化关系。
                string next=GeneTreeStorage.Successor(gene.DefinitionId,"evolution",gene.Value,genome);
                if(next!=null){copy.DefinitionId=next;copy.Tier=GeneDatabase.Get(next).Tier;copy.StateId=GeneDatabase.Get(next).DefaultStateId;}
                genome.Add(copy);
                return MutationResult.Ok(MutationType.Duplicate, $"复制出了 {copy.DefinitionId}");
            }
            else // 默认 5% 缺失
            {
                if (IsLastBaseCopy(genome,gene))
                {
                    return MutationResult.Fail($"{gene.DefinitionId} 是唯一的基础基因，无法缺失！");
                }

                genome.Remove(gene.InstanceId);
                return MutationResult.Ok(MutationType.Deletion, $"缺失了 {gene.DefinitionId}");
            }
        }
    }
}
