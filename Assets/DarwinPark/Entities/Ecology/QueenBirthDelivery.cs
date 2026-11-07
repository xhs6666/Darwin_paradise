using System.Linq;
using DarwinPark.Genetics;

namespace DarwinPark.Entities.Ecology
{
    // ★ 新增：每胎逐个落地，空间/数量不足就保留剩余胎数；每个孩子有独立基因实例和分工。
    public static class QueenBirthDelivery
    {
        public static bool TrySpawn(AntColonyMember queen)
        {
            if(!ColonyFormation.TryBirthPosition(queen,out var point))return false;
            var child=new Genome();
            foreach(var gene in queen.Genes.Genome.Genes)
            {
                var copy=GeneInstance.Create(gene.DefinitionId,gene.Value,gene.Tier);
                copy.Enabled=gene.Enabled;copy.CopyStrength=gene.CopyStrength;copy.MissingStateIds=new System.Collections.Generic.List<string>(gene.MissingStateIds);copy.OriginInstanceId=gene.InstanceId;
                var partner=queen.State.partnerGenome?.Genes?.FirstOrDefault(g=>g.DefinitionId==gene.DefinitionId);
                if(partner!=null)copy.Value=(copy.Value+partner.Value)*.5f;
                child.Add(copy);
            }
            return EcologyReproduction.Spawn(queen.Genes,child,point);
        }
    }
}
