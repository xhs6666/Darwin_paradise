using System.Collections.Generic;
using System.Linq;

namespace DarwinPark.Genetics
{
    [System.Serializable]
    public class Genome
    {
        public List<GeneInstance> Genes = new();
        public string BodyPlanId="organism_basic";
        public List<PartOverride> BodyOverrides=new();

        // Persistence snapshots keep gene instance identities and never consume the simulation RNG.
        public static Genome Snapshot(Genome source)
        {
            if(source==null)return null;
            var copy=new Genome { BodyPlanId=source.BodyPlanId };
            foreach(var part in source.BodyOverrides)
                copy.BodyOverrides.Add(new PartOverride{slotId=part.slotId,scale=part.scale,position=part.position,rotation=part.rotation});
            foreach(var gene in source.Genes)copy.Genes.Add(gene.Snapshot());
            return copy;
        }

        // ★ 适配：复制遗传数据由基因模型统一负责；客户端预览与权威投放共用，保持每次投放生成独立实例 ID。
        public static Genome Copy(Genome source)
        {
            if (source == null) return null;
            var copy = new Genome();
            copy.BodyPlanId=source.BodyPlanId;
            foreach(var part in source.BodyOverrides)copy.BodyOverrides.Add(new PartOverride{slotId=part.slotId,scale=part.scale,position=part.position,rotation=part.rotation});
            foreach (var gene in source.Genes)
            {
                var item = GeneInstance.Create(gene.DefinitionId, gene.Value, gene.Tier);
                item.Enabled = gene.Enabled; item.CopyStrength = gene.CopyStrength;
                item.StateId=gene.StateId;
                item.OriginInstanceId = gene.OriginInstanceId;
                item.MissingStateIds = new List<string>(gene.MissingStateIds);
                copy.Add(item);
            }
            return copy;
        }

        public GeneInstance Find(string instanceId) =>
            Genes.FirstOrDefault(g => g.InstanceId == instanceId);

        public IEnumerable<GeneInstance> FindByDefinition(string defId) =>
            Genes.Where(g => g.DefinitionId == defId);

        public bool HasDefinition(string defId) => Genes.Any(g => g.DefinitionId == defId);

        public GeneInstance Add(GeneInstance gene)
        {
            Genes.Add(gene);
            return gene;
        }

        public bool Remove(string instanceId)
        {
            var g = Find(instanceId);
            return g != null && Genes.Remove(g);
        }
    }
}
