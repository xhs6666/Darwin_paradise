using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DarwinPark.Genetics.Mutation
{
    public static class MutationInsertion
    {
        public static MutationResult Apply(Genome genome, GeneMutationSettings settings)
        {
            if (genome.Genes.Count == 0)
                return MutationResult.Fail("无基因可作为联系源");

            var source = genome.Genes[Random.Range(0, genome.Genes.Count)];

            // ★ 修复：显式声明为 List 类型，防止编译器推导出错
            List<GeneDefinition> pool = GeneDatabase.All
                .Where(d => !genome.HasDefinition(d.Id))
                .ToList();

            if (pool.Count == 0)
            {
                pool = GeneDatabase.All.ToList();
            }

            if (pool.Count == 0)
                return MutationResult.Fail("基因库为空");

            var newPos = source.InteractionPosition + Random.insideUnitSphere * settings.MaxInteractionDistance;

            float prob = GeneInteractionSpace.InteractionProbability(source.InteractionPosition, newPos, settings.MaxInteractionDistance);
            if (prob <= 0f || Random.value > prob)
                return MutationResult.Fail($"交互概率不足 (prob: {prob:F2})");

            var def = pool[Random.Range(0, pool.Count)];
            float closeness = 1f - (Vector3.Distance(newPos, source.InteractionPosition) / settings.MaxInteractionDistance);

            var instance = new GeneInstance
            {
                InstanceId = System.Guid.NewGuid().ToString("N"),
                DefinitionId = def.Id,
                Value = Mathf.Lerp(20f, 80f, closeness),
                Tier = def.Tier,
                Enabled = true,
                InteractionPosition = newPos
            };

            genome.Add(instance);

            return MutationResult.Ok(MutationType.Insertion,
                $"增添新基因 {def.Id}（来源 {source.DefinitionId}，距离 {Vector3.Distance(newPos, source.InteractionPosition):F1}）",
                instance);
        }
    }
}